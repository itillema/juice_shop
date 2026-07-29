using JuiceShop.Automation.Utility.Artifacts;
using JuiceShop.Automation.Utility.Configuration;
using JuiceShop.Automation.Utility.Logging;
using JuiceShop.Automation.Utility.TestData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Extensions.Logging;

namespace JuiceShop.Automation.Utility.DependencyInjection;

/// <summary>
/// Registers the Utility layer: configuration, logging and test data.
/// </summary>
/// <remarks>
/// Each layer owns its own registration extension and the Execution layer composes them. That
/// keeps the composition root honest — a layer cannot be wired into the container without its
/// own project being referenced, so the dependency graph in the .csproj files is the real one.
/// </remarks>
public static class UtilityServiceCollectionExtensions
{
    /// <summary>Adds configuration binding, logging and test-data services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration root, normally from <see cref="ConfigurationFactory"/>.</param>
    /// <param name="baseDirectory">Test binary directory, used to resolve relative artifact paths.</param>
    public static IServiceCollection AddUtilityLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        services.AddSingleton(configuration);

        // ValidateOnStart turns a malformed appsettings.json into an immediate, readable failure
        // instead of a NullReferenceException somewhere in the middle of the first test.
        services
            .AddOptions<AutomationSettings>()
            .Bind(configuration.GetSection(AutomationSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(provider => new ArtifactPathProvider(
            provider.GetRequiredService<IOptions<AutomationSettings>>(),
            baseDirectory));

        services.AddSingleton(provider =>
        {
            var paths = provider.GetRequiredService<ArtifactPathProvider>();
            return LoggerFactoryBuilder.Build(paths.LogDirectory);
        });

        services.AddSingleton<ILoggerFactory>(provider =>
            new SerilogLoggerFactory(provider.GetRequiredService<Serilog.Core.Logger>(), dispose: true));

        services.AddLogging();

        services.AddSingleton<RegistrationDataFactory>();

        return services;
    }
}
