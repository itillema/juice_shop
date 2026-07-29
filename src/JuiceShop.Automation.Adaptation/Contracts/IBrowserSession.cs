using JuiceShop.Automation.Adaptation.Contracts.Pages;

namespace JuiceShop.Automation.Adaptation.Contracts;

/// <summary>
/// One isolated browsing session against the SUT, together with the pages reachable from it.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam between the Adaptation layer and everything above it. Note what is absent:
/// there is no <c>IPage</c>, no locator, no browser type — nothing that names the automation
/// technology. That is what allows Microsoft.Playwright to be referenced by exactly one project
/// in the solution, and it is asserted by
/// <c>JuiceShop.Automation.Definition.Architecture.ArchitectureTests</c>.
/// </para>
/// <para>
/// The artifact operations live here rather than in the Execution layer for the same reason:
/// Execution needs to capture a trace on failure, but must not learn what a trace is made of.
/// </para>
/// </remarks>
public interface IBrowserSession : IAsyncDisposable
{
    /// <summary>The login page.</summary>
    ILoginPage Login { get; }

    /// <summary>The registration page.</summary>
    IRegistrationPage Registration { get; }

    /// <summary>The persistent top navigation bar.</summary>
    INavigationBar Navigation { get; }

    /// <summary>The product catalogue and search.</summary>
    IProductCatalogPage Catalog { get; }

    /// <summary>The shopping basket.</summary>
    IBasketPage Basket { get; }

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
