using CashFlow.Consolidation.API.Persistence;
using CashFlow.Domain.SharedKernel;
using CashFlow.ServiceDefaults;
using CashFlow.Transactions.API.Persistence;
using FluentAssertions;
using NetArchTest.Rules;

namespace CashFlow.ArchitectureTests;

public class DependencyTests
{
    [Fact]
    public void Domain_ShouldNotDependOn_TransactionsApi()
    {
        var domainAssembly = typeof(Money).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Transactions.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_ShouldNotDependOn_ConsolidationApi()
    {
        var domainAssembly = typeof(Money).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Consolidation.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_ShouldNotDependOn_IdentityApi()
    {
        var domainAssembly = typeof(Money).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Identity.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void TransactionsApi_ShouldNotDependOn_ConsolidationApi()
    {
        var transactionsAssembly = typeof(TransactionsDbContext).Assembly;

        var result = Types.InAssembly(transactionsAssembly)
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Consolidation.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ConsolidationApi_ShouldNotDependOn_TransactionsApi()
    {
        var consolidationAssembly = typeof(ConsolidationDbContext).Assembly;

        var result = Types.InAssembly(consolidationAssembly)
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Transactions.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void TransactionsCreateFeature_ShouldNotDependOn_GetFeature()
    {
        var assembly = typeof(TransactionsDbContext).Assembly;

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
        var assembly = typeof(ConsolidationDbContext).Assembly;

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
        var transactionsAssembly = typeof(TransactionsDbContext).Assembly;

        var result = Types.InAssembly(transactionsAssembly)
            .That()
            .ResideInNamespace("CashFlow.Transactions.API.Features.GetTransaction")
            .ShouldNot()
            .HaveDependencyOn("CashFlow.Transactions.API.Features.CreateTransaction")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Handlers_ShouldNotDependOn_MassTransitConsumers()
    {
        var transactionsAssembly = typeof(TransactionsDbContext).Assembly;

        var result = Types.InAssembly(transactionsAssembly)
            .That()
            .ResideInNamespace("CashFlow.Transactions.API.Features.CreateTransaction")
            .ShouldNot()
            .HaveDependencyOn("MassTransit")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ServiceDefaults_ShouldNotDependOn_AnyApiAssembly()
    {
        var assembly = typeof(GlobalExceptionHandler).Assembly;
        foreach (var ns in new[]
                 {
                     "CashFlow.Transactions.API", "CashFlow.Consolidation.API",
                     "CashFlow.Identity.API", "CashFlow.Gateway"
                 })
        {
            Types.InAssembly(assembly).ShouldNot().HaveDependencyOn(ns).GetResult()
                .IsSuccessful.Should().BeTrue($"ServiceDefaults must not depend on {ns}");
        }
    }

    [Fact]
    public void Validators_ShouldHaveValidatorSuffix()
    {
        var assembly = typeof(TransactionsDbContext).Assembly;
        Types.InAssembly(assembly)
            .That().HaveNameEndingWith("Validator")
            .Should().ResideInNamespaceContaining("Features")
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Handlers_ShouldResideInFeaturesNamespace()
    {
        foreach (var assembly in new[]
                 {
                     typeof(TransactionsDbContext).Assembly,
                     typeof(ConsolidationDbContext).Assembly
                 })
        {
            Types.InAssembly(assembly)
                .That().HaveNameEndingWith("Handler")
                .Should().ResideInNamespaceContaining("Features")
                .GetResult().IsSuccessful.Should().BeTrue();
        }
    }

    [Fact]
    public void Consumers_ShouldResideInFeaturesNamespace()
    {
        foreach (var assembly in new[]
                 {
                     typeof(TransactionsDbContext).Assembly,
                     typeof(ConsolidationDbContext).Assembly
                 })
        {
            Types.InAssembly(assembly)
                .That().HaveNameEndingWith("Consumer")
                .Should().ResideInNamespaceContaining("Features")
                .GetResult().IsSuccessful.Should().BeTrue();
        }
    }

    [Fact]
    public void Repositories_ShouldResideInPersistenceNamespace()
    {
        foreach (var assembly in new[]
                 {
                     typeof(TransactionsDbContext).Assembly,
                     typeof(ConsolidationDbContext).Assembly
                 })
        {
            Types.InAssembly(assembly)
                .That().HaveNameEndingWith("Repository")
                .Should().ResideInNamespaceContaining("Persistence")
                .GetResult().IsSuccessful.Should().BeTrue();
        }
    }

    [Fact]
    public void Handlers_ShouldNotDependOn_DbContext()
    {
        foreach (var assembly in new[]
                 {
                     typeof(TransactionsDbContext).Assembly,
                     typeof(ConsolidationDbContext).Assembly
                 })
        {
            var result = Types.InAssembly(assembly)
                .That().ResideInNamespaceContaining("Features")
                .And().HaveNameEndingWith("Handler")
                .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Handlers in {assembly.GetName().Name} must not depend on DbContext — use repositories");
        }
    }
}
