using System.Reflection;
using JuiceShop.Automation.Adaptation.Sut;
using JuiceShop.Automation.Definition.Flows;
using JuiceShop.Automation.Execution.Tests;
using JuiceShop.Automation.Utility.Configuration;
using NetArchTest.Rules;
using NUnit.Framework;

namespace JuiceShop.Automation.Execution.Architecture;

/// <summary>
/// Executable specification of the layering.
/// </summary>
/// <remarks>
/// <para>
/// The point of these is that the architecture is <em>tested</em>, not merely described in a README
/// that drifts. They run in the same <c>dotnet test</c> invocation as the browser tests, need no SUT
/// and no browser, and finish in milliseconds.
/// </para>
/// <para>
/// Much of the layering is already enforced without them. MSBuild rejects a circular
/// <c>ProjectReference</c> graph outright, and the page objects and their contracts are
/// <c>internal</c> to the Definition assembly, so no test can reach a locator even deliberately.
/// These cover what the compiler cannot see: which layers are allowed to know about the automation
/// technology, and whether the dependency graph still points the way it is documented to.
/// </para>
/// </remarks>
[TestFixture]
[Category(TestCategories.Architecture)]
[Parallelizable(ParallelScope.All)]
public sealed class ArchitectureTests
{
    private const string PlaywrightNamespace = "Microsoft.Playwright";
    private const string UtilityAssembly = "JuiceShop.Automation.Utility";
    private const string AdaptationAssembly = "JuiceShop.Automation.Adaptation";
    private const string DefinitionAssembly = "JuiceShop.Automation.Definition";
    private const string ExecutionAssembly = "JuiceShop.Automation.Execution";

    private static Assembly Utility => typeof(AutomationSettings).Assembly;

    private static Assembly Adaptation => typeof(HttpSutReadinessGate).Assembly;

    private static Assembly Definition => typeof(ShopFlow).Assembly;

    private static Assembly Execution => typeof(ArchitectureTests).Assembly;

    [Test]
    [Description("Utility is the bottom of the graph and depends on no other layer.")]
    public void Utility_Depends_On_No_Other_Layer()
    {
        const string Because =
            "The framework must stay reusable against another product. Where it needs something " +
            "from an integration, it declares a port (see ISutReadinessGate) and lets the " +
            "composition root supply the adapter.";

        AssertNoDependency(Utility, AdaptationAssembly, Because);
        AssertNoDependency(Utility, DefinitionAssembly, Because);
        AssertNoDependency(Utility, ExecutionAssembly, Because);
    }

    [Test]
    [Description("Adaptation and Definition are siblings; neither knows about the other or the tests.")]
    public void Sibling_Layers_Do_Not_Depend_On_Each_Other()
    {
        AssertNoDependency(
            Adaptation,
            DefinitionAssembly,
            "Integrations must not know how the UI is driven.");

        AssertNoDependency(
            Definition,
            AdaptationAssembly,
            "Page objects and flows must not reach an external service directly. If a flow needs " +
            "one, route it through a port on the Utility layer so the dependency stays explicit.");

        AssertNoDependency(Adaptation, ExecutionAssembly, "No layer may depend on the test cases.");
        AssertNoDependency(Definition, ExecutionAssembly, "No layer may depend on the test cases.");
    }

    [Test]
    [Description("Test cases express intent, not browser mechanics.")]
    public void Test_Cases_Do_Not_Depend_On_Playwright()
    {
        // This fixture is excluded from its own rule: it names the forbidden namespace as string
        // data, and NetArchTest's IL scan cannot tell a constant naming a namespace apart from a
        // genuine reference to a type in it. Every other type in the assembly is still covered.
        AssertNoDependency(
            Types.InAssembly(Execution)
                .That()
                .DoNotResideInNamespace(typeof(ArchitectureTests).Namespace),
            PlaywrightNamespace,
            "A test reached for the browser directly. Add the behaviour to a page object and expose " +
            "it through a flow — that is the seam that keeps test cases readable and insulates them " +
            "from changes to the application's interface.");
    }

    [Test]
    [Description("The Adaptation layer speaks protocols, not browsers.")]
    public void Adaptation_Does_Not_Depend_On_Playwright() =>
        AssertNoDependency(
            Adaptation,
            PlaywrightNamespace,
            "Adaptation exists for external services, protocols and data stores. Driving a browser " +
            "is the Utility layer's driver, and page objects belong in Definition.");

    [Test]
    [Description("Page objects stay internal, so no test can reach a locator.")]
    public void Page_Objects_And_Their_Contracts_Are_Not_Publicly_Visible()
    {
        var leaked = Types.InAssembly(Definition)
            .That()
            .ResideInNamespace($"{DefinitionAssembly}.Pages")
            .And()
            .ArePublic()
            .GetTypes()
            .ToList();

        Assert.That(
            leaked,
            Is.Empty,
            "Page objects and their contracts must be internal to the Definition assembly. Making " +
            "one public would let a test bypass the flow facade and drive the browser directly.");
    }

    [Test]
    [Description("The flow facade exposes no Playwright types to the test cases.")]
    public void Definition_Public_Api_Exposes_No_Playwright_Types()
    {
        var offenders = new List<string>();

        foreach (var type in Definition.GetExportedTypes())
        {
            const BindingFlags Members =
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var method in type.GetMethods(Members))
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
                type.GetProperties(Members)
                    .Where(property => IsPlaywrightType(property.PropertyType))
                    .Select(property => $"{type.Name}.{property.Name} is {property.PropertyType.Name}"));
        }

        Assert.That(
            offenders,
            Is.Empty,
            "The Definition layer leaked a Playwright type through its public API, which would " +
            "couple the test cases to the automation technology.");
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

    private static void AssertNoDependency(Assembly assembly, string forbiddenDependency, string because) =>
        AssertNoDependency(
            Types.InAssembly(assembly).That().ResideInNamespaceStartingWith("JuiceShop"),
            forbiddenDependency,
            because);

    private static void AssertNoDependency(PredicateList types, string forbiddenDependency, string because)
    {
        var result = types.ShouldNot().HaveDependencyOn(forbiddenDependency).GetResult();

        Assert.That(
            result.IsSuccessful,
            Is.True,
            $"{because}\nOffending types: {FormatFailures(result)}");
    }

    private static string FormatFailures(TestResult result) =>
        result.FailingTypeNames is null
            ? "(none reported)"
            : string.Join(", ", result.FailingTypeNames);
}
