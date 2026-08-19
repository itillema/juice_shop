using JuiceShop.Automation.Utility.Artifacts;
using JuiceShop.Automation.Utility.Driver;
using JuiceShop.Automation.Utility.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace JuiceShop.Automation.Utility.Testing;

/// <summary>Base for browser tests: session lifecycle, per-test DI scope, artifact capture.</summary>
/// <remarks>
/// Framework-level and application-agnostic. Deliberately not <c>PageTest</c>, which would spend the
/// single inheritance slot and put an <c>IPage</c> on every test's surface. See docs/adr/0002.
/// </remarks>
public abstract class E2ETestBase
{
    private IServiceScope _scope = null!;
    private ArtifactCollector _artifacts = null!;

    /// <summary>The browsing session for the current test.</summary>
    protected IBrowserSession Session { get; private set; } = null!;

    /// <summary>Logger scoped to the concrete test class.</summary>
    protected ILogger Logger { get; private set; } = null!;

    /// <summary>Fully qualified name of the currently executing test.</summary>
    protected static string CurrentTestName => TestContext.CurrentContext.Test.FullName;

    /// <summary>Resolves a service from the current test's scope.</summary>
    protected T Resolve<T>()
        where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    [SetUp]
    public async Task SetUpSessionAsync()
    {
        _scope = AutomationRuntime.Services.CreateScope();

        Logger = _scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(GetType());

        _artifacts = _scope.ServiceProvider.GetRequiredService<ArtifactCollector>();

        // Deriving from this class is what declares a test needs a live application, so the SUT
        // wait and browser launch are charged here rather than to every test in the assembly.
        var sessionFactory = await AutomationRuntime.EnsureBrowsingAsync();

        Session = await sessionFactory.CreateSessionAsync();

        await _artifacts.BeginAsync(Session, CurrentTestName);
        await Session.OpenApplicationAsync();

        Logger.LogInformation("--- {TestName} ---", CurrentTestName);
    }

    [TearDown]
    public async Task TearDownSessionAsync()
    {
        try
        {
            if (Session is not null)
            {
                await _artifacts.CompleteAsync(Session, CurrentTestName);
                await Session.DisposeAsync();
            }
        }
        finally
        {
            // Must release even if artifact collection threw, or the real failure is buried.
            _scope?.Dispose();
        }
    }
}
