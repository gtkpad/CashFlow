using FluentAssertions;
using NetArchTest.Rules;

namespace CashFlow.ArchitectureTests;

public class DependencyTests
{
    [Fact]
    public void Domain_ShouldNotDependOn_TransactionsApi()
    {
        var domainAssembly = typeof(CashFlow.Domain.SharedKernel.Money).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Transactions.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_ShouldNotDependOn_ConsolidationApi()
    {
        var domainAssembly = typeof(CashFlow.Domain.SharedKernel.Money).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Consolidation.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_ShouldNotDependOn_IdentityApi()
    {
        var domainAssembly = typeof(CashFlow.Domain.SharedKernel.Money).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Identity.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void TransactionsApi_ShouldNotDependOn_ConsolidationApi()
    {
        var transactionsAssembly = typeof(CashFlow.Transactions.API.Persistence.TransactionsDbContext).Assembly;

        var result = Types.InAssembly(transactionsAssembly)
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Consolidation.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ConsolidationApi_ShouldNotDependOn_TransactionsApi()
    {
        var consolidationAssembly = typeof(CashFlow.Consolidation.API.Persistence.ConsolidationDbContext).Assembly;

        var result = Types.InAssembly(consolidationAssembly)
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Transactions.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
