# ADR-013: Query-Side com Leitura Otimizada (AsNoTracking)

| Campo | Valor |
|---|---|
| **Status** | Aceito |
| **Data** | Março 2026 |
| **Última revisão** | Março 2026 |
| **Contexto** | O sistema implementa CQRS com separação clara entre command-side (Transactions API) e query-side (Consolidation API, `GetTransactionHandler`). No command-side, handlers usam `ITransactionRepository` para operações que envolvem invariantes de domínio. A questão é como otimizar query handlers para leitura. |
| **Decisão** | Query handlers usam Repository com métodos de leitura otimizados (`AsNoTracking()`), sem change tracking nem Unit of Work. Command handlers continuam usando `ITransactionRepository` (e `IUnitOfWork` para persistência) para proteger invariantes de domínio. |

## Detalhes

### Assimetria intencional no CQRS

| Aspecto | Command-Side | Query-Side |
|---|---|---|
| Objetivo | Proteger invariantes de domínio | Otimizar performance de leitura |
| Acesso a dados | Repository + Unit of Work | Repository com `AsNoTracking()` (sem Unit of Work) |
| Change Tracking | Necessário (domain events, outbox) | Desnecessário (`AsNoTracking()`) |
| Projeção | Aggregate Root completo | DTO específico da query |

### Abordagem: Repository com métodos de leitura dedicados

Os repositórios expõem métodos de leitura otimizados que internamente usam `AsNoTracking()`:

1. **Separação clara** — métodos de leitura (sem tracking) vs escrita (com tracking) no mesmo repositório.
2. **Performance** — `AsNoTracking()` elimina overhead do change tracker, sem perda mensurável pela indireção do repositório.
3. **Testabilidade** — query handlers são testáveis via mock de interface (unit tests) e via Testcontainers (integration tests).
4. **Consistência** — command e query handlers usam a mesma abstração (`IRepository`), mas com métodos semanticamente distintos.

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
public sealed class GetTransactionHandler(ITransactionRepository repository)
{
    var transaction = await repository.GetByIdAndMerchantAsync(txId, mId, ct);
    // Repository internamente usa AsNoTracking()
}
```

**Repository — métodos de leitura vs escrita** (`DailySummaryRepository`):
```csharp
// Leitura com tracking (consumer precisa do change tracker para SaveChangesAsync)
public Task<DailySummary?> GetByDateAndMerchantAsync(...)
    => db.DailySummaries.FirstOrDefaultAsync(...);

// Leitura sem tracking (query handler — read-only)
public Task<DailySummary?> FindByDateAndMerchantAsync(...)
    => db.DailySummaries.AsNoTracking().FirstOrDefaultAsync(...);
```

## Trade-offs

| Dimensão | Repository com AsNoTracking | DbContext Direto |
|---|---|---|
| Performance | Excelente — `AsNoTracking()` interno | Máxima — zero indireção |
| Testabilidade | Unit tests via mock + integration tests | Apenas integration tests |
| Troca de ORM | Facilita (interface oculta o ORM) | Exige modificar query handlers |
| Consistência | Simétrico com command-side | Assimétrico |
| Simplicidade | Boa — métodos de leitura semânticos | Alta — LINQ nativo |

## Consequências

- Query handlers dependem de interface de repositório — substituir o ORM não exige modificar os handlers.
- A separação entre métodos com e sem tracking no repositório é **intencional**: consumers usam tracking (precisam do `SaveChangesAsync`), query handlers usam `AsNoTracking()`.
- Novas queries devem seguir o padrão: injetar repositório, usar método de leitura sem tracking, projetar para DTO.
- Se o query-side migrar para um read-store separado (Redis, Elasticsearch), os repositórios abstraem a mudança.
