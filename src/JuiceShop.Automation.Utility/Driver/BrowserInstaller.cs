using Microsoft.Extensions.Logging;

namespace JuiceShop.Automation.Utility.Driver;

/// <summary>Ensures the Playwright browser binaries are present, without requiring a shell.</summary>
/// <remarks>
/// <see cref="Microsoft.Playwright.Program.Main"/> rather than <c>playwright.ps1</c>, which needs
/// PowerShell 7 — a separate download that Windows does not ship. A fast no-op once installed.
/// </remarks>
internal static class BrowserInstaller
{
    /// <summary>Downloads the requested browser if it is not already present.</summary>
    /// <param name="browserName">chromium, firefox or webkit.</param>
    public static void EnsureInstalled(string browserName, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(browserName);
        ArgumentNullException.ThrowIfNull(logger);

        logger.LogInformation("Ensuring Playwright browser '{Browser}' is installed.", browserName);

        var exitCode = Microsoft.Playwright.Program.Main(["install", browserName]);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Playwright failed to install browser '{browserName}' (exit code {exitCode}). " +
                "If this machine has no network access, pre-install the browsers and set " +
                "Automation:Browser:SkipBrowserInstall to true.");
        }

        logger.LogInformation("Playwright browser '{Browser}' is ready.", browserName);
    }
}
