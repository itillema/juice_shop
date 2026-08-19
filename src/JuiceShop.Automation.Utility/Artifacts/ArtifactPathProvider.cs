using System.Buffers;
using System.Text;
using JuiceShop.Automation.Utility.Configuration;
using Microsoft.Extensions.Options;

namespace JuiceShop.Automation.Utility.Artifacts;

/// <summary>Resolves where failure artifacts are written, and makes test names safe as file names.</summary>
public sealed class ArtifactPathProvider
{
    private static readonly SearchValues<char> InvalidChars =
        SearchValues.Create(new string(Path.GetInvalidFileNameChars()) + " ,()<>\"'");

    private readonly string _root;
    private readonly string _baseDirectory;

    public ArtifactPathProvider(IOptions<AutomationSettings> settings, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        _baseDirectory = baseDirectory;

        var configured = settings.Value.Artifacts.Directory;

        // An absolute path in configuration wins; otherwise resolve against the test binary.
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(baseDirectory, configured);
    }

    /// <summary>Where Allure writes raw results.</summary>
    /// <remarks>Resolved relative to the test binary, not the repo root — CI must collect from here.</remarks>
    public string AllureResultsDirectory => Path.Combine(_baseDirectory, "allure-results");

    /// <summary>Root artifact directory for the run.</summary>
    public string Root => _root;

    /// <summary>Directory holding Playwright traces.</summary>
    public string TraceDirectory => Path.Combine(_root, "traces");

    /// <summary>Directory holding failure screenshots.</summary>
    public string ScreenshotDirectory => Path.Combine(_root, "screenshots");

    /// <summary>Directory holding recorded video, when video capture is enabled.</summary>
    public string VideoDirectory => Path.Combine(_root, "videos");

    /// <summary>Directory holding run logs.</summary>
    public string LogDirectory => Path.Combine(_root, "logs");

    /// <summary>Removes artifacts left behind by a previous run.</summary>
    /// <remarks>
    /// Artifacts are only meaningful if their presence means "this run produced them". Allure
    /// results are cleared for the same reason — it appends, and history comes from the published
    /// report. Logs are kept: they roll by day and are useful across runs.
    /// </remarks>
    public void ClearPreviousRun()
    {
        foreach (var directory in new[]
                 {
                     TraceDirectory, ScreenshotDirectory, VideoDirectory, AllureResultsDirectory,
                 })
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>Path for a test's trace archive, creating the directory if needed.</summary>
    public string TraceFileFor(string testName) => Reserve(TraceDirectory, testName, ".zip");

    /// <summary>Path for a test's screenshot, creating the directory if needed.</summary>
    public string ScreenshotFileFor(string testName) => Reserve(ScreenshotDirectory, testName, ".png");

    private static string Reserve(string directory, string testName, string extension)
    {
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Sanitize(testName) + extension);
    }

    /// <summary>Reduces a test name to something safe on every filesystem.</summary>
    /// <remarks>Parameterised NUnit names carry parentheses, commas and quotes.</remarks>
    internal static string Sanitize(string testName)
    {
        if (string.IsNullOrWhiteSpace(testName))
        {
            return "unnamed-test";
        }

        var builder = new StringBuilder(testName.Length);
        foreach (var character in testName)
        {
            builder.Append(InvalidChars.Contains(character) ? '_' : character);
        }

        var sanitized = builder.ToString().Trim('_', '.');

        // Windows still enforces MAX_PATH for many APIs.
        const int maxLength = 120;
        return sanitized.Length <= maxLength ? sanitized : sanitized[^maxLength..];
    }
}
