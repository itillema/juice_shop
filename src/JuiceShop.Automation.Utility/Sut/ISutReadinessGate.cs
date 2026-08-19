namespace JuiceShop.Automation.Utility.Sut;

/// <summary>Port through which the run lifecycle establishes that the SUT is serving.</summary>
/// <remarks>
/// Declared here, implemented in Adaptation, bound in Execution. That split lets Utility own the run
/// lifecycle without depending on the integration that talks to the SUT. See docs/adr/0001.
/// </remarks>
public interface ISutReadinessGate
{
    /// <summary>Blocks until the SUT responds, or throws once the configured budget is spent.</summary>
    Task WaitUntilReadyAsync(CancellationToken cancellationToken = default);
}
