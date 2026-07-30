using JuiceShop.Automation.Definition.TestData;
using Microsoft.Extensions.DependencyInjection;

namespace JuiceShop.Automation.Definition.DependencyInjection;

/// <summary>Registers the Definition layer.</summary>
/// <remarks>
/// Only test data is registered. Page objects are not: they are constructed by
/// <see cref="Flows.ShopFlow"/> from the session handed to it, because they are per-session objects
/// with a lifetime the container has no way to model — and because keeping them out of the container
/// keeps them internal to this assembly, which is what stops a test case from resolving one.
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
