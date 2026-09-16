using System.ComponentModel.DataAnnotations;

namespace JuiceShop.Automation.Utility.Configuration;

/// <summary>Validated root of the automation configuration.</summary>
/// <remarks>Bound from appsettings.json; overridable by environment variable.</remarks>
public sealed class AutomationSettings
{
    /// <summary>Configuration section name. Environment overrides use <c>AUTOMATION__</c>.</summary>
    public const string SectionName = "Automation";

    [Required]
    public SutSettings Sut { get; init; } = new();

    [Required]
    public BrowserSettings Browser { get; init; } = new();

    [Required]
    public ArtifactSettings Artifacts { get; init; } = new();

    [Required]
    public TestDataSettings TestData { get; init; } = new();
}

/// <summary>Where the SUT lives and how long to wait for it.</summary>
public sealed class SutSettings
{
    /// <summary>Host-to-container, or <c>http://juice-shop:3000</c> from inside the compose network.</summary>
    [Required]
    [RegularExpression(
        @"^https?://[^/\s]+(/\S*)?$",
        ErrorMessage = "BaseUrl must be an http or https URL with a host, e.g. http://127.0.0.1:3000.")]
    public string BaseUrl { get; init; } = "http://127.0.0.1:3000";

    /// <summary>Readiness probe. Not <c>/rest/user/whoami</c>, which returns 200 before seeding finishes.</summary>
    [Required]
    public string HealthPath { get; init; } = "/rest/admin/application-version";

    /// <summary>Juice Shop re-seeds its database on every boot, which takes 20-60s.</summary>
    [Range(10, 600)]
    public int ReadinessTimeoutSeconds { get; init; } = 150;

    [Range(1, 30)]
    public int ReadinessPollIntervalSeconds { get; init; } = 2;
}

/// <summary>Browser launch and context configuration.</summary>
public sealed class BrowserSettings
{
    /// <summary>chromium, firefox or webkit.</summary>
    [Required]
    [RegularExpression("^(chromium|firefox|webkit)$", ErrorMessage = "Browser must be chromium, firefox or webkit.")]
    public string Name { get; init; } = "chromium";

    public bool Headless { get; init; } = true;

    /// <summary>Slows each operation. For demoing a run to an audience.</summary>
    [Range(0, 5000)]
    public int SlowMoMilliseconds { get; init; }

    /// <summary>Default timeout for individual Playwright actions.</summary>
    [Range(1000, 120000)]
    public int ActionTimeoutMilliseconds { get; init; } = 15000;

    /// <summary>Timeout for auto-retrying assertions. Raise this rather than adding a sleep.</summary>
    [Range(1000, 120000)]
    public int ExpectTimeoutMilliseconds { get; init; } = 10000;

    [Range(1000, 180000)]
    public int NavigationTimeoutMilliseconds { get; init; } = 30000;

    [Range(320, 3840)]
    public int ViewportWidth { get; init; } = 1600;

    [Range(240, 2160)]
    public int ViewportHeight { get; init; } = 900;

    /// <summary>Set inside the Playwright Docker images, which already ship browsers.</summary>
    public bool SkipBrowserInstall { get; init; }

    /// <summary>Cookies seeded into every session before its first navigation.</summary>
    /// <remarks>Configured, not hard-coded, so the driver stays free of application knowledge.</remarks>
    public IDictionary<string, string> SessionCookies { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>Failure-artifact collection policy.</summary>
public sealed class ArtifactSettings
{
    /// <summary>Root for traces and screenshots, relative to the test output directory.</summary>
    [Required]
    public string Directory { get; init; } = "artifacts";

    /// <summary>Tracing always starts; a passing test's trace is discarded rather than written.</summary>
    public bool CaptureTrace { get; init; } = true;

    public bool CaptureScreenshot { get; init; } = true;

    /// <summary>Off by default: video cannot be conditioned on the outcome, and traces diagnose better.</summary>
    public bool CaptureVideo { get; init; }
}

/// <summary>Test data generation settings.</summary>
public sealed class TestDataSettings
{
    /// <summary>Bogus seed. Fixed so a failing build re-runs with the same values.</summary>
    public int Seed { get; init; } = 1337;

    /// <summary>Must match Juice Shop's <c>application.domain</c>.</summary>
    [Required]
    public string EmailDomain { get; init; } = "juice-sh.op";
}
