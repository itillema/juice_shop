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
/// </remarks>
public static class AutomationRuntime
{
    private static ServiceProvider? _services;
    private static ISessionFactory? _sessionFactory;

    /// <summary>The run-scoped service provider.</summary>
    /// <exception cref="InvalidOperationException">Accessed before <see cref="StartAsync"/>.</exception>
    public static IServiceProvider Services =>
        _services ?? throw new InvalidOperationException(
            $"{nameof(AutomationRuntime)}.{nameof(StartAsync)} has not run. Ensure the test assembly " +
            "declares a namespace-less [SetUpFixture] that calls it.");

    /// <summary>The shared session factory.</summary>
    public static ISessionFactory SessionFactory =>
        _sessionFactory ?? throw new InvalidOperationException(
            $"{nameof(AutomationRuntime)}.{nameof(StartAsync)} has not run.");

    /// <summary>
    /// Builds configuration and the container, verifies the SUT is ready, and launches the browser.
    /// Runs exactly once per test assembly.
    /// </summary>
    /// <param name="configureServices">
    /// Registers the layers above the framework. The caller is the composition root.
    /// </param>
    public static async Task StartAsync(Action<IServiceCollection>? configureServices = null)
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

        // Resolved through the port. The HTTP adapter lives in the Adaptation layer and is bound by
        // the composition root, so the framework never learns which protocol the SUT speaks.
        await _services.GetRequiredService<ISutReadinessGate>().WaitUntilReadyAsync();

        _sessionFactory = _services.GetRequiredService<ISessionFactory>();
        await _sessionFactory.InitializeAsync();
    }

    /// <summary>Disposes the browser and the container. Runs once after all tests.</summary>
    public static async Task StopAsync()
    {
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
