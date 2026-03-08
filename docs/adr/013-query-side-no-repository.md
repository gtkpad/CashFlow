# ADR-013: Query-Side com Acesso Direto ao DbContext (sem Repository)

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | O sistema implementa CQRS com separação clara entre command-side (Transactions API) e query-side (Consolidation API, `GetTransactionHandler`). No command-side, handlers utilizam `ITransactionRepository` para operações de domínio que envolvem invariantes (criação, persistência). No query-side, os handlers `GetTransactionHandler` e `GetDailyBalanceHandler` acessam `DbContext` diretamente com `AsNoTracking()` e compiled queries. A questão é se query handlers deveriam usar abstrações de repositório. |
| **Decisão** | Query handlers acessam `DbContext` diretamente, sem abstração de repositório. Command handlers continuam usando `ITransactionRepository` (e `IUnitOfWork` para persistência) para proteger invariantes de domínio. |

## Detalhes

### Justificativa CQRS

Em arquiteturas CQRS, reads e writes têm requisitos fundamentalmente diferentes:

| Aspecto | Command-Side | Query-Side |
|---|---|---|
| **Objetivo** | Proteger invariantes de domínio | Otimizar performance de leitura |
| **Acesso a dados** | Via Repository + Unit of Work | Via DbContext direto |
| **Change Tracking** | Necessário (para outbox, domain events) | Desnecessário (`AsNoTracking()`) |
| **Projeção** | Aggregate Root completo | DTO específico da query |
| **Abstração** | `ITransactionRepository`, `IUnitOfWork` | Nenhuma — LINQ direto |

### Padrão no Código

**Command handler** (`CreateTransactionHandler`):
```csharp
public class CreateTransactionHandler(
    ITransactionRepository repository,
    IUnitOfWork unitOfWork, ...)
{
    await repository.AddAsync(transaction, ct);
    await unitOfWork.SaveChangesAsync(ct);
}
```

**Query handler** (`GetTransactionHandler`):
```csharp
public sealed class GetTransactionHandler(TransactionsDbContext db)
{
    var transaction = await db.Transactions
        .AsNoTracking()
        .FirstOrDefaultAsync(t => t.Id == txId && t.MerchantId == mId, ct);
}
```

### Por Que Não Usar Repository no Query-Side

1. **Repositories protegem invariantes** — queries não modificam estado, portanto não há invariantes a proteger.
2. **Performance** — Acesso direto permite `AsNoTracking()`, compiled queries (`EF.CompileAsyncQuery`), e projeções LINQ sem overhead de abstração.
3. **YAGNI** — Um `ITransactionQueryRepository` seria um wrapper 1:1 sobre o DbContext, adicionando indireção sem valor.
4. **Testabilidade** — Query handlers são testados com `InMemoryDatabase` ou integration tests com Testcontainers, validando a query real.

## Trade-offs

| Dimensão | DbContext Direto ✅ | Repository para Queries ❌ |
|---|---|---|
| **Performance** | Máxima — zero indireção | Overhead de abstração |
| **Simplicidade** | Alta — LINQ nativo | Wrapper desnecessário |
| **Testabilidade** | InMemory/Testcontainers | Mock de interface |
| **Flexibilidade** | Dependência de EF Core | Troca de ORM facilitada |
| **Consistência** | Assimétrico (intencional) | Simétrico |

## Consequências

- Query handlers dependem de `DbContext` concreto — substituir o ORM exige modificar os handlers.
- A assimetria entre command e query é **intencional** e documentada, não um atalho acidental.
- Novas queries devem seguir o mesmo padrão: `DbContext` + `AsNoTracking()` + projeção para DTO.
- Se o query-side migrar para um read-store separado (ex: Redis, Elasticsearch), esta decisão será substituída por uma nova ADR.
