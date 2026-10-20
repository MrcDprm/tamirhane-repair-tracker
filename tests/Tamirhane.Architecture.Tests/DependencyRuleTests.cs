using System.Reflection;
using NetArchTest.Rules;
using Tamirhane.Application;
using Tamirhane.Domain.Common;

namespace Tamirhane.Architecture.Tests;

/// <summary>
/// Clean Architecture'ın bağımlılık kuralı: bağımlılıklar yalnızca içe doğru akar.
/// Domain hiçbir şeye, Application sadece Domain'e bağlıdır; dış dünya (EF Core, ASP.NET) en dış katmanlardadır.
/// Kural bozulursa test, kuralı bozan tipleri adıyla yazar.
/// </summary>
public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(Entity).Assembly;
    private static readonly Assembly Application = typeof(DependencyInjection).Assembly;
    private static readonly Assembly Web = typeof(Program).Assembly;

    private const string DomainNs = "Tamirhane.Domain";
    private const string ApplicationNs = "Tamirhane.Application";
    private const string InfrastructureNs = "Tamirhane.Infrastructure";
    private const string WebNs = "Tamirhane.Web";
    private const string EfCore = "Microsoft.EntityFrameworkCore";
    private const string AspNetCore = "Microsoft.AspNetCore";

    [Fact]
    public void Domain_depends_on_no_other_layer_or_framework()
    {
        AssertNoDependency(Types.InAssembly(Domain),
            ApplicationNs, InfrastructureNs, WebNs, EfCore, AspNetCore, "Microsoft.Extensions");
    }

    [Fact]
    public void Domain_assembly_references_only_the_runtime()
    {
        var references = Domain.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        var foreign = references.Where(n => !(n.StartsWith("System") || n is "netstandard" or "mscorlib")).ToList();

        Assert.True(foreign.Count == 0, $"Domain yalnızca .NET'e bağlı olmalı; bulunan: {string.Join(", ", foreign)}");
    }

    [Fact]
    public void Application_depends_only_on_domain()
    {
        AssertNoDependency(Types.InAssembly(Application),
            InfrastructureNs, WebNs, EfCore, AspNetCore);
    }

    [Fact]
    public void Controllers_do_not_use_infrastructure_directly()
    {
        // Web, Infrastructure'ı sadece Program.cs'te servis kaydı için tanır (composition root).
        AssertNoDependency(Types.InAssembly(Web).That().ResideInNamespace($"{WebNs}.Controllers"),
            InfrastructureNs, EfCore);
    }

    [Fact]
    public void Domain_entities_cannot_be_changed_from_outside()
    {
        // Durum sadece domain metotlarıyla değişir: dışarıdan atanabilen özellik olmamalı.
        var publicSetters = Domain.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Entity)))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.SetMethod is { IsPublic: true } || p.SetMethod is { IsAssembly: true })
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.True(publicSetters.Count == 0, $"Dışarıdan atanabilen özellikler: {string.Join(", ", publicSetters)}");
    }

    private static void AssertNoDependency(PredicateList types, params string[] forbidden) =>
        AssertNoDependency(types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult(), forbidden);

    private static void AssertNoDependency(Types types, params string[] forbidden) =>
        AssertNoDependency(types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult(), forbidden);

    private static void AssertNoDependency(TestResult result, string[] forbidden)
    {
        var offenders = result.FailingTypeNames ?? [];
        Assert.True(result.IsSuccessful,
            $"Şunlara bağımlı olmamalı: {string.Join(", ", forbidden)}. Kuralı bozan tipler: {string.Join(", ", offenders)}");
    }
}
