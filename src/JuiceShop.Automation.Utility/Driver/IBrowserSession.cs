using Microsoft.Playwright;

namespace JuiceShop.Automation.Utility.Driver;

/// <summary>One isolated browsing session against the SUT.</summary>
/// <remarks>
/// Exposes the live <see cref="IPage"/> deliberately: page objects need a real driver handle. The
/// boundary that matters is between page objects and test cases, and it is enforced by keeping page
/// objects internal to the Definition assembly. See docs/adr/0002.
/// </remarks>
public interface IBrowserSession : IAsyncDisposable
{
    /// <summary>The page this session drives.</summary>
    IPage Page { get; }

    /// <summary>Navigates to the application root and waits for it to be interactive.</summary>
    Task OpenApplicationAsync(CancellationToken cancellationToken = default);

    /// <summary>Begins collecting a diagnostic trace.</summary>
    /// <param name="title">Title recorded inside the trace, normally the test name.</param>
    Task StartTracingAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>Stops trace collection, discarding the buffer when <paramref name="outputPath"/> is null.</summary>
    /// <returns>The written path, or null if the trace was discarded.</returns>
    Task<string?> StopTracingAsync(string? outputPath, CancellationToken cancellationToken = default);

    /// <summary>Captures a full-page screenshot.</summary>
    /// <returns>The written path, or null if capture failed.</returns>
    Task<string?> CaptureScreenshotAsync(string outputPath, CancellationToken cancellationToken = default);
}
