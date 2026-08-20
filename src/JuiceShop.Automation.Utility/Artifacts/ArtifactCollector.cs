using JuiceShop.Automation.Utility.Driver;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace JuiceShop.Automation.Utility.Artifacts;

/// <summary>Collects diagnostic artifacts for a finished test.</summary>
/// <remarks>
/// Playwright for .NET has no <c>trace: 'on-first-retry'</c> equivalent and its NUnit base classes
/// capture nothing — every byte here is collected because this class asks for it. Tracing runs for
/// every test but is only written on failure, so a green run's footprint is zero. See docs/adr/0002.
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

    /// <summary>Finishes collection, writing artifacts only when the test did not pass.</summary>
    /// <returns>Paths written, which may be empty.</returns>
    public async Task<IReadOnlyList<string>> CompleteAsync(IBrowserSession session, string testName)
    {
        ArgumentNullException.ThrowIfNull(session);

        var outcome = TestContext.CurrentContext.Result.Outcome.Status;
        var testFailed = outcome is TestStatus.Failed;
        var written = new List<string>(2);

        if (!testFailed)
        {
            // Stopping without a path discards the buffer; skipping the call leaves it accumulating.
            await session.StopTracingAsync(outputPath: null);
            return written;
        }

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

    /// <summary>Registers the file with NUnit so it reaches the .trx and the Allure report.</summary>
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
