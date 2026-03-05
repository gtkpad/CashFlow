using CashFlow.Domain.Transactions;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.API.Persistence;

public class TransactionsDbContext : DbContext
{
    private readonly DomainEventInterceptor? _interceptor;

    public TransactionsDbContext(DbContextOptions<TransactionsDbContext> options,
        DomainEventInterceptor? interceptor = null) : base(options)
        => _interceptor = interceptor;

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_interceptor is not null)
            optionsBuilder.AddInterceptors(_interceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("transactions");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransactionsDbContext).Assembly);

        // MassTransit Bus Outbox tables
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
