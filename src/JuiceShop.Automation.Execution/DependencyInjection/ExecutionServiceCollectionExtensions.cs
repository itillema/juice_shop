using JuiceShop.Automation.Execution.Artifacts;
using JuiceShop.Automation.Execution.Sut;
using Microsoft.Extensions.DependencyInjection;

namespace JuiceShop.Automation.Execution.DependencyInjection;

/// <summary>Registers the Execution layer.</summary>
public static class ExecutionServiceCollectionExtensions
{
    /// <summary>Adds SUT lifecycle management and artifact collection.</summary>
    public static IServiceCollection AddExecutionLayer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SutReadinessGate>();
        services.AddScoped<ArtifactCollector>();

        return services;
    }
}
