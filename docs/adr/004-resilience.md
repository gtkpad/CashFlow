# ADR-004: Resiliência — MassTransit Retry, Npgsql e HttpClient (Polly v8)

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | A comunicação assíncrona (MassTransit/RabbitMQ) e as conexões com PostgreSQL precisam de proteção contra falhas transitórias. O YARP também precisa de circuit breaker para os serviços backend. A arquitetura não possui chamadas HTTP síncronas entre serviços de domínio — toda comunicação inter-serviço é via RabbitMQ. Portanto, a estratégia de resiliência foca nos canais reais: mensageria, banco de dados e o YARP. |
| **Decisão** | **(1)** MassTransit `UseMessageRetry` com backoff exponencial no consumer (declarado na `ConsumerDefinition` — já documentado no [ADR-002](002-messaging.md)). **(2)** Reconexão automática ao RabbitMQ via configuração `AutoRecover` nativa do MassTransit. **(3)** Retry policy do Npgsql para falhas transitórias de banco, configurada via connection string. **(4)** `AddStandardResilienceHandler()` nos HttpClients globais via `ServiceDefaults` — usado pelo YARP para os backends internos e pelos health checks. |

## Detalhes

A configuração de resiliência é centralizada no `ServiceDefaults` via `AddStandardResilienceHandler()` nos HttpClients globais (usado pelo YARP para backends internos), com: retry exponencial (3 tentativas, 500ms de delay), circuit breaker (failure ratio 50%, minimum throughput 10, timeout de 5s por tentativa). O Npgsql usa retry policy configurada na connection string (`Retry Max Count=3; Retry Connection Timeout=5`).

> **Nota**: O retry do consumer MassTransit (`UseMessageRetry`) está configurado na `TransactionCreatedConsumerDefinition` ([ADR-002](002-messaging.md)). Após esgotar retries, mensagens são movidas para a error queue — ver [ADR-007](007-dlq.md).

## Trade-offs

| Canal | Mecanismo | Cobertura |
|---|---|---|
| Mensageria (MassTransit) | `UseMessageRetry` + error queue (DLQ) | Falhas transitórias + persistentes |
| Banco (Npgsql) | Connection string retry policy | Falhas de conexão transitórias |
| HTTP (YARP → backends) | `AddStandardResilienceHandler()` (Polly v8) | Retry + Circuit Breaker + Timeout |
| RabbitMQ (conexão) | MassTransit `AutoRecover` | Reconexão automática |

## Consequências

- Toda resiliência de mensageria é declarativa via `ConsumerDefinition` — sem `try/catch` manual.
- HttpClient resilience é global via `ServiceDefaults`, aplicado automaticamente a todos os serviços.
- Sem chamadas HTTP síncronas entre serviços de domínio, o foco de resiliência HTTP é apenas YARP → backends.
