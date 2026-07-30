namespace JuiceShop.Automation.Utility.Sut;

/// <summary>
/// Port through which the run lifecycle establishes that the system under test is serving requests.
/// </summary>
/// <remarks>
/// <para>
/// Declared here and implemented in the Adaptation layer. That split is what lets the Utility layer
/// own the run lifecycle — which must not start tests against an application that is still
/// booting — without taking a dependency on the integration that talks to it. The concrete adapter
/// is bound at the composition root in the Execution layer.
/// </para>
/// <para>
/// The alternative, putting the HTTP call directly in the lifecycle code, would drag an external
/// protocol into the framework layer and make Utility unusable against a SUT that is reached any
/// other way.
/// </para>
/// </remarks>
public interface ISutReadinessGate
{
    /// <summary>
    /// Blocks until the SUT responds successfully, or throws once the configured budget is spent.
    /// </summary>
    Task WaitUntilReadyAsync(CancellationToken cancellationToken = default);
}
