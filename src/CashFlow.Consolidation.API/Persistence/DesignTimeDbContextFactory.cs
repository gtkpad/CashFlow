using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CashFlow.Consolidation.API.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ConsolidationDbContext>
{
    public ConsolidationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConsolidationDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=consolidation;Username=postgres;Password=postgres");
        return new ConsolidationDbContext(optionsBuilder.Options);
    }
}
