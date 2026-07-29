using Allure.NUnit;
using JuiceShop.Automation.Execution.Testing;
using JuiceShop.Automation.Utility.TestData;
using NUnit.Framework;

namespace JuiceShop.Automation.Definition.Flows;

/// <summary>
/// Base class for Juice Shop test cases. Exposes the business-action facade and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// This type is the reason test fixtures never name an Adaptation type. It lives in the Flows
/// namespace, so the architecture rule "nothing in <c>...Definition.Tests</c> may depend on the
/// Adaptation assembly" holds even though the tests transitively use it — inheriting from a type
/// does not import its dependencies into the deriving type's own metadata.
/// </para>
/// <para>
/// <c>[AllureNUnit]</c> is applied here so every fixture is reported without each one having to
/// remember the attribute.
/// </para>
/// </remarks>
[AllureNUnit]
public abstract class JuiceShopTest : E2ETestBase
{
    /// <summary>The business-action facade for the current test's session.</summary>
    protected ShopFlow Shop { get; private set; } = null!;

    /// <summary>Generates unique registration data for tests that need their own account.</summary>
    protected RegistrationData NewAccount() => Resolve<RegistrationDataFactory>().Create();

    /// <summary>
    /// Builds the flow facade over the session created by the Execution layer.
    /// </summary>
    /// <remarks>
    /// NUnit runs base-class <c>[SetUp]</c> methods before derived ones, so
    /// <see cref="E2ETestBase.Session"/> is already populated by the time this runs. The ordering
    /// is guaranteed by NUnit, not incidental.
    /// </remarks>
    [SetUp]
    public void SetUpFlows()
    {
        Shop = new ShopFlow(Session);
    }
}
