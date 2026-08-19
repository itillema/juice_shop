using JuiceShop.Automation.Utility.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace JuiceShop.Automation.Utility.Driver;

/// <summary>Playwright-backed <see cref="ISessionFactory"/>.</summary>
/// <remarks>
/// One <see cref="IPlaywright"/> and one <see cref="IBrowser"/> per run; one context per test.
/// A browser costs ~1s to launch, a context single-digit milliseconds.
/// </remarks>
internal sealed class PlaywrightSessionFactory : ISessionFactory
{
    private readonly AutomationSettings _settings;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PlaywrightSessionFactory> _logger;
    private readonly SemaphoreSlim _initialisationGate = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightSessionFactory(
        IOptions<AutomationSettings> settings,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _settings = settings.Value;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PlaywrightSessionFactory>();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initialisationGate.WaitAsync(cancellationToken);
        try
        {
            if (_browser is not null)
            {
                return;
            }

            var browserSettings = _settings.Browser;

            if (!browserSettings.SkipBrowserInstall)
            {
                BrowserInstaller.EnsureInstalled(browserSettings.Name, _logger);
            }

            _playwright = await Microsoft.Playwright.Playwright.CreateAsync();

            var browserType = browserSettings.Name switch
            {
                "chromium" => _playwright.Chromium,
                "firefox" => _playwright.Firefox,
                "webkit" => _playwright.Webkit,
                _ => throw new InvalidOperationException(
                    $"Unsupported browser '{browserSettings.Name}'."),
            };

            _browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = browserSettings.Headless,
                SlowMo = browserSettings.SlowMoMilliseconds,
                // Containers default to a 64MB /dev/shm, which Chromium exhausts and then crashes
                // the renderer — surfacing as "Target closed" on a random test.
                Args = browserSettings.Name == "chromium"
                    ? ["--disable-dev-shm-usage"]
                    : null,
            });

            _logger.LogInformation(
                "Launched {Browser} {Version} (headless: {Headless}).",
                browserSettings.Name,
                _browser.Version,
                browserSettings.Headless);
        }
        finally
        {
            _initialisationGate.Release();
        }
    }

    public async Task<IBrowserSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_browser is null)
        {
            throw new InvalidOperationException(
                $"{nameof(InitializeAsync)} must be called before creating a session.");
        }

        var browserSettings = _settings.Browser;
        var artifactSettings = _settings.Artifacts;

        var contextOptions = new BrowserNewContextOptions
        {
            BaseURL = _settings.Sut.BaseUrl,
            ViewportSize = new ViewportSize
            {
                Width = browserSettings.ViewportWidth,
                Height = browserSettings.ViewportHeight,
            },
            // Pinned: Juice Shop translates product names, so a different locale breaks every
            // text-based selector.
            Locale = "en-US",
            ColorScheme = ColorScheme.Light,
            IgnoreHTTPSErrors = true,
        };

        if (artifactSettings.CaptureVideo)
        {
            contextOptions.RecordVideoDir = Path.Combine(artifactSettings.Directory, "videos");
        }

        var context = await _browser.NewContextAsync(contextOptions);

        context.SetDefaultTimeout(browserSettings.ActionTimeoutMilliseconds);
        context.SetDefaultNavigationTimeout(browserSettings.NavigationTimeoutMilliseconds);

        await ApplySessionCookiesAsync(context);

        var page = await context.NewPageAsync();

        // Playwright requires an element to be stable, so an animating overlay turns every nearby
        // click into a timing gamble.
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });

        return new PlaywrightBrowserSession(
            context,
            page,
            _settings,
            _loggerFactory.CreateLogger<PlaywrightBrowserSession>());
    }

    /// <summary>Seeds the configured cookies before the context's first navigation.</summary>
    /// <remarks>
    /// Names and values come from configuration, so this carries no knowledge of the application.
    /// For Juice Shop they pre-dismiss the welcome dialog and cookie banner — clicking them away
    /// instead is slower and less reliable.
    /// </remarks>
    private async Task ApplySessionCookiesAsync(IBrowserContext context)
    {
        var cookies = _settings.Browser.SessionCookies;

        if (cookies.Count == 0)
        {
            return;
        }

        var baseUrl = _settings.Sut.BaseUrl;

        await context.AddCookiesAsync(
            [.. cookies.Select(cookie => new Cookie
            {
                Name = cookie.Key,
                Value = cookie.Value,
                Url = baseUrl,
            })]);
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
        _initialisationGate.Dispose();
    }
}
