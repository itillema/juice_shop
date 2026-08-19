namespace JuiceShop.Automation.Utility.Driver;

/// <summary>Creates isolated browsing sessions, and owns the process-wide browser behind them.</summary>
/// <remarks>
/// One browser per run, a fresh context per test. Sharing the page or the context instead is the
/// classic mistake and produces cross-test interference that reads as flakiness.
/// </remarks>
public interface ISessionFactory : IAsyncDisposable
{
    /// <summary>Launches the browser. Called once per run, before any session is created.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a session with its own isolated storage and seeded cookies.</summary>
    Task<IBrowserSession> CreateSessionAsync(CancellationToken cancellationToken = default);
}
