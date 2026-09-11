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
/// Run lifecycle: one container for the assembly, a scope per test, and the shared browser.
/// </summary>
/// <remarks>See docs/architecture.md — "The cycle, and how it is broken".</remarks>
public static class AutomationRuntime
{
    private static ServiceProvider? _services;
    private static ISessionFactory? _sessionFactory;
    private static Lazy<Task<ISessionFactory>>? _browsing;

    private const string NotStarted =
        $"{nameof(AutomationRuntime)}.{nameof(StartAsync)} has not run. Ensure the test assembly " +
        "declares a namespace-less [SetUpFixture] that calls it.";

    /// <summary>The run-scoped service provider.</summary>
    public static IServiceProvider Services =>
        _services ?? throw new InvalidOperationException(NotStarted);

    /// <summary>Builds configuration and the container. Touches nothing outside the process.</summary>
    /// <param name="configureServices">Registers the layers above the framework.</param>
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

        // ValidateOnStart registers an IStartupValidator that only an IHost would invoke, and a test
        // run has no host — so the run lifecycle invokes it. Resolving the options below would also
        // validate; this says so outright rather than leaving it to a side effect of the log line.
        _services.GetRequiredService<IStartupValidator>().Validate();

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

        // Still Task-returning: bound directly to an NUnit [OneTimeSetUp].
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits for the SUT and launches the browser, once per run, on first session use.
    /// </summary>
    /// <remarks>
    /// Deferred so a run with no browser test — the architecture rules — needs neither.
    /// ExecutionAndPublication also caches a faulted task, so with the SUT down only the first
    /// test spends the readiness budget.
    /// </remarks>
    public static Task<ISessionFactory> EnsureBrowsingAsync() =>
        (_browsing ?? throw new InvalidOperationException(NotStarted)).Value;

    private static async Task<ISessionFactory> StartBrowsingAsync()
    {
        // Through the port, so the framework never learns which protocol the SUT speaks.
        await Services.GetRequiredService<ISutReadinessGate>().WaitUntilReadyAsync();

        var sessionFactory = Services.GetRequiredService<ISessionFactory>();
        await sessionFactory.InitializeAsync();

        // Assigned only on success, so StopAsync never disposes a half-built factory.
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
