using System.Buffers;
using System.Text;
using JuiceShop.Automation.Utility.Configuration;
using Microsoft.Extensions.Options;

namespace JuiceShop.Automation.Utility.Artifacts;

/// <summary>
/// Resolves where failure artifacts are written, and turns test names into safe file names.
/// </summary>
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

        // An absolute path in configuration wins; otherwise resolve relative to the test binary,
        // which is where CI expects to collect from.
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(baseDirectory, configured);
    }

    /// <summary>
    /// Directory Allure writes its raw results into.
    /// </summary>
    /// <remarks>
    /// Fixed by <c>allureConfig.json</c> and resolved by Allure relative to the test binary, not to
    /// the repository root — a CI step pointed at <c>./allure-results</c> finds an empty directory.
    /// </remarks>
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

    /// <summary>
    /// Removes artifacts left behind by a previous run.
    /// </summary>
    /// <remarks>
    /// Without this, a green run still ships the screenshots and traces of whatever failed last
    /// time. CI uploads them, someone opens the artifact bundle expecting it to describe this run,
    /// and draws a conclusion about a failure that no longer exists. Artifacts are only meaningful
    /// if their presence means "this run produced them".
    /// <para>
    /// Allure results are cleared for the same reason. Allure appends, so a locally generated report
    /// would otherwise show every test from every run this working copy has ever executed. Trend and
    /// history come from the published report's own history directory, not from these raw results, so
    /// nothing is lost.
    /// </para>
    /// <para>
    /// Logs are deliberately not cleared: they roll by day and are useful across runs.
    /// </para>
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

    /// <summary>Full path for a test's trace archive, creating the directory if needed.</summary>
    public string TraceFileFor(string testName) => Reserve(TraceDirectory, testName, ".zip");

    /// <summary>Full path for a test's screenshot, creating the directory if needed.</summary>
    public string ScreenshotFileFor(string testName) => Reserve(ScreenshotDirectory, testName, ".png");

    private static string Reserve(string directory, string testName, string extension)
    {
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Sanitize(testName) + extension);
    }

    /// <summary>
    /// Reduces a fully qualified test name to something safe on every filesystem.
    /// NUnit test names routinely contain parentheses, commas and quotes from parameterised cases,
    /// all of which are either invalid on Windows or awkward to handle in CI artifact globs.
    /// </summary>
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

        // Windows still enforces MAX_PATH for many APIs; long parameterised names blow past it.
        const int maxLength = 120;
        return sanitized.Length <= maxLength ? sanitized : sanitized[^maxLength..];
    }
}
