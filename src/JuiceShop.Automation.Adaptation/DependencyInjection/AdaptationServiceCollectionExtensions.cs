using JuiceShop.Automation.Adaptation.Sut;
using JuiceShop.Automation.Utility.Sut;
using Microsoft.Extensions.DependencyInjection;

namespace JuiceShop.Automation.Adaptation.DependencyInjection;

/// <summary>Registers the Adaptation layer's integrations.</summary>
/// <remarks>
/// Each layer owns its registration extension, so a layer cannot reach the container without the
/// composition root referencing its project.
/// </remarks>
public static class AdaptationServiceCollectionExtensions
{
    /// <summary>Binds the ports declared in the Utility layer to their concrete adapters.</summary>
    public static IServiceCollection AddAdaptationLayer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISutReadinessGate, HttpSutReadinessGate>();

        return services;
    }
}
