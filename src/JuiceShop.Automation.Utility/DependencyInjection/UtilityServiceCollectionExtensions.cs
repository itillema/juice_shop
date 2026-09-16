using JuiceShop.Automation.Utility.Artifacts;
using JuiceShop.Automation.Utility.Configuration;
using JuiceShop.Automation.Utility.Driver;
using JuiceShop.Automation.Utility.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Extensions.Logging;

namespace JuiceShop.Automation.Utility.DependencyInjection;

/// <summary>Registers the framework: configuration, logging, driver and artifact capture.</summary>
public static class UtilityServiceCollectionExtensions
{
    /// <summary>Adds the framework services.</summary>
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

        // Turns a malformed appsettings.json, or a nonsense environment override, into an immediate
        // and readable failure. NOT ValidateDataAnnotations(): it validates the root object only, so
        // every attribute on the nested sections would go unchecked. See AutomationSettingsValidator.
        services
            .AddOptions<AutomationSettings>()
            .Bind(configuration.GetSection(AutomationSettings.SectionName))
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<AutomationSettings>, AutomationSettingsValidator>());

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

        // One browser for the run; one context per test. See ISessionFactory.
        services.AddSingleton<ISessionFactory, PlaywrightSessionFactory>();

        services.AddScoped<ArtifactCollector>();

        return services;
    }
}
