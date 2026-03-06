# Architecture Decision Records (ADRs)

Decisões arquiteturais documentadas para o sistema CashFlow.

> **Referência:** Para a visão geral da arquitetura, diagramas C4 e contexto completo, consulte [`docs/architecture.md`](../architecture.md).

## Índice

| ADR | Decisão | Status |
|---|---|---|
| [ADR-001](001-topology.md) | Topologia: Serviços Independentes com API Gateway (vs. Monolito Modular) | Aceito |
| [ADR-002](002-messaging.md) | Mensageria: RabbitMQ + MassTransit Bus Outbox + Consumer Inbox | Aceito |
| [ADR-003](003-database.md) | Banco de Dados: PostgreSQL com databases separados por serviço | Aceito |
| [ADR-004](004-resilience.md) | Resiliência: MassTransit Retry, Npgsql e HttpClient Polly v8 | Aceito |
| [ADR-005](005-concurrency.md) | Concorrência: Append-Only Writes + Optimistic Concurrency (xmin) + Particionamento | Aceito |
| [ADR-006](006-gateway-auth.md) | API Gateway e Autenticação: YARP + ASP.NET Core Identity (Auth Offloading) | Aceito |
| [ADR-007](007-dlq.md) | Dead Letter Queue: Topologia de redelivery e recuperação operacional | Aceito |
| [ADR-008](008-gateway-ha.md) | Alta Disponibilidade: Azure Container Apps + .NET Aspire | Aceito |
| [ADR-009](009-e2e-testing.md) | Testes E2E com .NET Aspire Testing (DistributedApplicationTestingBuilder) | Aceito |
| [ADR-010](010-di-handlers.md) | Handlers via DI Direto (sem MediatR/Mediator) | Aceito |

## Formato

Cada ADR segue o formato:

```markdown
# ADR-NNN: Título

| Campo | Valor |
|---|---|
| **Status** | Aceito / Proposto / Substituído |
| **Data** | ... |
| **Contexto** | ... |
| **Decisão** | ... |

## Detalhes
[conteúdo técnico, diagramas, configurações]

## Trade-offs
[tabela comparativa]

## Consequências
[impactos e thresholds de migração]
```
