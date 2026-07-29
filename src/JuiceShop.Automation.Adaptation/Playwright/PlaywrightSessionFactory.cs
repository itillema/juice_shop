using JuiceShop.Automation.Adaptation.Contracts;
using JuiceShop.Automation.Utility.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace JuiceShop.Automation.Adaptation.Playwright;

/// <summary>
/// Playwright-backed implementation of <see cref="ISessionFactory"/>.
/// </summary>
/// <remarks>
/// Owns exactly one <see cref="IPlaywright"/> and one <see cref="IBrowser"/> for the whole run, and
/// hands out one <see cref="IBrowserContext"/> per test. Launching a browser costs roughly a second;
/// creating a context costs single-digit milliseconds. Sharing the browser and isolating the context
/// is what makes a parallel suite both fast and free of cross-test bleed.
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
                Args = browserSettings.Name == "chromium"
                    // Containers default to a 64MB /dev/shm, which Chromium exhausts and then
                    // crashes the renderer. The failure surfaces as "Target closed" on a random
                    // test, which is indistinguishable from flakiness until you know to look.
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
            // Pinned so that neither the host machine's locale nor its theme can change what the
            // application renders. Juice Shop translates product names, so a different locale
            // breaks every text-based selector.
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

        await ApplyOverlayDismissalCookiesAsync(context);

        var page = await context.NewPageAsync();

        // Angular Material animates dialogs and snackbars in and out. Playwright's actionability
        // checks require an element to be stable, so an animating overlay turns every nearby click
        // into a timing gamble.
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });

        return new PlaywrightBrowserSession(
            context,
            page,
            _settings,
            _loggerFactory.CreateLogger<PlaywrightBrowserSession>());
    }

    /// <summary>
    /// Pre-dismisses the two overlays that block the first interaction of any fresh session.
    /// </summary>
    /// <remarks>
    /// This is exactly what Juice Shop's own Cypress harness does in its global setup. Dismissing
    /// them by clicking, in every test, is both slower and less reliable — the welcome banner is a
    /// Material dialog that traps focus, and the cookie bar sits bottom-right where it overlaps
    /// controls.
    /// <para>
    /// <c>language=en</c> is not cosmetic. Without it the application may resolve a different
    /// locale, and every visible-text selector then fails in a way that looks machine-specific.
    /// </para>
    /// </remarks>
    private async Task ApplyOverlayDismissalCookiesAsync(IBrowserContext context)
    {
        var baseUrl = _settings.Sut.BaseUrl;

        await context.AddCookiesAsync(
        [
            new Cookie { Name = "welcomebanner_status", Value = "dismiss", Url = baseUrl },
            new Cookie { Name = "cookieconsent_status", Value = "dismiss", Url = baseUrl },
            new Cookie { Name = "language", Value = "en", Url = baseUrl },
        ]);
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
