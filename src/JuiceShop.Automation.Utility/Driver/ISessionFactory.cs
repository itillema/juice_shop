namespace JuiceShop.Automation.Utility.Driver;

/// <summary>
/// Creates isolated browsing sessions, and owns the process-wide browser that backs them.
/// </summary>
/// <remarks>
/// Only one driver should communicate with the SUT, but the distinction that matters in practice is
/// <em>which</em> object is the singleton: one browser process shared across the run, and a fresh
/// isolated context — cookies, storage, cache — per test. Sharing the page or the context instead is
/// the classic mistake, and under parallel execution it produces cross-test interference that reads
/// as flakiness.
/// </remarks>
public interface ISessionFactory : IAsyncDisposable
{
    /// <summary>Launches the browser. Called once per run, before any session is created.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a session with its own isolated storage, pre-configured so that the first
    /// interaction is not blocked by the application's welcome dialog or cookie banner.
    /// </summary>
    Task<IBrowserSession> CreateSessionAsync(CancellationToken cancellationToken = default);
}
