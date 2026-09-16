using Allure.NUnit;
using JuiceShop.Automation.Definition.Flows;
using JuiceShop.Automation.Definition.TestData;
using JuiceShop.Automation.Utility.Configuration;
using JuiceShop.Automation.Utility.Testing;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace JuiceShop.Automation.Execution;

/// <summary>Base for Juice Shop test cases. Exposes the business-action facade and nothing else.</summary>
/// <remarks>
/// The seam between framework and application: <see cref="E2ETestBase"/> knows sessions and artifacts, this adds <see cref="Shop"/>. <c>[AllureNUnit]</c> here so no fixture must remember it.
/// </remarks>
[AllureNUnit]
public abstract class JuiceShopTest : E2ETestBase
{
    /// <summary>The business-action facade for the current test's session.</summary>
    protected ShopFlow Shop { get; private set; } = null!;

    /// <summary>Generates unique registration data for tests that need their own account.</summary>
    protected RegistrationData NewAccount() => Resolve<RegistrationDataFactory>().Create();

    /// <summary>Builds the flow facade over the session created by the framework.</summary>
    /// <remarks>NUnit guarantees base-class <c>[SetUp]</c> runs first, so Session is populated.</remarks>
    [SetUp]
    public void SetUpFlows()
    {
        Shop = new ShopFlow(Session, Resolve<IOptions<AutomationSettings>>());
    }
}
