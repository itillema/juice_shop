using Microsoft.Playwright;

namespace JuiceShop.Automation.Utility.Driver;

/// <summary>
/// One isolated browsing session against the SUT.
/// </summary>
/// <remarks>
/// <para>
/// The Playwright driver is a Utility concern, so this contract exposes the live
/// <see cref="IPage"/> for the layer above to drive. That is deliberate: page objects in the
/// Definition layer need a real driver handle, and hand-rolling an abstraction over Playwright's
/// locator API would be a large surface for no benefit. The boundary that actually matters is the
/// one between page objects and test cases, and it is enforced by keeping page objects internal to
/// the Definition assembly rather than by hiding the driver here.
/// </para>
/// <para>
/// What the session adds on top of the raw page is lifecycle and diagnostics: an isolated context
/// per test, plus trace and screenshot capture expressed so the reporting code can call it without
/// knowing what a trace is made of.
/// </para>
/// </remarks>
public interface IBrowserSession : IAsyncDisposable
{
    /// <summary>The page this session drives.</summary>
    IPage Page { get; }

    /// <summary>Navigates to the application root and waits for it to be interactive.</summary>
    Task OpenApplicationAsync(CancellationToken cancellationToken = default);

    /// <summary>Begins collecting a diagnostic trace for this session.</summary>
    /// <param name="title">Title recorded inside the trace, normally the test name.</param>
    Task StartTracingAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops trace collection. Passing <see langword="null"/> discards the trace rather than
    /// writing it — that is the supported way to keep traces for failures only, and the stop call
    /// must still happen on the passing path or the session keeps buffering.
    /// </summary>
    /// <param name="outputPath">Destination archive path, or <see langword="null"/> to discard.</param>
    /// <returns>The written path, or <see langword="null"/> if the trace was discarded.</returns>
    Task<string?> StopTracingAsync(string? outputPath, CancellationToken cancellationToken = default);

    /// <summary>Captures a full-page screenshot.</summary>
    /// <returns>The written path, or <see langword="null"/> if capture failed.</returns>
    Task<string?> CaptureScreenshotAsync(string outputPath, CancellationToken cancellationToken = default);
}
