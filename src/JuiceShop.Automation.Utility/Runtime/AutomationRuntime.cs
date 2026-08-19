using JuiceShop.Automation.Utility.Artifacts;
using JuiceShop.Automation.Utility.Configuration;
using JuiceShop.Automation.Utility.DependencyInjection;
using JuiceShop.Automation.Utility.Driver;
using JuiceShop.Automation.Utility.Sut;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace JuiceShop.Automation.Utility.Runtime;

/// <summary>
/// Run lifecycle: builds the container, verifies the SUT is reachable, and owns the browser.
/// </summary>
/// <remarks>
/// <para>
/// NUnit has no host builder and no constructor injection, so the accepted pattern is one
/// <see cref="IServiceProvider"/> built once for the assembly and a scope taken per test. The
/// common alternative found in blog posts — building a provider in each fixture's constructor —
/// re-parses configuration once per fixture and leaks a provider each time.
/// </para>
/// <para>
/// The framework owns the sequence but not the contents: the caller supplies the layers it wants
/// registered through <c>configureServices</c>. That is what lets the run lifecycle live down here
/// while the composition root — the one place that legitimately knows about every layer at once —
/// stays at the top, in the test project.
/// </para>
/// <para>
/// Startup is split in two. <see cref="StartAsync"/> does only what is free — configuration, the
/// container, artifact housekeeping — and <see cref="EnsureBrowsingAsync"/> defers the run's two
/// external dependencies, the SUT readiness gate and the browser launch, until a test actually asks
/// for a session. The split is load-bearing rather than an optimisation: the assembly-level
/// <c>[SetUpFixture]</c> that calls <see cref="StartAsync"/> runs before <em>every</em> fixture, so
/// anything eager here is charged to tests that need none of it. The architecture rules are the case
/// that matters — they assert on IL metadata, and are documented to need no SUT and no browser, but
/// were failing after a 150s readiness timeout because this method waited for Juice Shop first.
/// </para>
/// </remarks>
public static class AutomationRuntime
{
    private static ServiceProvider? _services;
    private static ISessionFactory? _sessionFactory;
    private static Lazy<Task<ISessionFactory>>? _browsing;

    /// <summary>The run-scoped service provider.</summary>
    /// <exception cref="InvalidOperationException">Accessed before <see cref="StartAsync"/>.</exception>
    public static IServiceProvider Services =>
        _services ?? throw new InvalidOperationException(
            $"{nameof(AutomationRuntime)}.{nameof(StartAsync)} has not run. Ensure the test assembly " +
            "declares a namespace-less [SetUpFixture] that calls it.");

    /// <summary>
    /// Builds configuration and the container. Runs exactly once per test assembly, and deliberately
    /// touches nothing outside the process — see <see cref="EnsureBrowsingAsync"/>.
    /// </summary>
    /// <param name="configureServices">
    /// Registers the layers above the framework. The caller is the composition root.
    /// </param>
    /// <remarks>
    /// Synchronous now that the two awaits it used to hold have moved to
    /// <see cref="EnsureBrowsingAsync"/>, but still <see cref="Task"/>-returning: it is bound
    /// directly to an NUnit <c>[OneTimeSetUp]</c>, and nothing about the caller should have to
    /// change if a future step here needs to await again.
    /// </remarks>
    public static Task StartAsync(Action<IServiceCollection>? configureServices = null)
    {
        var configuration = ConfigurationFactory.Build();

        var services = new ServiceCollection();
        services.AddUtilityLayer(configuration, TestContext.CurrentContext.TestDirectory);
        configureServices?.Invoke(services);

        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        // ValidateOnStart only runs when something actually resolves the options, so resolve them
        // here. A typo in appsettings.json then fails the run immediately with a readable message
        // instead of surfacing as a null deep inside the first test.
        var settings = _services.GetRequiredService<IOptions<AutomationSettings>>().Value;

        // Artifacts must describe this run and only this run.
        _services.GetRequiredService<ArtifactPathProvider>().ClearPreviousRun();

        var logger = _services.GetRequiredService<ILogger<object>>();
        logger.LogInformation(
            "Starting run against {BaseUrl} using {Browser} (headless: {Headless}).",
            settings.Sut.BaseUrl,
            settings.Browser.Name,
            settings.Browser.Headless);

        _browsing = new Lazy<Task<ISessionFactory>>(
            StartBrowsingAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies the SUT is reachable and launches the browser, once per run, on first use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called from the browser test base rather than from setup, so that a run containing no browser
    /// test — the architecture rules, for instance — never contacts the SUT and never starts a
    /// browser process.
    /// </para>
    /// <para>
    /// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> is required, not defensive:
    /// fixtures run in parallel, so several of them reach this concurrently on the first test. It
    /// also means a <em>faulted</em> task is cached, which is the behaviour wanted when the SUT is
    /// down — the first test spends the readiness budget and every later one fails immediately with
    /// the same message, instead of each spending it again.
    /// </para>
    /// </remarks>
    public static Task<ISessionFactory> EnsureBrowsingAsync() =>
        (_browsing ?? throw new InvalidOperationException(
            $"{nameof(AutomationRuntime)}.{nameof(StartAsync)} has not run. Ensure the test assembly " +
            "declares a namespace-less [SetUpFixture] that calls it.")).Value;

    private static async Task<ISessionFactory> StartBrowsingAsync()
    {
        // Resolved through the port. The HTTP adapter lives in the Adaptation layer and is bound by
        // the composition root, so the framework never learns which protocol the SUT speaks.
        await Services.GetRequiredService<ISutReadinessGate>().WaitUntilReadyAsync();

        var sessionFactory = Services.GetRequiredService<ISessionFactory>();
        await sessionFactory.InitializeAsync();

        // Assigned only once the launch succeeded, so StopAsync never disposes a half-built factory.
        _sessionFactory = sessionFactory;
        return sessionFactory;
    }

    /// <summary>Disposes the browser and the container. Runs once after all tests.</summary>
    public static async Task StopAsync()
    {
        _browsing = null;

        if (_sessionFactory is not null)
        {
            await _sessionFactory.DisposeAsync();
            _sessionFactory = null;
        }

        if (_services is not null)
        {
            await _services.DisposeAsync();
            _services = null;
        }
    }
}
