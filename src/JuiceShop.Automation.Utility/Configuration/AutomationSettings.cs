using System.ComponentModel.DataAnnotations;

namespace JuiceShop.Automation.Utility.Configuration;

/// <summary>
/// Strongly typed root of the automation configuration, bound from appsettings.json and
/// overridable by environment variable.
/// </summary>
/// <remarks>
/// ISTQB CTAL-TAE 2016 §3.1.6 lists configuration of the test system as testware that must be
/// versioned alongside the SUT. Binding it into a validated object rather than reading loose
/// strings is what makes "the configuration is wrong" a startup failure instead of a mid-suite
/// mystery.
/// </remarks>
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

/// <summary>Where the system under test lives and how long we are willing to wait for it.</summary>
public sealed class SutSettings
{
    /// <summary>
    /// Base URL of Juice Shop. <c>http://127.0.0.1:3000</c> when tests run on the host against the
    /// container; <c>http://juice-shop:3000</c> when the runner is itself inside the compose network.
    /// Driving both from one key is why the same test binary works in either topology.
    /// </summary>
    [Required]
    [Url]
    public string BaseUrl { get; init; } = "http://127.0.0.1:3000";

    /// <summary>
    /// Readiness probe path. Returns HTTP 200 with a version payload once seeding has finished.
    /// Deliberately not <c>/rest/user/whoami</c>, which returns 200 with an empty user even when
    /// the app is not ready and so is useless as a gate.
    /// </summary>
    [Required]
    public string HealthPath { get; init; } = "/rest/admin/application-version";

    /// <summary>
    /// How long to wait for the SUT to become ready before failing the run.
    /// Juice Shop drops and re-seeds its entire SQLite database on every boot
    /// (<c>sequelize.sync({ force: true })</c>), which takes 20-60s on typical hardware.
    /// </summary>
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

    /// <summary>Milliseconds to slow each operation by. Useful for demoing a run to an audience.</summary>
    [Range(0, 5000)]
    public int SlowMoMilliseconds { get; init; }

    /// <summary>Default timeout for individual Playwright actions.</summary>
    [Range(1000, 120000)]
    public int ActionTimeoutMilliseconds { get; init; } = 15000;

    /// <summary>
    /// Timeout for auto-retrying web-first assertions. This is the knob that absorbs ordinary
    /// SPA rendering latency; raising it is almost always better than adding a sleep.
    /// </summary>
    [Range(1000, 120000)]
    public int ExpectTimeoutMilliseconds { get; init; } = 10000;

    [Range(1000, 180000)]
    public int NavigationTimeoutMilliseconds { get; init; } = 30000;

    [Range(320, 3840)]
    public int ViewportWidth { get; init; } = 1600;

    [Range(240, 2160)]
    public int ViewportHeight { get; init; } = 900;

    /// <summary>
    /// Skips the automatic browser download. Set to true inside the Playwright Docker images,
    /// which already ship browsers at <c>/ms-playwright</c>.
    /// </summary>
    public bool SkipBrowserInstall { get; init; }
}

/// <summary>Failure-artifact collection policy.</summary>
public sealed class ArtifactSettings
{
    /// <summary>Root directory for traces and screenshots, relative to the test output directory.</summary>
    [Required]
    public string Directory { get; init; } = "artifacts";

    /// <summary>
    /// Capture a Playwright trace. Tracing always starts; on a passing test the trace is discarded
    /// rather than written, which keeps artifact size proportional to failures.
    /// </summary>
    public bool CaptureTrace { get; init; } = true;

    public bool CaptureScreenshot { get; init; } = true;

    /// <summary>
    /// Video is decided at context creation and cannot be conditioned on the outcome, so enabling
    /// it means recording every test and deleting the passes. Off by default: traces already embed
    /// screenshots and DOM snapshots and are far more useful for diagnosis.
    /// </summary>
    public bool CaptureVideo { get; init; }
}

/// <summary>Test data generation settings.</summary>
public sealed class TestDataSettings
{
    /// <summary>
    /// Seed for Bogus. Fixing it makes generated data reproducible, so a reviewer can re-run a
    /// failing build and get the same values.
    /// </summary>
    public int Seed { get; init; } = 1337;

    /// <summary>Email domain for registered users. Must match Juice Shop's <c>application.domain</c>.</summary>
    [Required]
    public string EmailDomain { get; init; } = "juice-sh.op";
}
