using JuiceShop.Automation.Adaptation.Contracts;
using JuiceShop.Automation.Execution.Artifacts;
using JuiceShop.Automation.Execution.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace JuiceShop.Automation.Execution.Testing;

/// <summary>
/// Base class for browser-driven tests: session lifecycle, per-test DI scope and artifact capture.
/// </summary>
/// <remarks>
/// <para>
/// This deliberately does not derive from <c>Microsoft.Playwright.NUnit.PageTest</c>. That class
/// exposes an <c>IPage</c> property, which would put a Playwright type on the inherited surface of
/// every test class in the solution and defeat the layering this architecture exists to
/// demonstrate. C# allows only one base class, so the choice is mutually exclusive and has to be
/// made up front. See docs/adr/0002.
/// </para>
/// <para>
/// What is given up by not inheriting from it is small and mostly replaced here: worker-scoped
/// browser reuse (replaced by one shared browser plus a context per test) and the
/// <c>&lt;Playwright&gt;</c> .runsettings binding (replaced by appsettings.json, owned by the
/// Utility layer). Artifact capture is not given up, because those base classes never provided it.
/// </para>
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

        Session = await AutomationRuntime.SessionFactory.CreateSessionAsync();

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
            // The scope must be released even if artifact collection threw, or a failing test
            // leaks a scope and the real failure gets buried under a teardown error.
            _scope?.Dispose();
        }
    }
}
