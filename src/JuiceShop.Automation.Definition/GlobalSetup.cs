using System.Diagnostics.CodeAnalysis;
using JuiceShop.Automation.Execution.Runtime;
using NUnit.Framework;

// Deliberately declared outside any namespace. NUnit treats a [SetUpFixture] with no namespace as
// applying to the entire assembly, which is what makes this run exactly once before any fixture
// and once after the last one. Putting it in a namespace would silently scope it to that namespace
// only — the tests would still run, but against an uninitialised runtime.

/// <summary>
/// Assembly-level setup. Delegates to the Execution layer, which owns the real work.
/// </summary>
/// <remarks>
/// This lives in the test project because NUnit only discovers setup fixtures in the assembly under
/// test — a <c>[SetUpFixture]</c> in a referenced class library is never found.
/// </remarks>
[SetUpFixture]
[SuppressMessage(
    "Design",
    "CA1050:Declare types in namespaces",
    Justification = "NUnit scopes a [SetUpFixture] to the namespace it is declared in. Assembly-wide " +
                    "scope requires no namespace, so this rule cannot be satisfied without breaking " +
                    "the run-once-per-assembly semantics this type exists to provide.")]
public sealed class GlobalSetup
{
    [OneTimeSetUp]
    public Task RunBeforeAnyTestsAsync() => AutomationRuntime.StartAsync();

    [OneTimeTearDown]
    public Task RunAfterAllTestsAsync() => AutomationRuntime.StopAsync();
}
