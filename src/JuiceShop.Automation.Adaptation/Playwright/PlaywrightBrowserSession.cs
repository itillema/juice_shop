using JuiceShop.Automation.Adaptation.Contracts;
using JuiceShop.Automation.Adaptation.Contracts.Pages;
using JuiceShop.Automation.Adaptation.Pages;
using JuiceShop.Automation.Utility.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace JuiceShop.Automation.Adaptation.Playwright;

/// <summary>
/// Playwright-backed browsing session. Holds the page and the page objects reachable from it.
/// </summary>
internal sealed class PlaywrightBrowserSession : IBrowserSession
{
    private readonly IBrowserContext _context;
    private readonly IPage _page;
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
        _page = page;
        _settings = settings;
        _logger = logger;

        var expectTimeout = settings.Browser.ExpectTimeoutMilliseconds;

        Login = new LoginPage(page, expectTimeout);
        Registration = new RegistrationPage(page, expectTimeout);
        Navigation = new NavigationBar(page, expectTimeout);
        Catalog = new ProductCatalogPage(page, expectTimeout);
        Basket = new BasketPage(page, expectTimeout);
    }

    public ILoginPage Login { get; }

    public IRegistrationPage Registration { get; }

    public INavigationBar Navigation { get; }

    public IProductCatalogPage Catalog { get; }

    public IBasketPage Basket { get; }

    public async Task OpenApplicationAsync(CancellationToken cancellationToken = default)
    {
        await _page.GotoAsync(JuiceShopRoutes.Home);
        await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
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

        // Stop must be called on both paths. Omitting it on the passing path leaves the context
        // buffering trace data for the rest of its life; passing a null Path is how the buffer is
        // dropped instead of written.
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
            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = outputPath,
                FullPage = true,
            });

            return outputPath;
        }
        catch (PlaywrightException exception)
        {
            // A screenshot is a diagnostic aid. If the page has already crashed or closed, that is
            // worth a log line but must not replace the real test failure with a teardown error.
            _logger.LogWarning(exception, "Could not capture a screenshot for {Path}.", outputPath);
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Closing the context flushes video, if it was enabled, and releases the browser's
        // per-context memory. The shared browser deliberately outlives this.
        await _context.CloseAsync();
    }
}
