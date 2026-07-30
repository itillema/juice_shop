using Microsoft.Extensions.Logging;

namespace JuiceShop.Automation.Utility.Driver;

/// <summary>
/// Ensures the Playwright browser binaries are present, without requiring a shell.
/// </summary>
/// <remarks>
/// <para>
/// The documented install route is <c>pwsh bin/&lt;config&gt;/&lt;tfm&gt;/playwright.ps1 install</c>.
/// That is a poor fit for a "clone and run" repository: <c>pwsh</c> is PowerShell 7, which is a
/// separate download and is <em>not</em> present on a default Windows install — Windows ships
/// PowerShell 5.1, which cannot run the script. This was confirmed on the development machine for
/// this project, where <c>pwsh</c> is absent.
/// </para>
/// <para>
/// <see cref="Microsoft.Playwright.Program.Main"/> is the officially documented alternative and
/// needs no shell at all. It is also a fast no-op when the browsers are already installed, so
/// calling it on every run costs nothing after the first.
/// </para>
/// </remarks>
internal static class BrowserInstaller
{
    /// <summary>Downloads the requested browser if it is not already present.</summary>
    /// <param name="browserName">chromium, firefox or webkit.</param>
    /// <param name="logger">Logger for progress reporting.</param>
    /// <exception cref="InvalidOperationException">The install command returned a non-zero exit code.</exception>
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
