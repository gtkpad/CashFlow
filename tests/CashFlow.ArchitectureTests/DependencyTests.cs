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

    [Fact]
    public void TransactionsCreateFeature_ShouldNotDependOn_GetFeature()
    {
        var assembly = typeof(CashFlow.Transactions.API.Persistence.TransactionsDbContext).Assembly;

        var result = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace("CashFlow.Transactions.API.Features.CreateTransaction")
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Transactions.API.Features.GetTransaction")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ConsolidationConsumerFeature_ShouldNotDependOn_QueryFeature()
    {
        var assembly = typeof(CashFlow.Consolidation.API.Persistence.ConsolidationDbContext).Assembly;

        var result = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace("CashFlow.Consolidation.API.Features.TransactionCreated")
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Consolidation.API.Features.GetDailyBalance")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Consumers_ShouldNotDependOn_Endpoints()
    {
        var consolidationAssembly = typeof(CashFlow.Consolidation.API.Persistence.ConsolidationDbContext).Assembly;

        var result = Types.InAssembly(consolidationAssembly)
            .That()
            .ResideInNamespace("CashFlow.Consolidation.API.Features.TransactionCreated")
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Consolidation.API.Features.GetDailyBalance")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();

        var transactionsAssembly = typeof(CashFlow.Transactions.API.Persistence.TransactionsDbContext).Assembly;

        var result2 = Types.InAssembly(transactionsAssembly)
            .That()
            .ResideInNamespace("CashFlow.Transactions.API.Features.GetTransaction")
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Transactions.API.Features.CreateTransaction")
            .GetResult();

        result2.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Handlers_ShouldNotDependOn_MassTransitConsumers()
    {
        var transactionsAssembly = typeof(CashFlow.Transactions.API.Persistence.TransactionsDbContext).Assembly;

        var result = Types.InAssembly(transactionsAssembly)
            .That()
            .ResideInNamespace("CashFlow.Transactions.API.Features.CreateTransaction")
            .ShouldNot()
            .HaveDependencyOn("MassTransit")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
