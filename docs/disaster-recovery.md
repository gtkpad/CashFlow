# Plano de Disaster Recovery — CashFlow

> **Última revisão:** 2026-03-06
> **Responsável:** Equipe de Engenharia
> **Status:** Ativo

## Índice

1. [Objetivos de Recuperação (RPO/RTO)](#1-objetivos-de-recuperação-rporto)
2. [Classificação de Incidentes](#2-classificação-de-incidentes)
3. [Inventário de Componentes e Dependências](#3-inventário-de-componentes-e-dependências)
4. [Mecanismos de Resiliência Implementados](#4-mecanismos-de-resiliência-implementados)
5. [Runbooks por Cenário de Falha](#5-runbooks-por-cenário-de-falha)
   - [5.1 PostgreSQL — Banco Indisponível](#51-postgresql--banco-indisponível)
   - [5.2 PostgreSQL — Corrupção de Dados / Restore PITR](#52-postgresql--corrupção-de-dados--restore-pitr)
   - [5.3 RabbitMQ — Broker Indisponível](#53-rabbitmq--broker-indisponível)
   - [5.4 Container App — Serviço em CrashLoop](#54-container-app--serviço-em-crashloop)
   - [5.5 Deploy Falhado — Rollback de Revisão](#55-deploy-falhado--rollback-de-revisão)
   - [5.6 Mensagens em Dead Letter Queue](#56-mensagens-em-dead-letter-queue)
   - [5.7 Consistência Eventual Degradada](#57-consistência-eventual-degradada)
   - [5.8 Gateway Indisponível (Perda Total de Ingress)](#58-gateway-indisponível-perda-total-de-ingress)
   - [5.9 Perda de Região Azure (Disaster Regional)](#59-perda-de-região-azure-disaster-regional)
6. [Alertas e Detecção](#6-alertas-e-detecção)
7. [Escalation Matrix](#7-escalation-matrix)
8. [Teste de Restore (Procedimento Mensal)](#8-teste-de-restore-procedimento-mensal)
9. [Gaps Conhecidos e Roadmap](#9-gaps-conhecidos-e-roadmap)

---

## 1. Objetivos de Recuperação (RPO/RTO)

| Métrica | Definição | Meta | Justificativa |
|---|---|---|---|
| **RPO** (Recovery Point Objective) | Perda máxima de dados aceita | **< 5 minutos** | WAL contínuo do Azure PostgreSQL Flexible Server |
| **RTO** (Recovery Time Objective) | Tempo máximo de indisponibilidade | **< 4 horas** | Restore PITR + redeploy de Container Apps |
| **Retenção de backup** | Período mínimo disponível | **35 dias (PITR) + snapshots manuais para 90 dias** | Conformidade LGPD + BCB |

### Onde os dados vivem

| Dado | Localização | Durabilidade | RPO efetivo |
|---|---|---|---|
| Transactions (command side) | `transactions-db` (PostgreSQL) | ACID + WAL contínuo | < 5 min |
| Daily Summaries (query side) | `consolidation-db` (PostgreSQL) | ACID + WAL contínuo | < 5 min |
| Identidade/Usuários | `identity-db` (PostgreSQL) | ACID + WAL contínuo | < 5 min |
| Mensagens em trânsito | RabbitMQ (AzureFile volume) | Durável (disk-backed) | Depende de LRS/ZRS |
| Mensagens no Outbox | `transactions.OutboxMessage` (PostgreSQL) | ACID — mesmo backup do banco | < 5 min |
| Cache de consolidation | Output Cache (in-memory) | Efêmero — reconstruído automaticamente | N/A |

**Nota crítica:** O Consolidation é um **read model derivado**. Mesmo em perda total, pode ser reconstruído a partir das transactions (republishing de eventos).

---

## 2. Classificação de Incidentes

| Severidade | Critério | Tempo de Resposta | Exemplos |
|---|---|---|---|
| **SEV-1 (Crítico)** | Sistema indisponível para todos os usuários; perda de dados iminente ou confirmada | **15 min** | PostgreSQL down; Gateway crashloop; corrupção de dados |
| **SEV-2 (Alto)** | Funcionalidade principal degradada; dados não estão sendo perdidos | **1 hora** | Consumer parado (mensagens acumulando no outbox/RabbitMQ); latência > 5x normal |
| **SEV-3 (Médio)** | Funcionalidade secundária impactada; workaround disponível | **4 horas** | DLQ com mensagens; health check intermitente; alerta de throughput baixo |
| **SEV-4 (Baixo)** | Anomalia detectada sem impacto em usuários | **Próximo dia útil** | Log de warning elevado; métrica fora do baseline |

---

## 3. Inventário de Componentes e Dependências

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Azure Container Apps Environment                   │
│                                                                       │
│  ┌─────────┐    ┌──────────────┐    ┌────────────────┐               │
│  │ Gateway  │───►│ Transactions │───►│  PostgreSQL     │               │
│  │ (YARP)   │    │     API      │    │ (transactions)  │               │
│  │          │    └──────┬───────┘    └────────────────┘               │
│  │          │           │ outbox                                       │
│  │          │           ▼                                              │
│  │          │    ┌──────────────┐    ┌────────────────┐               │
│  │          │    │  RabbitMQ    │───►│ Consolidation  │               │
│  │          │    │  (messaging) │    │     API        │               │
│  │          │    └──────────────┘    └───────┬────────┘               │
│  │          │                                │                         │
│  │          │───►┌──────────────┐    ┌───────▼────────┐               │
│  │          │    │ Identity API │    │  PostgreSQL     │               │
│  └─────────┘    └──────┬───────┘    │ (consolidation) │               │
│                        │            └────────────────┘               │
│                 ┌──────▼───────┐                                      │
│                 │  PostgreSQL   │                                      │
│                 │  (identity)   │                                      │
│                 └──────────────┘                                      │
└─────────────────────────────────────────────────────────────────────┘
                         │
              ┌──────────▼──────────┐
              │   Azure Monitor      │
              │ (App Insights + Logs)│
              └─────────────────────┘
```

### Dependências Críticas por Serviço

| Serviço | Depende de | Impacto se dependência falhar |
|---|---|---|
| **Gateway** | Transactions API, Consolidation API, Identity API | HTTP 503 para rotas afetadas (YARP passive health marks destination unhealthy) |
| **Transactions API** | PostgreSQL (`transactions-db`), RabbitMQ | DB down → HTTP 500. RabbitMQ down → **aceita transactions normalmente** (outbox persiste no PostgreSQL) |
| **Consolidation API** | PostgreSQL (`consolidation-db`), RabbitMQ | DB down → consumer falha, circuit breaker abre. Leituras HTTP 500. RabbitMQ down → consumer desconecta, mensagens acumulam no outbox do Transactions |
| **Identity API** | PostgreSQL (`identity-db`) | DB down → login/registro falham (HTTP 500) |
| **RabbitMQ** | Azure File Share (volume) | Volume down → perda de mensagens não persistidas; restart pode perder estado |

---

## 4. Mecanismos de Resiliência Implementados

### 4.1 Consumer MassTransit — Pipeline de 4 camadas

O consumer `TransactionCreatedConsumer` tem a pipeline de resiliência mais sofisticada do sistema. A ordem de execução (outermost first):

```
CircuitBreaker → DelayedRedelivery → MessageRetry → EntityFrameworkOutbox → Consumer
```

| Camada | Configuração | Falha tratada |
|---|---|---|
| **Circuit Breaker** | 15% trip threshold, 10 msgs mínimo, reset 5 min | Falha sustentada do downstream (DB down) |
| **Delayed Redelivery** | 3 tentativas: 5 min, 15 min, 1 hora | Exaustão dos retries internos |
| **Message Retry** | Exponential 5× (100ms → 30s), jitter 50ms. Apenas `DbUpdateConcurrencyException` | Conflito de concorrência otimista (`xmin`) |
| **Consumer Outbox/Inbox** | EF Core inbox table para deduplicação | Mensagem duplicada (redelivery + retry) |

**Após exaustão de todas as camadas:** mensagem vai para `<queue>_error` (dead letter queue padrão do MassTransit).

### 4.2 Producer Outbox (Transactions API)

| Propriedade | Valor |
|---|---|
| `QueryDelay` | 100ms (polling do outbox) |
| `DuplicateDetectionWindow` | 30 minutos |
| Lock provider | PostgreSQL advisory locks (`UsePostgres()`) |

**Garantia:** Se o serviço crashar entre o `SaveChanges` e o publish para RabbitMQ, a mensagem sobrevive no `transactions.OutboxMessage` e é entregue no restart.

### 4.3 Health Checks

| Endpoint | Tipo | O que verifica |
|---|---|---|
| `/health` | Readiness | Todos os checks registrados (DB connectivity via `SELECT 1`) |
| `/alive` | Liveness | Apenas self-check (processo vivo) |

**Gap conhecido:** Nenhum health check verifica conectividade com RabbitMQ. O broker pode estar down e `/health` ainda retorna Healthy.

### 4.4 YARP Gateway Health Monitoring

| Tipo | Intervalo | Comportamento |
|---|---|---|
| Active | 10s (timeout 5s) | Polls `/health` dos upstreams; marca unhealthy se falhar |
| Passive | `TransportFailureRate` | Marca unhealthy por falhas de transporte; reativa após 2 min |

### 4.5 HTTP Client Resilience (Standard Handler)

Aplicado globalmente a todos os `HttpClient` via `AddStandardResilienceHandler()`:

| Layer | Default |
|---|---|
| Rate limiter | 1000 concurrent requests |
| Total timeout | 30s |
| Retry | 3× exponential (2s, 4s, 8s) em 408/429/5xx |
| Circuit breaker | 10% failure rate, 30s break |
| Attempt timeout | 10s por tentativa |

### 4.6 Concorrência Otimista

`DailySummary` usa `xmin` (PostgreSQL system column) como row version. Conflitos geram `DbUpdateConcurrencyException`, capturada pela camada de retry do MassTransit. O particionamento por `{MerchantId}:{Date}` (8 partições) minimiza conflitos serializando mensagens do mesmo merchant/data.

---

## 5. Runbooks por Cenário de Falha

### 5.1 PostgreSQL — Banco Indisponível

**Alerta:** `cashflow-healthcheck-failure` (Sev 1) + `cashflow-http-5xx-rate` (Sev 2)

**Impacto:** Todas as operações de escrita e leitura falham nos serviços afetados. Transactions API continua aceitando requests, mas retorna HTTP 500. Consumer abre circuit breaker após 15% de falha.

**Diagnóstico:**

```bash
# 1. Verificar status do PostgreSQL Flexible Server
az postgres flexible-server show \
  --name <server-name> \
  --resource-group rg-<env-name> \
  --query "{state:state, fullyQualifiedDomainName:fullyQualifiedDomainName}"

# 2. Verificar conectividade a partir do Container App
az containerapp exec \
  --name transactions \
  --resource-group rg-<env-name> \
  --command "pg_isready -h <hostname>"

# 3. Verificar métricas do servidor
az monitor metrics list \
  --resource <postgres-resource-id> \
  --metrics cpu_percent memory_percent active_connections storage_percent \
  --interval PT1M
```

**Recuperação:**

1. **Storage cheio:** Aumentar storage via Portal ou CLI (`az postgres flexible-server update --storage-size <GB>`). O PostgreSQL para de aceitar escritas quando o storage atinge 100%.
2. **CPU/Memória:** Escalar o SKU temporariamente (`az postgres flexible-server update --sku-name Standard_D4s_v3`).
3. **Falha de zona:** Se HA Zone Redundant estiver habilitado, failover automático ocorre em ~60-120s. Se não, aguardar recovery da plataforma (~5-15 min).
4. **Servidor irrecuperável:** Executar restore PITR (vide [Runbook 5.2](#52-postgresql--corrupção-de-dados--restore-pitr)).

**Pós-incidente:**
- Mensagens no outbox (`transactions.OutboxMessage`) serão entregues automaticamente quando o DB voltar.
- Consumer MassTransit reconecta automaticamente quando o circuit breaker fecha (após 5 min de reset).
- Output cache do Consolidation será repopulado automaticamente nas próximas requests.

---

### 5.2 PostgreSQL — Corrupção de Dados / Restore PITR

**Alerta:** Geralmente detectado por anomalias nos dados, não por alertas automáticos.

**Impacto:** Dados inconsistentes. Pode afetar saldo consolidado, transactions ou identidade.

**Procedimento de Restore:**

```bash
# 1. Identificar o ponto no tempo ANTES da corrupção
# Usar Application Insights para encontrar o timestamp do último estado bom:
az monitor app-insights query \
  --app <app-insights-name> \
  --analytics-query "customMetrics | where name == 'cashflow.transactions.created' | summarize count() by bin(timestamp, 1m) | order by timestamp desc | take 60"

# 2. Criar novo servidor a partir de PITR
az postgres flexible-server restore \
  --resource-group rg-<env-name> \
  --name <new-server-name> \
  --source-server /subscriptions/<sub-id>/resourceGroups/rg-<env-name>/providers/Microsoft.DBforPostgreSQL/flexibleServers/<original-server-name> \
  --restore-point-in-time "2026-03-06T10:00:00Z"

# 3. Validar dados no servidor restaurado
psql "host=<new-server>.postgres.database.azure.com dbname=transactions-db" \
  -c "SELECT COUNT(*) FROM transactions.transactions;"
psql "host=<new-server>.postgres.database.azure.com dbname=consolidation-db" \
  -c "SELECT date, total_credits, total_debits, transaction_count FROM consolidation.daily_summary ORDER BY date DESC LIMIT 10;"

# 4. Comparar contagens com o servidor original (se acessível)
# Se os dados no restored server estão corretos, migrar o DNS/connection string.

# 5. Atualizar connection strings nos Container Apps
# Opção A: atualizar o Bicep e fazer azd deploy
# Opção B: atualizar via CLI (mais rápido para emergência)
az containerapp update \
  --name transactions \
  --resource-group rg-<env-name> \
  --set-env-vars "ConnectionStrings__transactions-db=Host=<new-server>.postgres.database.azure.com;..."

# 6. Reiniciar todos os serviços para reconectar
az containerapp revision restart \
  --name transactions --resource-group rg-<env-name> --revision <active-revision>
az containerapp revision restart \
  --name consolidation --resource-group rg-<env-name> --revision <active-revision>
az containerapp revision restart \
  --name identity --resource-group rg-<env-name> --revision <active-revision>
```

**RTO estimado:** 2-4 horas (dependendo do tamanho do banco e validação).

---

### 5.3 RabbitMQ — Broker Indisponível

**Alerta:** `cashflow-consistency-delta` (Sev 1) — delta entre transactions criadas e eventos processados cresce.

**Impacto:**
- **Transactions API:** Continua aceitando e persistindo transactions normalmente. Mensagens ficam no `OutboxMessage` table (PostgreSQL). **Nenhuma perda de dados.**
- **Consolidation API:** Para de processar eventos. Saldos consolidados ficam desatualizados (eventual consistency gap cresce).
- **Usuários:** Podem criar transactions, mas consultas de consolidação retornam dados defasados.

**Diagnóstico:**

```bash
# 1. Verificar Container App do RabbitMQ
az containerapp show \
  --name messaging \
  --resource-group rg-<env-name> \
  --query "{runningStatus:properties.runningStatus, latestRevision:properties.latestReadyRevisionName}"

# 2. Verificar logs do container
az containerapp logs show \
  --name messaging \
  --resource-group rg-<env-name> \
  --tail 100

# 3. Verificar volume (Azure File Share)
az storage share show \
  --name <share-name> \
  --account-name <storage-account> \
  --query "{quota:properties.quota, lastModified:properties.lastModified}"

# 4. Contar mensagens acumuladas no outbox
# (via Application Insights ou query direta no DB)
az monitor app-insights query \
  --app <app-insights-name> \
  --analytics-query "customMetrics | where name == 'cashflow.transactions.created' | summarize sum(valueSum) by bin(timestamp, 5m) | order by timestamp desc | take 12"
```

**Recuperação:**

1. **Container crashloop:** Reiniciar a Container App.
   ```bash
   az containerapp revision restart \
     --name messaging --resource-group rg-<env-name> --revision <active-revision>
   ```

2. **Volume corrompido:** Recriar o container (o RabbitMQ repopula a partir dos publishers).
   ```bash
   # Deletar e recriar via azd
   azd deploy --no-prompt
   ```

3. **Após recovery do broker:**
   - O Bus Outbox Delivery Service (background service no Transactions API) detecta automaticamente que o broker voltou e começa a entregar mensagens acumuladas no `OutboxMessage`.
   - O consumer MassTransit reconecta automaticamente.
   - O gap de consistência se fecha gradualmente conforme mensagens acumuladas são processadas.

**Nota:** Mensagens que já estavam no RabbitMQ (queue) e não no outbox podem ser perdidas se o volume for irrecuperável. O outbox garante que todas as novas mensagens têm durabilidade no PostgreSQL.

---

### 5.4 Container App — Serviço em CrashLoop

**Alerta:** `cashflow-healthcheck-failure` (Sev 1)

**Diagnóstico:**

```bash
# 1. Listar revisões e seus status
az containerapp revision list \
  --name <service-name> \
  --resource-group rg-<env-name> \
  --query "[].{name:name, active:properties.active, runningState:properties.runningState, createdTime:properties.createdTime}" \
  -o table

# 2. Ver logs da revisão com crash
az containerapp logs show \
  --name <service-name> \
  --resource-group rg-<env-name> \
  --tail 200

# 3. Verificar se é problema de configuração/secrets
az containerapp show \
  --name <service-name> \
  --resource-group rg-<env-name> \
  --query "properties.template.containers[0].env[].name" -o tsv
```

**Recuperação:**

1. **Bug no código:** Rollback para revisão anterior (vide [Runbook 5.5](#55-deploy-falhado--rollback-de-revisão)).
2. **Dependência indisponível (DB, broker):** Resolver a dependência primeiro. O container reinicia automaticamente (`restartPolicy: Always`).
3. **Out of memory / CPU:** Ajustar resources no Bicep ou temporariamente via CLI:
   ```bash
   az containerapp update \
     --name <service-name> \
     --resource-group rg-<env-name> \
     --cpu 1.0 --memory 2.0Gi
   ```
4. **Secret incorreto/expirado:** Atualizar via CLI e reiniciar:
   ```bash
   az containerapp secret set \
     --name <service-name> \
     --resource-group rg-<env-name> \
     --secrets "jwt--signingkey=<new-value>"
   az containerapp revision restart \
     --name <service-name> --resource-group rg-<env-name> --revision <revision>
   ```

---

### 5.5 Deploy Falhado — Rollback de Revisão

**Alerta:** `cashflow-healthcheck-failure` (Sev 1) + `cashflow-http-5xx-rate` (Sev 2) — imediatamente após um deploy.

**Contexto:** Todos os Container Apps usam `activeRevisionsMode: 'Single'`. Cada deploy cria uma nova revisão e roteia 100% do tráfego para ela imediatamente. Não há blue/green automático.

**Rollback — Opção A: Re-deploy do commit anterior (recomendado)**

```bash
# 1. Identificar o último commit bom
git log --oneline -10

# 2. Fazer checkout e re-deploy
git checkout <last-good-commit>
azd deploy --no-prompt

# 3. Voltar para main
git checkout main
```

**Rollback — Opção B: Reativar revisão anterior via CLI (mais rápido)**

```bash
# 1. Listar revisões
az containerapp revision list \
  --name <service-name> \
  --resource-group rg-<env-name> \
  -o table

# 2. Mudar para Multiple revision mode (necessário para traffic splitting)
az containerapp revision set-mode \
  --name <service-name> \
  --resource-group rg-<env-name> \
  --mode multiple

# 3. Rotear 100% do tráfego para a revisão anterior
az containerapp ingress traffic set \
  --name <service-name> \
  --resource-group rg-<env-name> \
  --revision-weight <old-revision>=100 <bad-revision>=0

# 4. (Após estabilizar) Voltar para Single revision mode
az containerapp revision set-mode \
  --name <service-name> \
  --resource-group rg-<env-name> \
  --mode single
```

**Rollback de infraestrutura Bicep:** Não há `azd rollback`. Reverter mudanças no Bicep via git e re-executar `azd provision`.

---

### 5.6 Mensagens em Dead Letter Queue

**Alerta:** `cashflow-dlq-depth` (Sev 2)

**Contexto:** Mensagens chegam na DLQ (`<queue>_error`) após exaustão de todo o pipeline de resiliência: 5 retries exponenciais × 3 redeliveries (5min, 15min, 1h). Isso significa que a mensagem falhou por **~1h20min** de tentativas.

**Diagnóstico:**

```bash
# 1. Verificar a DLQ no RabbitMQ Management UI
# Em dev local: http://localhost:15672 (guest/guest)
# Em produção: port-forward via az containerapp exec ou Azure Portal console

# 2. Identificar a mensagem e o erro via Application Insights
az monitor app-insights query \
  --app <app-insights-name> \
  --analytics-query "
    exceptions
    | where cloud_RoleName == 'consolidation'
    | where timestamp > ago(2h)
    | summarize count() by type, outerMessage
    | order by count_ desc
  "
```

**Recuperação:**

1. **Corrigir a causa raiz** (bug no consumer, schema mismatch, etc.)
2. **Reprocessar mensagens da DLQ:**
   ```bash
   # Via RabbitMQ Management API: mover mensagens de _error de volta para a fila principal
   # No RabbitMQ Management UI: Queue > _error > Move messages > destination queue
   ```
3. **Se a mensagem é irrecuperável:** Mover para uma fila de arquivo morto e documentar. Verificar se o `DailySummary` precisa de correção manual.

**Validação pós-reprocessamento:**

```sql
-- Verificar se o saldo consolidado está correto para o merchant/data afetado
SELECT date, total_credits, total_debits, transaction_count
FROM consolidation.daily_summary
WHERE merchant_id = '<affected-merchant>'
  AND date = '<affected-date>';

-- Comparar com a soma real das transactions
SELECT
  SUM(CASE WHEN type = 'Credit' THEN amount ELSE 0 END) as expected_credits,
  SUM(CASE WHEN type = 'Debit' THEN amount ELSE 0 END) as expected_debits,
  COUNT(*) as expected_count
FROM transactions.transactions
WHERE merchant_id = '<affected-merchant>'
  AND reference_date = '<affected-date>';
```

---

### 5.7 Consistência Eventual Degradada

**Alerta:** `cashflow-consistency-delta` (Sev 1) — delta > 100 eventos por 10+ min.

**Contexto:** O sistema é event-driven (CQRS). O Consolidation API é um read model derivado do Transactions API. Algum atraso é esperado; o alerta dispara quando o gap cresce além do normal.

**Causas prováveis:**

| Causa | Diagnóstico | Ação |
|---|---|---|
| Consumer parado | Verificar logs do Consolidation API | Reiniciar container |
| Circuit breaker aberto | Buscar "circuit breaker" nos logs | Resolver causa raiz do downstream |
| RabbitMQ lento | Verificar métricas do messaging container | Escalar ou reiniciar |
| PostgreSQL lento | Verificar CPU/connections do DB | Escalar SKU |
| Backlog de outbox | Verificar `OutboxMessage` count no DB | Aguardar drain; aumentar `MessageDeliveryLimit` se necessário |

**Recuperação:**
1. Resolver a causa raiz (ver runbooks específicos acima).
2. O backlog se resolve automaticamente — o consumer processa mensagens acumuladas assim que a causa é removida.
3. Monitorar o delta via KQL:
   ```
   let created = customMetrics | where name == "cashflow.transactions.created" | summarize Created = sum(valueSum) by bin(timestamp, 5m);
   let processed = customMetrics | where name == "cashflow.consolidation.events_processed" | summarize Processed = sum(valueSum) by bin(timestamp, 5m);
   created | join kind=leftouter processed on timestamp | extend Delta = Created - coalesce(Processed, 0) | render timechart
   ```

---

### 5.8 Gateway Indisponível (Perda Total de Ingress)

**Alerta:** Nenhum request chegando ao Application Insights + reclamações de usuários.

**Impacto:** Perda total de acesso ao sistema. Nenhuma API funciona.

**Diagnóstico:**

```bash
# 1. Verificar o Gateway Container App
az containerapp show \
  --name gateway \
  --resource-group rg-<env-name> \
  --query "{fqdn:properties.configuration.ingress.fqdn, runningStatus:properties.runningStatus}"

# 2. Verificar DNS resolution
nslookup <gateway-fqdn>

# 3. Testar health do gateway diretamente
curl -v https://<gateway-fqdn>/alive
```

**Recuperação:**
1. **Container crashloop:** Verificar logs, rollback se necessário (vide [5.5](#55-deploy-falhado--rollback-de-revisão)).
2. **Ingress misconfigured:** Verificar configuração via CLI e corrigir.
3. **Container Apps Environment down:** Verificar status da plataforma no [Azure Status](https://status.azure.com/).

---

### 5.9 Perda de Região Azure (Disaster Regional)

**Impacto:** Perda total de todos os serviços. RTO alvo: < 4 horas.

**Pré-requisitos (devem estar habilitados antes):**
- [ ] `geoRedundantBackup: 'Enabled'` no PostgreSQL Flexible Server
- [ ] ACR com geo-replicação (`Premium` SKU)
- [ ] Documentação de `azd` e infra parametrizável por região

**Procedimento:**

1. **Ativar provisioning na região secundária:**
   ```bash
   # Atualizar variáveis de ambiente do azd
   azd env set AZURE_LOCATION "<secondary-region>"
   azd provision --no-prompt
   ```

2. **Restaurar PostgreSQL a partir do geo-backup:**
   ```bash
   az postgres flexible-server geo-restore \
     --resource-group rg-<env-name>-dr \
     --name <server-name>-dr \
     --source-server /subscriptions/<sub-id>/resourceGroups/rg-<env-name>/providers/Microsoft.DBforPostgreSQL/flexibleServers/<original-server-name> \
     --location <secondary-region>
   ```

3. **Deploy dos serviços:**
   ```bash
   azd deploy --no-prompt
   ```

4. **Atualizar DNS / Traffic Manager** para apontar para o novo gateway.

5. **Validar:** Executar smoke tests manuais (criar transaction, verificar consolidation).

**Nota:** Este procedimento requer preparação prévia. Ver seção [9. Gaps Conhecidos](#9-gaps-conhecidos-e-roadmap).

---

## 6. Alertas e Detecção

### Alertas Configurados (Azure Monitor Scheduled Query Rules)

| Alerta | Sev | Trigger | Ação |
|---|---|---|---|
| `cashflow-healthcheck-failure` | **1** | Serviços com health check falhando em janelas de 5 min (2 de 5 janelas avaliadas) | Email |
| `cashflow-consistency-delta` | **1** | Delta created vs processed > 100 por 10+ min | Email |
| `cashflow-dlq-depth` | **2** | DLQ depth > 0 em qualquer janela | Email |
| `cashflow-consolidation-latency-p95` | **2** | p95 > 200ms por 5+ min consecutivos | Email |
| `cashflow-ingestion-rate-low` | **2** | < 50 events/s por 3 de 5 janelas | Email |
| `cashflow-eventual-consistency-p95` | **2** | p95 > 5000ms por 5+ min consecutivos | Email |
| `cashflow-http-5xx-rate` | **2** | 5xx rate > 5% (min 10 req/min) por 5+ min | Email |
| `cashflow-consolidation-throughput-low` | **3** | < 50 req/s por 3 de 5 janelas | Log only (sem email) |

### Gaps de Alertas

| Gap | Impacto | Recomendação |
|---|---|---|
| Sem alerta de CPU/storage do PostgreSQL | Pode ficar sem disco sem aviso | Adicionar alert rule para `storage_percent > 80%` |
| Sem alerta de replica count dos Container Apps | Não detecta scale failure | Adicionar alert para `replicaCount < 1` |
| Sem alerta de restart do RabbitMQ | Perda silenciosa de estado do broker | Adicionar alert para container restart count |
| Alertas condicionais (`if alert_email_address`) | Deploy sem email = sem alertas | Tornar email obrigatório ou usar fallback |
| Canal único (email) | Resposta lenta fora de horário | Integrar PagerDuty/webhook para Sev-1 |

---

## 7. Escalation Matrix

| Severidade | Primeiro Contato | Escalação (se não resolvido em) | Comunicação |
|---|---|---|---|
| **SEV-1** | Engenheiro on-call | Líder técnico em 30 min; CTO em 1 hora | Canal de incidentes (Slack/Teams) atualizado a cada 15 min |
| **SEV-2** | Engenheiro on-call | Líder técnico em 2 horas | Canal de incidentes atualizado a cada 30 min |
| **SEV-3** | Engenheiro designado | Líder técnico em 1 dia útil | Ticket no backlog |
| **SEV-4** | Próxima sprint planning | — | Registro no backlog |

**Contatos (preencher conforme a equipe):**

| Papel | Nome | Contato |
|---|---|---|
| On-call primário | _A definir_ | _A definir_ |
| Líder técnico | _A definir_ | _A definir_ |
| DBA / Infraestrutura | _A definir_ | _A definir_ |
| CTO | _A definir_ | _A definir_ |

---

## 8. Teste de Restore (Procedimento Mensal)

> **Um backup não testado não é um backup.**

### Checklist Mensal

- [ ] **Data do teste:** ____
- [ ] **Executado por:** ____

### Passos

1. **Provisionar servidor PostgreSQL Flexible Server isolado:**
   ```bash
   az postgres flexible-server restore \
     --resource-group rg-<env-name>-restore-test \
     --name cashflow-restore-test-$(date +%Y%m%d) \
     --source-server <production-server-name> \
     --restore-point-in-time "$(date -u -v-1d '+%Y-%m-%dT%H:%M:%SZ')"
   ```

2. **Validar integridade dos dados:**
   ```sql
   -- Contagem de transactions
   SELECT COUNT(*) FROM transactions.transactions;

   -- Verificar saldos de referência (comparar com produção)
   SELECT merchant_id, date, total_credits, total_debits, transaction_count
   FROM consolidation.daily_summary
   WHERE date >= CURRENT_DATE - INTERVAL '7 days'
   ORDER BY merchant_id, date;

   -- Verificar integridade do outbox (devem ser 0 ou poucas mensagens pendentes)
   SELECT COUNT(*) FROM transactions."OutboxMessage" WHERE delivered IS NULL;
   ```

3. **Medir o RTO real:**
   - Timestamp de início do restore: ____
   - Timestamp de dados disponíveis: ____
   - **RTO medido:** ____

4. **Limpar recursos de teste:**
   ```bash
   az group delete --name rg-<env-name>-restore-test --yes --no-wait
   ```

5. **Documentar resultado:**
   - RTO medido vs. meta (< 4 horas): ____
   - Dados íntegros: Sim / Não
   - Observações: ____

---

## 9. Gaps Conhecidos e Roadmap

### Infraestrutura (Bicep)

| Gap | Risco | Ação | Prioridade | Status |
|---|---|---|---|---|
| SKU `Standard_B1ms` (Burstable) | Throttling sob carga, sem SLA de HA. HA Zone Redundant **requer** GeneralPurpose ou MemoryOptimized | Migrar para `Standard_D2s_v3+` e habilitar `highAvailabilityMode: 'ZoneRedundant'` no Bicep | **P1** | Aberto |
| `AllowAllAzureIps` firewall rule | Qualquer serviço Azure pode acessar o DB | Private endpoint + VNet injection | **P2** | Aberto |
| RabbitMQ single-replica em LRS | SPOF na mensageria | Migrar para Azure Service Bus ou ZRS | **P2** | Aberto |
| ACR Basic (sem geo-replicação) | Image pull falha se região cair | Upgrade para Premium | **P2** | Aberto |
| Container Apps sem health probes Bicep | Plataforma não detecta unhealthy via /health e /alive | Adicionar probes nos módulos Bicep | **P1** | Aberto |
| Sem VNet injection | Comunicação multitenant | Configurar VNet dedicada | **P2** | Aberto |

#### Resolvido no Bicep (postgres.module.bicep)

| Item | Antes | Depois |
|---|---|---|
| `backupRetentionDays` | 7 | **35** (parametrizável) |
| `geoRedundantBackup` | `Disabled` | **`Enabled`** (parametrizável) |
| `autoGrow` | Não configurado | **`Enabled`** |

> **Nota:** `highAvailabilityMode` permanece como `Disabled` por default pois requer SKU GeneralPurpose/MemoryOptimized. Ao migrar para produção com SKU adequado, sobrescrever no `main.bicep` com `highAvailabilityMode: 'ZoneRedundant'`.

### Aplicação

| Gap | Risco | Ação | Prioridade |
|---|---|---|---|
| Sem health check de RabbitMQ | Broker down não detectado por `/health` | Adicionar MassTransit health check | **P1** |
| Sem `EnableRetryOnFailure` no EF Core | Transient DB errors → HTTP 500 | Configurar retry no Npgsql | **P2** |
| `activeRevisionsMode: 'Single'` | Deploy sem zero-downtime | Mudar para `Multiple` com traffic splitting | **P2** |
| Sem scaling rules nos Container Apps | Não escala sob carga | Adicionar HTTP/queue-depth triggers | **P2** |

### Operacional

| Gap | Risco | Ação | Prioridade |
|---|---|---|---|
| Alertas apenas por email | Resposta lenta fora de horário | Integrar PagerDuty/OpsGenie | **P1** |
| Sem rotação de on-call | Burnout, single point of failure humano | Definir escala de plantão | **P1** |
| Log Analytics retenção padrão (30 dias) | Perda de logs para auditoria | Configurar 90+ dias | **P2** |
| Sem teste de DR automatizado | Validação manual, propensa a erro | Script de teste mensal no CI | **P3** |

---

> **Referência:** Para a arquitetura detalhada, ADRs e diagramas C4, consulte [`architecture.md`](architecture.md) e [`adr/`](adr/).
