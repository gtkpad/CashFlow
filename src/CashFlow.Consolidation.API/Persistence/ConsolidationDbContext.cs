using CashFlow.Domain.Consolidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.API.Persistence;

public sealed class ConsolidationDbContext(DbContextOptions<ConsolidationDbContext> options)
    : DbContext(options)
{
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("consolidation");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsolidationDbContext).Assembly);

        // MassTransit Consumer Outbox tables (Inbox + Outbox)
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
