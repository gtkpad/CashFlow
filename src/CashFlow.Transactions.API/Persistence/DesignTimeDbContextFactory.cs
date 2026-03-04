using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CashFlow.Transactions.API.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TransactionsDbContext>
{
    public TransactionsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TransactionsDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=transactions;Username=postgres;Password=postgres");
        return new TransactionsDbContext(optionsBuilder.Options);
    }
}
