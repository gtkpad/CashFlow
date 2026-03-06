# ADR-003: Banco de Dados — PostgreSQL Unificado com Schemas Separados

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | CQRS sugere stores separados para reads e writes. A questão é: PostgreSQL + PostgreSQL, PostgreSQL + Redis, ou outra combinação? |
| **Decisão** | **PostgreSQL** para ambos os lados (write e read), com **bancos separados por serviço** (`transactions-db`, `consolidation-db`, `identity-db`) dentro do mesmo servidor PostgreSQL. Cada banco usa seu próprio schema principal (`transactions`, `consolidation`) — o isolamento é em nível de banco (database), não apenas de schema, o que garante que as migrations EF Core de cada serviço sejam completamente independentes. |

## Detalhes

> **Nota sobre isolamento de bancos vs. schemas**: O Aspire `postgres.AddDatabase("transactions-db")` cria um **banco (database) separado** dentro do servidor PostgreSQL — não um schema dentro do mesmo banco. A modelagem física usa schemas nomeados (`transactions.transaction`, `consolidation.daily_summary`) para organização dentro de cada banco dedicado. As migrations EF Core são geradas e aplicadas independentemente em cada banco.

### Por que NÃO Redis para o consolidation?

| Critério | PostgreSQL ✅ | Redis ❌ (para este cenário) |
|---|---|---|
| 50 req/s de leitura | ~5000 q/s single node (**100x margem**) | ~100k q/s (**2000x margem** — overkill) |
| Durabilidade | ACID nativo, WAL, crash recovery | In-memory — AOF tem gap de ~1s de perda potencial |
| Complexidade operacional | 1 tech stack para operar | +1 tech stack (persistência poliglota) |
| Custo operacional | 0 (já usa PostgreSQL para writes) | Container adicional + configuração AOF/RDB |
| Transações ACID para Inbox | Nativo | Requer Lua scripts ou MULTI/EXEC |
| Consultas analíticas (relatório por período) | `SELECT ... WHERE date BETWEEN` com índice | Requer scan de chaves ou sorted sets |

**Decisão**: PostgreSQL é mais que suficiente para 50 req/s e oferece durabilidade, transações e queries analíticas nativas. Redis seria justificado apenas a partir de ~1000 req/s no consolidation — nesse caso, como **cache** à frente do PostgreSQL (não substituto).

> **Se fosse usar Redis**: Seria como cache layer com `IOutputCacheStore` do ASP.NET Core, mantendo PostgreSQL como source of truth. Mas para 50 req/s, `[OutputCache(Duration = 5)]` no endpoint já é suficiente.

### Modelagem Física

**Schema `transactions`** (otimizado para writes — append-only): Tabela `transaction` com colunas `id` (UUID PK), `reference_date` (DATE), `type` (SMALLINT: 1=Credit, 2=Debit), `value_amount` (DECIMAL 18,2), `value_currency` (VARCHAR 3, default 'BRL'), `description` (VARCHAR 500), `created_at` (TIMESTAMPTZ), `created_by` (VARCHAR 128) e `merchant_id` (UUID, isolamento por tenant). Índice em `reference_date`. Concurrency token usa `xmin` nativo do PostgreSQL. Tabelas `OutboxMessage` e `OutboxState` do Bus Outbox gerenciadas pelo MassTransit via migrations.

**Schema `consolidation`** (otimizado para reads): Tabela `daily_summary` com colunas `id` (UUID PK), `merchant_id` (UUID), `date` (DATE), `total_credits_amount` (DECIMAL 18,2), `total_debits_amount` (DECIMAL 18,2), `transaction_count` (INT) e `updated_at` (TIMESTAMPTZ). Índice único composto em `(merchant_id, date)`. Concurrency via `xmin` (shadow property no EF Core). Tabelas `InboxState`, `OutboxMessage` e `OutboxState` do Consumer Outbox gerenciadas pelo MassTransit via migrations.

> **Nota**: As tabelas `OutboxMessage`, `OutboxState` e `InboxState` do MassTransit são criadas automaticamente via `modelBuilder.AddOutboxMessageEntity()`, `AddOutboxStateEntity()` e `AddInboxStateEntity()`. Não é necessário DDL manual.

## Trade-offs

| Critério | PostgreSQL para ambos ✅ | PostgreSQL + Redis |
|---|---|---|
| Simplicidade operacional | 1 tech stack | 2 tech stacks |
| Suficiente para 50 req/s | Sim (100x margem) | Overkill |
| ACID para Inbox/Outbox | Nativo | Requer workarounds |
| Custo | Zero adicional | Container extra |

## Consequências

- Único tech stack de banco reduz complexidade operacional.
- Se leituras ultrapassarem ~1000 req/s, adicionar Redis como cache layer à frente do PostgreSQL.
