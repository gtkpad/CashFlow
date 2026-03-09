# ADR-003: Banco de Dados — PostgreSQL com Databases Separados por Serviço

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | CQRS sugere stores separados para reads e writes. A questão é: PostgreSQL + PostgreSQL, PostgreSQL + Redis, ou outra combinação? |
| **Decisão** | **PostgreSQL** para ambos os lados (write e read), com **databases separados por serviço** (`transactions-db`, `consolidation-db`, `identity-db`) dentro do mesmo servidor PostgreSQL Flexible Server. |

## Detalhes

O Aspire `postgres.AddDatabase("transactions-db")` cria um **database separado** dentro do servidor PostgreSQL — não um schema dentro do mesmo database. A modelagem física usa schemas nomeados (`transactions`, `consolidation`) para organização dentro de cada database dedicado. As migrations EF Core são geradas e aplicadas independentemente.

### Por que não Redis para o Consolidation

| Critério | PostgreSQL | Redis |
|---|---|---|
| 50 req/s de leitura | ~5.000 q/s single node (100x margem) | ~100k q/s (2.000x margem — overkill) |
| Durabilidade | ACID nativo, WAL, crash recovery | In-memory — AOF tem gap de ~1s de perda |
| Complexidade operacional | 1 tech stack (já usado para writes) | +1 tech stack, persistência poliglota |
| Transações ACID para Inbox | Nativo | Requer Lua scripts ou MULTI/EXEC |
| Consultas analíticas | `SELECT ... WHERE date BETWEEN` com índice | Requer scan de chaves ou sorted sets |

PostgreSQL é suficiente para 50 req/s com folga de 100x. Redis seria justificado apenas como **cache layer** à frente do PostgreSQL a partir de ~1.000 req/s.

### Modelagem Física

**Schema `transactions` (write-optimized, append-only):**

| Coluna | Tipo | Observação |
|---|---|---|
| `id` | UUID PK | |
| `merchant_id` | UUID | Índice — filtro por tenant |
| `reference_date` | DATE | Índice — filtro por data |
| `type` | SMALLINT | 1=Credit, 2=Debit |
| `value_amount` | DECIMAL(18,2) | |
| `value_currency` | VARCHAR(3) | Default 'BRL' |
| `description` | VARCHAR(500) | |
| `created_at` | TIMESTAMPTZ | |
| `created_by` | VARCHAR(128) | |

Tabelas `OutboxMessage` e `OutboxState` do Bus Outbox gerenciadas pelo MassTransit via migrations.

**Schema `consolidation` (read-optimized):**

| Coluna | Tipo | Observação |
|---|---|---|
| `id` | UUID PK | |
| `merchant_id` | UUID | Índice único composto `(merchant_id, date)` |
| `date` | DATE | Índice único composto `(merchant_id, date)` |
| `total_credits_amount` | DECIMAL(18,2) | |
| `total_debits_amount` | DECIMAL(18,2) | |
| `transaction_count` | INT | |
| `updated_at` | TIMESTAMPTZ | |

Concorrência via `xmin` (shadow property no EF Core). Tabelas `InboxState`, `OutboxMessage` e `OutboxState` gerenciadas pelo MassTransit via migrations.

## Trade-offs

| Critério | PostgreSQL para ambos | PostgreSQL + Redis |
|---|---|---|
| Simplicidade operacional | 1 tech stack | 2 tech stacks |
| Suficiente para 50 req/s | Sim (100x margem) | Overkill |
| ACID para Inbox/Outbox | Nativo | Workarounds necessários |
| Custo | Zero adicional | Container extra |

## Nota: Divergência de Configuração entre APIs

A Consolidation API usa `AddAzureNpgsqlDbContext<>` (com DbContext pooling), otimizando para leituras de alta frequência. A Transactions API, por sua vez, usa `AddDbContext<>` **sem pooling**, porque o `DomainEventInterceptor` é registrado como serviço scoped e injetado via `OnConfiguring` — o que é incompatível com DbContext pooling (`AddDbContextPool`/`AddAzureNpgsqlDbContext`), que exige contextos sem estado scoped.

Essa divergência é **intencional**: o write-side precisa do interceptor para capturar domain events na mesma transação ACID (Bus Outbox pattern), enquanto o read-side não publica eventos e pode usar pooling livremente.

## Consequências

- Único tech stack de banco reduz complexidade operacional.
- Migrations EF Core de cada serviço são completamente independentes.
- A Transactions API não se beneficia de DbContext pooling — trade-off aceito em favor do `DomainEventInterceptor` para publicação atômica de eventos.
- Se leituras ultrapassarem ~1.000 req/s, adicionar Redis como cache layer à frente do PostgreSQL (Output Cache com Redis IOutputCacheStore).
