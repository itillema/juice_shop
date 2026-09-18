using JuiceShop.Automation.Definition.TestData;
using Microsoft.Extensions.DependencyInjection;

namespace JuiceShop.Automation.Definition.DependencyInjection;

/// <summary>Registers the Definition layer.</summary>
/// <remarks>
/// Test data only. Page objects are built per-session by <see cref="Flows.ShopFlow"/>; keeping them out of the container is what stops a test case resolving one.
/// </remarks>
public static class DefinitionServiceCollectionExtensions
{
    public static IServiceCollection AddDefinitionLayer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<RegistrationDataFactory>();

        return services;
    }
}
