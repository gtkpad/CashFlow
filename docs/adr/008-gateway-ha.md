# ADR-008: Alta Disponibilidade do API Gateway via Azure Container Apps + .NET Aspire

| Campo | Valor |
|---|---|
| **Status** | Aceito (atualizado para ACA) |
| **Data** | Março 2026 |
| **Contexto** | O [ADR-001](001-topology.md) identifica o YARP como SPOF da topologia: uma única instância do Gateway derruba o acesso externo a todos os serviços, mesmo que Transactions e Consolidation estejam saudáveis. O runtime de produção é **Azure Container Apps (ACA)** e a orquestração local é **.NET Aspire (AppHost)**. O YARP é o único ponto de entrada — sem HA, é um SPOF mais crítico do que os problemas que a arquitetura resolve. O Azure Developer CLI (`azd`) lê o AppHost e provisiona a infraestrutura ACA automaticamente. |
| **Decisão** | O YARP roda como **Azure Container App com ingress externo e múltiplas réplicas**. O ACA gerencia automaticamente o balanceamento de tráfego, health checks e reinicializações. A eliminação do SPOF é delegada ao ACA — não requer Nginx, HAProxy ou gestão manual de Kubernetes. O `azd` provisiona toda a infraestrutura a partir do AppHost. |

## Detalhes

### Por que o ACA resolve o SPOF melhor do que alternativas

| Critério | Nginx/HAProxy | Kubernetes (self-managed) | Azure Container Apps ✅ |
|---|---|---|---|
| Gestão de réplicas | Manual | Automático (Deployment) | Automático (min/max replicas) |
| Restart em falha | Manual ou systemd | Automático (kubelet) | Automático (plataforma gerenciada) |
| Rolling update sem downtime | Complexo | Nativo (`RollingUpdate`) | Nativo (revisões automáticas) |
| Health check integrado | Configuração separada | Readiness/Liveness probes | Health probes nativas |
| TLS/certificados | Manual (Let's Encrypt, etc.) | cert-manager ou manual | Automático (managed TLS) |
| Integração com Aspire | Não | Via aspirate (community) | Nativo via `azd` (oficial) |
| Complexidade operacional | Alta | Média (requer cluster K8s) | Baixa (serverless gerenciado) |
| Custo operacional | Servidor dedicado | Cluster K8s ($$) | Pay-per-use (escala a zero possível) |

### Topologia no Azure Container Apps

```
Internet
  │
  ▼
ACA Environment (managed TLS + ingress)
  │
  ├── Gateway (YARP) ──── ingress: externo, min: 2, max: 5 replicas
  │     │
  │     ├──→ Identity API ──── ingress: interno
  │     ├──→ Transactions API ──── ingress: interno
  │     └──→ Consolidation API ──── ingress: interno
  │
  ├── PostgreSQL Flexible Server (managed, fora do ACA Environment)
  └── RabbitMQ ──── Container App interno (sem ingress externo)
```

O `azd` provisiona automaticamente:
- **Container Apps Environment** — rede isolada para todos os containers
- **Gateway** — Container App com `WithExternalHttpEndpoints()` → ingress externo
- **Identity, Transactions, Consolidation** — Container Apps com ingress interno
- **Azure Container Registry (ACR)** — para armazenar as imagens
- **PostgreSQL Flexible Server** — banco de dados gerenciado
- **RabbitMQ** — Container App interno para mensageria

### Configuração YARP: Active + Passive Health Checks

Os serviços backend possuem health checks configurados no YARP:

- **Active Health Check**: polling a cada 10 segundos no path `/health` com timeout de 5 segundos.
- **Passive Health Check**: policy `TransportFailureRate` que detecta falhas nas respostas reais sem polling adicional, removendo o destino do balanceamento até o período de reativação (2 minutos).

> **Nota**: Os endereços dos destinos usam o **Aspire Service Discovery** com prefixo `https+http://` (tenta HTTPS primeiro, HTTP como fallback). Em produção (ACA), resolvem via DNS interno do Container Apps Environment.

### Aspire AppHost — Ponte Dev/Prod

O .NET Aspire serve como ponte entre o ambiente de desenvolvimento local (Docker) e o ambiente de produção (ACA). No AppHost, o Gateway é declarado com `WithExternalHttpEndpoints()` e referencia os três serviços (Identity, Transactions, Consolidation). Em produção, o `azd` lê essa declaração e configura o Gateway Container App com ingress externo; serviços sem `WithExternalHttpEndpoints()` recebem ingress interno automaticamente.

### Deploy via Azure Developer CLI (`azd`)

O `azd` lê o AppHost e provisiona toda a infraestrutura automaticamente: `azd provision` gera Bicep in-memory a partir do AppHost e provisiona os recursos Azure; `azd deploy` builda as imagens via `dotnet publish /t:PublishContainer` e faz deploy para ACA. Não há Dockerfiles nem manifests manuais.

### Session Affinity vs. Stateless

O YARP suporta Session Affinity via cookie ou header, mas **não deve ser ativado** nesta arquitetura:

| Critério | Stateless (recomendado) | Session Affinity (não recomendado aqui) |
|---|---|---|
| Tolerância a falha de instância | Requisições redistribuídas automaticamente | Requisições afetadas perdem afinidade |
| Escala horizontal | Transparente | Requer sincronização de "stickiness" |
| Compatibilidade com Auth Offloading | Perfeita — cada instância valida o token independentemente | Desnecessária |
| Complexidade | Nenhuma | Adiciona estado ao load balancer |

### Cálculo de Disponibilidade

Assumindo disponibilidade de 99,9% por réplica com min 2 réplicas:

```
P(falha de 1 réplica) = 0,1% → probabilidade = 0,001
P(todas as réplicas cairem simultaneamente) = 0,001² = 1 × 10⁻⁶ → 0,0001%
Disponibilidade efetiva: ≥ 99,9999% (~6 noves)

O SPOF move-se para o Azure Container Apps Environment (plataforma
gerenciada pela Microsoft — SLA típico: 99,95%).
```

## Trade-offs

| Aspecto | Valor |
|---|---|
| **Elimina SPOF?** | Sim — ACA gerencia réplicas e reinicializações |
| **Complexidade adicionada?** | Mínima — ACA é serverless, sem gestão de cluster |
| **Rolling updates zero-downtime?** | Sim — revisões automáticas nativas |
| **TLS gerenciado?** | Sim — managed certificates automáticos |
| **Stateless (obrigatório)?** | Sim — YARP não mantém estado de sessão |
| **Custo?** | Pay-per-use — escala baseada em tráfego real |

## Consequências

- Os serviços backend devem expor `/health` (já configurado via `AddHealthChecks()` do .NET Aspire).
- O `AppHost` mantém a orquestração para desenvolvimento local (Docker).
- Em produção, o `azd` provisiona e deploya a partir do AppHost — sem manifests manuais.
- O YARP deve ser **stateless** (sem sticky sessions) — garantido pelo design atual.
- Não são necessários Dockerfiles — o `azd` usa `dotnet publish /t:PublishContainer`.
