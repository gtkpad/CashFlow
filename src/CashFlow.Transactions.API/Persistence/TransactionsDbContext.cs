using CashFlow.Domain.SharedKernel;
using CashFlow.Domain.Transactions;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.API.Persistence;

public sealed class TransactionsDbContext(
    DbContextOptions<TransactionsDbContext> options,
    DomainEventInterceptor? domainEventInterceptor = null)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (domainEventInterceptor is not null)
            optionsBuilder.AddInterceptors(domainEventInterceptor);
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
