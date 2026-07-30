using Allure.NUnit;
using JuiceShop.Automation.Definition.Flows;
using JuiceShop.Automation.Definition.TestData;
using JuiceShop.Automation.Utility.Configuration;
using JuiceShop.Automation.Utility.Testing;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace JuiceShop.Automation.Execution;

/// <summary>
/// Base class for Juice Shop test cases. Exposes the business-action facade and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// The seam between the framework and the application. <see cref="E2ETestBase"/> in the Utility
/// layer knows about sessions, scopes and artifacts but nothing about shops; this adds the one
/// application-specific thing a test needs, which is <see cref="Shop"/>.
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
    /// Builds the flow facade over the session created by the framework.
    /// </summary>
    /// <remarks>
    /// NUnit runs base-class <c>[SetUp]</c> methods before derived ones, so
    /// <see cref="E2ETestBase.Session"/> is already populated by the time this runs. The ordering is
    /// guaranteed by NUnit, not incidental.
    /// </remarks>
    [SetUp]
    public void SetUpFlows()
    {
        Shop = new ShopFlow(Session, Resolve<IOptions<AutomationSettings>>());
    }
}
