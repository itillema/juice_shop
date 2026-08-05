using JuiceShop.Automation.Utility.Driver;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace JuiceShop.Automation.Utility.Artifacts;

/// <summary>
/// Collects diagnostic artifacts for a finished test.
/// </summary>
/// <remarks>
/// <para>
/// Playwright for .NET has no declarative equivalent of the JavaScript runner's
/// <c>trace: 'on-first-retry'</c> / <c>screenshot: 'only-on-failure'</c>. It is a widespread
/// misconception that <c>PageTest</c> and friends capture artifacts automatically — reading their
/// source shows no artifact code at all. Every byte collected here is collected because this class
/// asks for it.
/// </para>
/// <para>
/// The policy is deliberately asymmetric: tracing runs for every test, but the buffer is only
/// written to disk when the test failed. That keeps a green run's artifact footprint at zero while
/// still guaranteeing that any failure — including a first-time, non-reproducible one — arrives
/// with a full trace attached.
/// </para>
/// </remarks>
public sealed class ArtifactCollector
{
    private readonly ArtifactPathProvider _paths;
    private readonly ILogger<ArtifactCollector> _logger;

    public ArtifactCollector(ArtifactPathProvider paths, ILogger<ArtifactCollector> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(logger);

        _paths = paths;
        _logger = logger;
    }

    /// <summary>Starts collection for a test.</summary>
    public Task BeginAsync(IBrowserSession session, string testName)
    {
        ArgumentNullException.ThrowIfNull(session);

        _logger.LogDebug("Started artifact collection for {TestName}.", testName);
        return session.StartTracingAsync(testName);
    }

    /// <summary>
    /// Finishes collection, writing artifacts only when the test did not pass.
    /// </summary>
    /// <returns>Paths of the artifacts written, which may be empty.</returns>
    public async Task<IReadOnlyList<string>> CompleteAsync(IBrowserSession session, string testName)
    {
        ArgumentNullException.ThrowIfNull(session);

        var outcome = TestContext.CurrentContext.Result.Outcome.Status;
        var testFailed = outcome is TestStatus.Failed;
        var written = new List<string>(2);

        if (!testFailed)
        {
            // Stopping without a path discards the buffer. Skipping the call entirely would leave
            // the context accumulating trace data for the rest of its life.
            await session.StopTracingAsync(outputPath: null);
            return written;
        }

        // The path provider sanitises the test name itself — parameterised NUnit names routinely
        // contain quotes, commas and parentheses that are invalid in a Windows filename.
        var screenshotPath = await session.CaptureScreenshotAsync(_paths.ScreenshotFileFor(testName));
        if (screenshotPath is not null)
        {
            written.Add(screenshotPath);
            Attach(screenshotPath, "Screenshot at failure");
        }

        var tracePath = await session.StopTracingAsync(_paths.TraceFileFor(testName));
        if (tracePath is not null)
        {
            written.Add(tracePath);
            Attach(tracePath, "Playwright trace — open at https://trace.playwright.dev");
        }

        _logger.LogError(
            "Test failed. Collected {Count} artifact(s) for {TestName}.",
            written.Count,
            testName);

        return written;
    }

    /// <summary>
    /// Registers the file with NUnit so it flows into the .trx and into the Allure report body.
    /// </summary>
    private void Attach(string path, string description)
    {
        try
        {
            TestContext.AddTestAttachment(path, description);
        }
        catch (FileNotFoundException exception)
        {
            _logger.LogWarning(exception, "Artifact {Path} vanished before it could be attached.", path);
        }
    }
}
