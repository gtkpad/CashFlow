# ADR-013: Query-Side com DbContext Direto (sem Repository)

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Contexto** | O sistema implementa CQRS com separação clara entre command-side (Transactions API) e query-side (Consolidation API, `GetTransactionHandler`). No command-side, handlers usam `ITransactionRepository` para operações que envolvem invariantes de domínio. A questão é se query handlers também deveriam usar abstrações de repositório. |
| **Decisão** | Query handlers acessam `DbContext` diretamente com `AsNoTracking()`. Command handlers continuam usando `ITransactionRepository` (e `IUnitOfWork` para persistência) para proteger invariantes de domínio. |

## Detalhes

### Assimetria intencional no CQRS

| Aspecto | Command-Side | Query-Side |
|---|---|---|
| Objetivo | Proteger invariantes de domínio | Otimizar performance de leitura |
| Acesso a dados | Repository + Unit of Work | DbContext direto |
| Change Tracking | Necessário (domain events, outbox) | Desnecessário (`AsNoTracking()`) |
| Projeção | Aggregate Root completo | DTO específico da query |

### Por que não Repository no Query-Side

1. **Repositories protegem invariantes** — queries não modificam estado, logo não há invariantes a proteger.
2. **Performance** — `AsNoTracking()` + compiled queries sem overhead de abstração.
3. **YAGNI** — um `ITransactionQueryRepository` seria um wrapper 1:1 sobre o DbContext, adicionando indireção sem valor.
4. **Testabilidade** — query handlers são testados com integration tests via Testcontainers, validando a query real contra um PostgreSQL de verdade.

### Exemplo de contraste

**Command handler** (`CreateTransactionHandler`):
```csharp
public class CreateTransactionHandler(
    ITransactionRepository repository,
    IUnitOfWork unitOfWork)
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

## Trade-offs

| Dimensão | DbContext Direto | Repository para Queries |
|---|---|---|
| Performance | Máxima — zero indireção | Overhead de abstração |
| Simplicidade | Alta — LINQ nativo | Wrapper desnecessário |
| Testabilidade | Integration tests com Testcontainers | Mock de interface |
| Troca de ORM | Exige modificar os query handlers | Facilita (interface oculta o ORM) |
| Consistência | Assimétrico com command-side (intencional) | Simétrico |

## Consequências

- Query handlers dependem de `DbContext` concreto — substituir o ORM exige modificar os handlers.
- A assimetria entre command e query é **intencional** e documentada, não um atalho.
- Novas queries devem seguir o padrão: `DbContext` + `AsNoTracking()` + projeção para DTO.
- Se o query-side migrar para um read-store separado (Redis, Elasticsearch), esta ADR será substituída.
