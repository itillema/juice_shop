using JuiceShop.Automation.Utility.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace JuiceShop.Automation.Utility.Driver;

/// <summary>One isolated Playwright context and the page driven within it.</summary>
/// <remarks>Lifetime and diagnostics only — it holds no knowledge of the application.</remarks>
internal sealed class PlaywrightBrowserSession : IBrowserSession
{
    private readonly IBrowserContext _context;
    private readonly AutomationSettings _settings;
    private readonly ILogger<PlaywrightBrowserSession> _logger;

    private bool _tracing;

    public PlaywrightBrowserSession(
        IBrowserContext context,
        IPage page,
        AutomationSettings settings,
        ILogger<PlaywrightBrowserSession> logger)
    {
        _context = context;
        _settings = settings;
        _logger = logger;

        Page = page;
    }

    public IPage Page { get; }

    public async Task OpenApplicationAsync(CancellationToken cancellationToken = default)
    {
        await Page.GotoAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    public async Task StartTracingAsync(string title, CancellationToken cancellationToken = default)
    {
        if (!_settings.Artifacts.CaptureTrace)
        {
            return;
        }

        await _context.Tracing.StartAsync(new TracingStartOptions
        {
            Title = title,
            Screenshots = true,
            Snapshots = true,
            Sources = true,
        });

        _tracing = true;
    }

    public async Task<string?> StopTracingAsync(string? outputPath, CancellationToken cancellationToken = default)
    {
        if (!_tracing)
        {
            return null;
        }

        _tracing = false;

        // Must be called on both paths, or the context buffers trace data for the rest of its life.
        // A null Path drops the buffer instead of writing it.
        await _context.Tracing.StopAsync(new TracingStopOptions { Path = outputPath });

        if (outputPath is not null)
        {
            _logger.LogInformation("Wrote Playwright trace to {TracePath}", outputPath);
        }

        return outputPath;
    }

    public async Task<string?> CaptureScreenshotAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        if (!_settings.Artifacts.CaptureScreenshot)
        {
            return null;
        }

        try
        {
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = outputPath,
                FullPage = true,
            });

            return outputPath;
        }
        catch (PlaywrightException exception)
        {
            // A crashed page must not replace the real test failure with a teardown error.
            _logger.LogWarning(exception, "Could not capture a screenshot for {Path}.", outputPath);
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Flushes video and frees per-context memory. The shared browser outlives this.
        await _context.CloseAsync();
    }
}
