using JuiceShop.Automation.Utility.Artifacts;
using JuiceShop.Automation.Utility.Driver;
using JuiceShop.Automation.Utility.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace JuiceShop.Automation.Utility.Testing;

/// <summary>
/// Base class for browser-driven tests: session lifecycle, per-test DI scope and artifact capture.
/// </summary>
/// <remarks>
/// <para>
/// Framework-level and application-agnostic — it knows about sessions and artifacts, not about
/// Juice Shop. The SUT-specific base class that exposes the business-action facade sits in the test
/// project and derives from this.
/// </para>
/// <para>
/// This deliberately does not derive from <c>Microsoft.Playwright.NUnit.PageTest</c>. That class
/// occupies C#'s single inheritance slot, which is a one-way decision once fixtures exist, and its
/// public <c>IPage</c> property would put a browser handle on the inherited surface of every test
/// class in the solution. What is given up is small and mostly replaced here; notably it is a
/// misconception that those base classes capture failure artifacts — reading their source shows no
/// tracing or screenshot code at all. See docs/adr/0002.
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
