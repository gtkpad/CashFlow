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
| [ADR-011](011-container-apps-scaling.md) | Auto-Scaling: HTTP Scaling Rules + Dimensionamento por Perfil de Carga | Aceito |
| [ADR-012](012-postgresql-scaling.md) | PostgreSQL Scaling: General Purpose SKU + PgBouncer Built-in | Aceito |
| [ADR-013](013-query-side-no-repository.md) | Query-Side com DbContext Direto (sem Repository) — CQRS | Aceito |
| [ADR-014](014-resource-authorization.md) | Autorização Baseada em Recurso via MerchantId (Tenant Isolation) | Aceito |
| [ADR-015](015-data-security.md) | Segurança de Dados: Encryption at Rest/Transit + Networking Roadmap | Aceito |
| [ADR-016](016-api-versioning.md) | API Versioning com Asp.Versioning + Política de Deprecation | Aceito |

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
