namespace JuiceShop.Automation.Definition.Tests;

/// <summary>
/// Category names used to slice the suite from the command line.
/// </summary>
/// <remarks>
/// Constants rather than loose strings so that a rename cannot leave a CI filter silently matching
/// nothing — a filter that matches no tests reports success, which is the worst possible failure
/// mode for a quality gate.
/// <para>
/// Example: <c>dotnet test --filter "TestCategory=Smoke"</c>
/// </para>
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

    /// <summary>
    /// Quarantined as intermittently unreliable. Excluded from the main run with
    /// <c>--filter "TestCategory!=Flaky"</c> rather than papered over with a retry attribute,
    /// so that the instability stays visible instead of being hidden by a green build.
    /// </summary>
    public const string Flaky = "Flaky";
}
