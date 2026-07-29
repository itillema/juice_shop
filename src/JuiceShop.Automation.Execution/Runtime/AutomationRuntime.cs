using JuiceShop.Automation.Adaptation.Contracts;
using JuiceShop.Automation.Adaptation.DependencyInjection;
using JuiceShop.Automation.Execution.DependencyInjection;
using JuiceShop.Automation.Execution.Sut;
using JuiceShop.Automation.Utility.Artifacts;
using JuiceShop.Automation.Utility.Configuration;
using JuiceShop.Automation.Utility.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace JuiceShop.Automation.Execution.Runtime;

/// <summary>
/// Run-scoped composition root: configuration, container, SUT readiness and the shared browser.
/// </summary>
/// <remarks>
/// <para>
/// NUnit has no host builder and no constructor injection, so the accepted pattern is one
/// <see cref="IServiceProvider"/> built once for the assembly and a scope taken per test. The
/// common alternative found in blog posts — building a provider in each fixture's constructor —
/// re-parses configuration once per fixture and leaks a provider each time.
/// </para>
/// <para>
/// The startup logic lives here rather than in the <c>[SetUpFixture]</c> itself because NUnit only
/// discovers setup fixtures in the assembly under test. The test project therefore carries a thin
/// fixture that delegates to this type, and everything substantial stays in the Execution layer.
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
    /// Builds configuration and the container, verifies the SUT is reachable, and launches the
    /// browser. Runs exactly once per test assembly.
    /// </summary>
    public static async Task StartAsync()
    {
        var configuration = ConfigurationFactory.Build();

        var services = new ServiceCollection();
        services.AddUtilityLayer(configuration, TestContext.CurrentContext.TestDirectory);
        services.AddAdaptationLayer();
        services.AddExecutionLayer();

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

        await _services.GetRequiredService<SutReadinessGate>().WaitUntilReadyAsync();

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
