using System.Diagnostics.CodeAnalysis;
using JuiceShop.Automation.Adaptation.DependencyInjection;
using JuiceShop.Automation.Definition.DependencyInjection;
using JuiceShop.Automation.Utility.Runtime;
using NUnit.Framework;

// MUST stay outside any namespace: NUnit scopes a [SetUpFixture] to its namespace, and only a
// namespace-less one runs once for the whole assembly. In a namespace the tests still run, but
// against an uninitialised runtime.

/// <summary>Assembly-level setup, and the solution's composition root.</summary>
/// <remarks>
/// The one place that knows every layer at once. Wiring here rather than inside the framework is
/// what keeps Utility free of references to Adaptation and Definition. It lives in the test project
/// because NUnit only discovers setup fixtures in the assembly under test.
/// </remarks>
[SetUpFixture]
[SuppressMessage(
    "Design",
    "CA1050:Declare types in namespaces",
    Justification = "Assembly-wide [SetUpFixture] scope requires no namespace, so this rule cannot " +
                    "be satisfied without breaking the run-once-per-assembly semantics.")]
public sealed class GlobalSetup
{
    [OneTimeSetUp]
    public Task RunBeforeAnyTestsAsync() =>
        AutomationRuntime.StartAsync(services => services
            .AddAdaptationLayer()
            .AddDefinitionLayer());

    [OneTimeTearDown]
    public Task RunAfterAllTestsAsync() => AutomationRuntime.StopAsync();
}
