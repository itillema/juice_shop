using JuiceShop.Automation.Adaptation.Sut;
using JuiceShop.Automation.Utility.Sut;
using Microsoft.Extensions.DependencyInjection;

namespace JuiceShop.Automation.Adaptation.DependencyInjection;

/// <summary>Registers the Adaptation layer's integrations.</summary>
/// <remarks>
/// Each layer owns its own registration extension and the composition root calls them. That keeps
/// the wiring honest — a layer cannot appear in the container without its project being referenced
/// by the composition root, so the reference graph in the .csproj files is the real one.
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
