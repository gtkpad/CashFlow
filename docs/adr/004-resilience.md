# ADR-004: Resiliência — MassTransit Retry, Npgsql e HttpClient (Polly v8)

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | A comunicação assíncrona (MassTransit/RabbitMQ) e as conexões com PostgreSQL precisam de proteção contra falhas transitórias. Não existem chamadas HTTP síncronas entre serviços de domínio — toda comunicação inter-serviço é via RabbitMQ. A estratégia de resiliência foca nos canais reais: mensageria, banco de dados e YARP → backends. |
| **Decisão** | (1) MassTransit `UseMessageRetry` com backoff exponencial no consumer (configurado na `ConsumerDefinition`). (2) Reconexão automática ao RabbitMQ via `AutoRecover` nativo do MassTransit. (3) Retry policy do Npgsql para falhas transitórias de banco. (4) `AddStandardResilienceHandler()` nos HttpClients globais via `ServiceDefaults`. |

## Detalhes

### Por Canal de Comunicação

| Canal | Mecanismo | Cobertura |
|---|---|---|
| Mensageria (MassTransit consumer) | `UseMessageRetry` exponencial 5× (100ms → 30s, jitter 50ms) | `DbUpdateConcurrencyException` — conflitos de concorrência otimista |
| Mensageria (exaustão de retries) | Error queue (`*_error`) + `Fault<T>` consumer | Falhas persistentes — veja [ADR-007](007-dlq.md) |
| Mensageria (circuit breaker) | `UseCircuitBreaker` (15% trip, 5min reset) | Falha sustentada do downstream (DB down) |
| RabbitMQ (conexão) | MassTransit `AutoRecover` | Reconexão automática após queda do broker |
| Banco (Npgsql) | Connection string retry policy (`Retry Max Count=3`) | Falhas de conexão transitórias |
| HTTP (YARP → backends) | `AddStandardResilienceHandler()` (Polly v8) | Retry + Circuit Breaker + Timeout |

### Pipeline do Consumer MassTransit

A `TransactionCreatedConsumerDefinition` configura middlewares na ordem LIFO (último registrado = mais externo na execução):

1. `UsePartitioner(8)` — mais externo: roteia por `{MerchantId}:{ReferenceDate}`, elimina conflitos de concorrência entre consumers paralelos. Ver [ADR-005](005-concurrency.md).
2. `UseCircuitBreaker` — protege contra cascata de falhas.
3. `UseMessageRetry` — retry exponencial para casos residuais.
4. `UseEntityFrameworkOutbox` — mais interno: gerencia a TX que envolve o consumer.

### HttpClient (AddStandardResilienceHandler)

Aplicado globalmente via `ServiceDefaults`:

| Layer | Configuração |
|---|---|
| Rate limiter | 1.000 requisições concorrentes |
| Total timeout | 30s |
| Retry | 3× exponencial (2s, 4s, 8s) em 408/429/5xx |
| Circuit breaker | 10% failure rate, 30s break |
| Attempt timeout | 10s por tentativa |

## Consequências

- Toda resiliência de mensageria é declarativa via `ConsumerDefinition` — sem `try/catch` manual.
- HttpClient resilience é global via `ServiceDefaults`, aplicado automaticamente a todos os serviços.
- Sem chamadas HTTP síncronas entre serviços de domínio, o foco de resiliência HTTP é apenas YARP → backends.
