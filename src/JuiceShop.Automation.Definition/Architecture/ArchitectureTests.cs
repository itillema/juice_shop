using System.Reflection;
using JuiceShop.Automation.Adaptation.Contracts;
using JuiceShop.Automation.Definition.Flows;
using JuiceShop.Automation.Definition.Tests;
using JuiceShop.Automation.Execution.Testing;
using JuiceShop.Automation.Utility.Configuration;
using NetArchTest.Rules;
using NUnit.Framework;

namespace JuiceShop.Automation.Definition.Architecture;

/// <summary>
/// Executable specification of the gTAA layering.
/// </summary>
/// <remarks>
/// <para>
/// The point of these is that the architecture is <em>tested</em>, not merely described in a README
/// that drifts. They run in the same <c>dotnet test</c> invocation as the browser tests, need no SUT
/// and no browser, and finish in milliseconds.
/// </para>
/// <para>
/// Some of the layering is already enforced by the compiler — MSBuild rejects a circular
/// <c>ProjectReference</c> graph outright, and the Playwright page objects are <c>internal</c>, so
/// no test can reach a locator even deliberately. These cover the rules the compiler cannot see:
/// which layer is allowed to know about the automation technology, and whether a test script has
/// started calling the adapters directly instead of going through the business-action facade.
/// That second rule is ISTQB CTAL-TAE v2.0 §3.1.3's "no direct calls should be made to the core
/// libraries from test scripts", turned into a failing build.
/// </para>
/// </remarks>
[TestFixture]
[Category(TestCategories.Architecture)]
[Parallelizable(ParallelScope.All)]
public sealed class ArchitectureTests
{
    private const string PlaywrightNamespace = "Microsoft.Playwright";
    private const string AdaptationAssembly = "JuiceShop.Automation.Adaptation";
    private const string ExecutionAssembly = "JuiceShop.Automation.Execution";
    private const string DefinitionAssembly = "JuiceShop.Automation.Definition";
    private const string UtilityAssembly = "JuiceShop.Automation.Utility";

    private static Assembly Utility => typeof(AutomationSettings).Assembly;

    private static Assembly Adaptation => typeof(IBrowserSession).Assembly;

    private static Assembly Execution => typeof(E2ETestBase).Assembly;

    private static Assembly Definition => typeof(ShopFlow).Assembly;

    [Test]
    [Description("Only the Adaptation layer may know that the SUT is driven by Playwright.")]
    public void Only_Adaptation_Depends_On_Playwright()
    {
        AssertNoDependency(
            Types.InAssembly(Utility),
            PlaywrightNamespace,
            "The Utility layer must stay technology-neutral so it can be reused across SUTs.");

        AssertNoDependency(
            Types.InAssembly(Execution),
            PlaywrightNamespace,
            "The Execution layer captures artifacts through IBrowserSession precisely so that it " +
            "never needs a Playwright type. If this fails, that abstraction has been bypassed.");

        // This fixture is excluded from its own rule: it names the forbidden namespaces as string
        // data, and NetArchTest's IL scan cannot tell a constant naming a namespace apart from a
        // genuine reference to a type in it. Everything else in the assembly is still covered.
        AssertNoDependency(
            Types.InAssembly(Definition)
                .That()
                .DoNotResideInNamespace(typeof(ArchitectureTests).Namespace),
            PlaywrightNamespace,
            "Test cases and flows must express intent, not browser mechanics.");
    }

    [Test]
    [Description("Test scripts reach the SUT through the business-action facade, never directly.")]
    public void Test_Fixtures_Do_Not_Call_The_Adaptation_Layer_Directly()
    {
        AssertNoDependency(
            Types.InAssembly(Definition).That().ResideInNamespace(typeof(AuthenticationTests).Namespace),
            AdaptationAssembly,
            "A test reached past ShopFlow into the adapters. Add the behaviour to the flow facade " +
            "instead — that is the seam that keeps test cases readable and insulates them from " +
            "changes to the SUT's interface.");
    }

    [Test]
    [Description("Dependencies point one way: Definition -> Execution -> Adaptation -> Utility.")]
    public void Layer_Dependencies_Point_In_One_Direction()
    {
        AssertNoDependency(
            Types.InAssembly(Utility),
            AdaptationAssembly,
            "Utility is the bottom of the graph and must depend on no other layer.");

        AssertNoDependency(Types.InAssembly(Utility), ExecutionAssembly, "Utility must not depend on Execution.");
        AssertNoDependency(Types.InAssembly(Utility), DefinitionAssembly, "Utility must not depend on Definition.");

        AssertNoDependency(Types.InAssembly(Adaptation), ExecutionAssembly, "Adaptation must not depend on Execution.");
        AssertNoDependency(Types.InAssembly(Adaptation), DefinitionAssembly, "Adaptation must not depend on Definition.");

        AssertNoDependency(Types.InAssembly(Execution), DefinitionAssembly, "Execution must not depend on Definition.");
    }

    [Test]
    [Description("Page objects stay internal so no layer above can touch a locator.")]
    public void Page_Objects_Are_Not_Publicly_Visible()
    {
        var leaked = Types.InAssembly(Adaptation)
            .That()
            .ResideInNamespace($"{AdaptationAssembly}.Pages")
            .And()
            .ArePublic()
            .GetTypes()
            .ToList();

        Assert.That(
            leaked,
            Is.Empty,
            "Page objects must be internal. Making one public would let a test bypass the page " +
            "contract and depend on Playwright types directly.");
    }

    [Test]
    [Description("The Adaptation layer's public surface is free of Playwright types.")]
    public void Adaptation_Public_Api_Exposes_No_Playwright_Types()
    {
        var offenders = new List<string>();

        foreach (var type in Adaptation.GetExportedTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (IsPlaywrightType(method.ReturnType))
                {
                    offenders.Add($"{type.Name}.{method.Name} returns {method.ReturnType.Name}");
                }

                offenders.AddRange(
                    method.GetParameters()
                        .Where(parameter => IsPlaywrightType(parameter.ParameterType))
                        .Select(parameter => $"{type.Name}.{method.Name} accepts {parameter.ParameterType.Name}"));
            }

            offenders.AddRange(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(property => IsPlaywrightType(property.PropertyType))
                    .Select(property => $"{type.Name}.{property.Name} is {property.PropertyType.Name}"));
        }

        Assert.That(
            offenders,
            Is.Empty,
            "The Adaptation layer leaked a Playwright type through its public API. Everything above " +
            "this layer would then be coupled to the automation technology.");
    }

    private static bool IsPlaywrightType(Type type)
    {
        if (type.Namespace?.StartsWith(PlaywrightNamespace, StringComparison.Ordinal) == true)
        {
            return true;
        }

        // Task<IPage> and friends hide the offending type one level down.
        return type.IsGenericType && type.GetGenericArguments().Any(IsPlaywrightType);
    }

    private static void AssertNoDependency(PredicateList types, string forbiddenDependency, string because)
    {
        var result = types.ShouldNot().HaveDependencyOn(forbiddenDependency).GetResult();

        Assert.That(
            result.IsSuccessful,
            Is.True,
            $"{because}\nOffending types: {FormatFailures(result)}");
    }

    private static void AssertNoDependency(Types types, string forbiddenDependency, string because) =>
        AssertNoDependency(types.That().ResideInNamespaceStartingWith("JuiceShop"), forbiddenDependency, because);

    private static string FormatFailures(TestResult result) =>
        result.FailingTypeNames is null
            ? "(none reported)"
            : string.Join(", ", result.FailingTypeNames);
}
