namespace JuiceShop.Automation.Execution.Tests;

/// <summary>Category names used to slice the suite: <c>--filter "TestCategory=Smoke"</c>.</summary>
/// <remarks>
/// Constants, not loose strings — a renamed category would leave a CI filter matching nothing, and
/// a filter that matches nothing reports success.
/// </remarks>
public static class TestCategories
{
    /// <summary>Fast, high-value checks. The gate for every pull request.</summary>
    public const string Smoke = "Smoke";

    /// <summary>Broader coverage, run on merge and nightly.</summary>
    public const string Regression = "Regression";

    /// <summary>Sign-in, sign-out and registration.</summary>
    public const string Authentication = "Authentication";

    /// <summary>Catalogue browsing and search.</summary>
    public const string Catalog = "Catalog";

    /// <summary>Basket management.</summary>
    public const string Basket = "Basket";

    /// <summary>Layering rules. No browser, no SUT — these run in milliseconds.</summary>
    public const string Architecture = "Architecture";

    /// <summary>Quarantined by filter rather than by <c>[Retry]</c>, so instability stays visible.</summary>
    public const string Flaky = "Flaky";
}
