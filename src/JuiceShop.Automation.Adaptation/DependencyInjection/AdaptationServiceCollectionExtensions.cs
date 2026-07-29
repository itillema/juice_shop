using JuiceShop.Automation.Adaptation.Contracts;
using JuiceShop.Automation.Adaptation.Playwright;
using Microsoft.Extensions.DependencyInjection;

namespace JuiceShop.Automation.Adaptation.DependencyInjection;

/// <summary>Registers the Adaptation layer.</summary>
public static class AdaptationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the session factory. The concrete Playwright implementation stays internal — callers
    /// only ever resolve <see cref="ISessionFactory"/>.
    /// </summary>
    public static IServiceCollection AddAdaptationLayer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Singleton because it owns the one browser process for the run. Sessions themselves are
        // created explicitly per test rather than resolved, because creating one is asynchronous
        // and DI has no async activation.
        services.AddSingleton<ISessionFactory, PlaywrightSessionFactory>();

        return services;
    }
}
