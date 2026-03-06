# ADR-009: Estratégia de Testes E2E com .NET Aspire

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | O sistema possui três camadas de testes automatizados: unitários (domínio isolado), integração (WebApplicationFactory + Testcontainers) e arquitetura (NetArchTest). Os testes de integração com `WebApplicationFactory` testam uma única API em isolamento — eles sobem o servidor HTTP de um único projeto, substituem serviços por mocks ou containers Testcontainers, e validam a fatia vertical individualmente. Esse modelo é rápido e preciso para testar comportamento de um serviço, mas tem um gap estrutural: **não exercita a comunicação assíncrona entre serviços via RabbitMQ em condições reais**. O `CashFlow.AppHost` já orquestra exatamente essa topologia para desenvolvimento local. O .NET Aspire oferece a API `DistributedApplicationTestingBuilder` (pacote `Aspire.Hosting.Testing`) que reutiliza o `AppHost` como motor de testes E2E. |
| **Decisão** | Criar o projeto `CashFlow.E2ETests` usando `DistributedApplicationTestingBuilder` referenciando `CashFlow.AppHost`. Os testes E2E são responsáveis por validar **fluxos de negócio completos** que cruzam fronteiras de serviço — especialmente os que dependem de consistência eventual via MassTransit + RabbitMQ. Testes de comportamento dentro de um único serviço continuam em `CashFlow.IntegrationTests`. |

## Detalhes

### Diferença Fundamental entre Integration Tests e E2E Tests

| Dimensão | `CashFlow.IntegrationTests` | `CashFlow.E2ETests` |
|---|---|---|
| **Motor de boot** | `WebApplicationFactory<Program>` | `DistributedApplicationTestingBuilder` |
| **Escopo** | Uma única API em isolamento | Todos os 4 processos + PostgreSQL + RabbitMQ |
| **Infraestrutura** | Testcontainers (DB real, RabbitMQ real, mas levantados ad-hoc) | Aspire AppHost (mesma topologia do ambiente de desenvolvimento) |
| **Comunicação entre serviços** | Mockada ou não testada | Real (HTTP via Gateway + AMQP via RabbitMQ) |
| **Consistência eventual** | Não testada diretamente | Testada com polling + timeout explícito |
| **Velocidade de execução** | Rápida (~5-30s/suite) | Lenta (~60-120s/suite — boot de toda a topologia) |
| **Uso recomendado** | CI em cada commit/PR | CI em merge para `main` ou staging gate |
| **Granularidade de falha** | Alta — falha aponta para o serviço exato | Média — falha pode ser em qualquer nó da cadeia |

### Quando Usar Cada Abordagem

- **Use `IntegrationTests`** para: validar regras de negócio de uma API, testar o handler de um command/query, verificar que o endpoint retorna 401 sem token, testar o consumer MassTransit de forma isolada.
- **Use `E2ETests`** para: validar que o fluxo completo `POST /api/v1/transactions` → RabbitMQ → Consumer → `GET /api/v1/consolidation/{date}` produz o resultado esperado; validar que o YARP roteia corretamente e injeta `X-User-Id`; smoke tests antes de deploy em staging.

### Fixture Compartilhada — Boot único por suíte de testes

O boot do Aspire AppHost leva entre 30 e 90 segundos. Para evitar reboots entre cada teste, a fixture `CashFlowAppFixture` compartilha uma única instância do `DistributedApplication` por suite, usando `IAsyncLifetime` do xUnit. Os timeouts são calibrados para a topologia completa: `StartupTimeout = 5 minutos`, `DefaultTimeout = 30 segundos`, `EventualConsistencyTimeout = 15 segundos`.

### Helpers de Autenticação

O `AuthHelper` é um utilitário estático que registra um merchant de teste (idempotente) e obtém um Bearer Token via Gateway. O token é cacheado na sessão e reutilizado entre testes da mesma suite, protegido por `SemaphoreSlim` para thread safety.

### Teste E2E — Fluxo Completo: Criar Transaction → Verificar Consolidation

**Fluxo validado:**
1. `POST /api/v1/transactions` (Gateway → Transactions.API) → INSERT transaction + INSERT OutboxMessage (ACID TX)
2. Delivery Service faz poll (QueryDelay: 100ms) → Publish ao RabbitMQ
3. `TransactionCreatedConsumer` recebe → InboxState check → `ApplyTransaction` → COMMIT
4. `GET /api/v1/consolidation/{date}` (Gateway → Consolidation.API) → Retorna balance atualizado

### Teste E2E — Isolamento de Falhas

Valida o NFR-1 em nível de sistema: demonstra que a indisponibilidade do consolidation não impede a criação de transactions.

### Configuração de Timeout para Consistência Eventual

O `EventualConsistencyTimeout` de 15 segundos é calculado com base no pior cenário:

```
Componentes da latência end-to-end (pior caso):
  QueryDelay do Bus Outbox           = 100ms  (configurado em ADR-002)
  Processamento do consumer          =  30ms  (baseline sem contenção — ADR-005)
  Latência de rede Docker/Aspire     =  10ms
  ─────────────────────────────────────────────
  Latência máxima esperada           = 140ms

Margem de segurança para testes CI   = ~71x  (10s / 140ms)
```

A margem de 71x é deliberadamente conservadora para absorver cold start de container PostgreSQL no CI, variações de scheduler do Docker em ambientes CI com recursos limitados, e polling do teste a cada 500ms.

### Execução no Pipeline CI/CD

Os testes E2E rodam via GitHub Actions apenas em merge para `main` ou em PRs com label `run-e2e`. O workflow usa `ubuntu-latest` (Docker pré-instalado), configura .NET 10, e executa os testes com `xUnit.MaxParallelThreads=1`. O timeout do job é 15 minutos e os resultados `.trx` são armazenados como artefatos.

## Trade-offs

| Aspecto | Valor |
|---|---|
| **Cobertura** | Máxima — testa o fluxo real com todos os componentes |
| **Velocidade** | Lenta — boot do AppHost leva 60-120s; recomendado apenas em gates de merge |
| **Confiabilidade** | Alta — testa o comportamento exato de produção, incluindo consistência eventual |
| **Manutenção** | Moderada — mudanças no AppHost quebram os testes E2E diretamente |
| **Diagnóstico de falhas** | Excelente — Aspire Dashboard disponível durante execução local |
| **Paralelismo** | Não recomendado — testes compartilham o mesmo banco; `MaxParallelThreads=1` |
| **Custo CI** | Moderado — requer Docker no runner; usar caching de imagens Docker |
| **Quando quebra** | Qualquer mudança de contrato entre serviços é detectada |

## Consequências

- Os testes E2E dependem diretamente do `CashFlow.AppHost`: mudanças nos nomes de recursos Aspire devem ser refletidas nos helpers da fixture.
- O `EventualConsistencyTimeout` de 15s pressupõe `QueryDelay = 100ms` configurado no Bus Outbox ([ADR-002](002-messaging.md)). Se o valor padrão de 1000ms for mantido, aumentar para 20s.
- Testes E2E não substituem testes de integração — eles são complementares.
- Ao adicionar novos serviços ao `AppHost`, adicionar o respectivo `WaitForResourceHealthyAsync` na fixture.
