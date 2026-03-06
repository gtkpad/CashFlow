namespace CashFlow.Domain.Consolidation;

/// <summary>
/// Transaction type within the Consolidation bounded context.
/// Intentionally duplicated from <c>CashFlow.Domain.Transactions.TransactionType</c>
/// to maintain bounded context isolation (see ADR-001). Mapping between contexts
/// occurs at the integration event layer (anti-corruption pattern).
/// </summary>
public enum TransactionType
{
    Credit = 1,
    Debit = 2
}
