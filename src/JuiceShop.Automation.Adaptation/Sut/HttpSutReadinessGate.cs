using System.Diagnostics;
using System.Net.Http;
using JuiceShop.Automation.Utility.Configuration;
using JuiceShop.Automation.Utility.Sut;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuiceShop.Automation.Adaptation.Sut;

/// <summary>Establishes SUT readiness over HTTP.</summary>
/// <remarks>
/// Deliberately redundant with the Compose healthcheck, which says nothing about a container started
/// by other means or a remote environment. See docs/adr/0004.
/// </remarks>
public sealed class HttpSutReadinessGate : ISutReadinessGate
{
    private readonly AutomationSettings _settings;
    private readonly ILogger<HttpSutReadinessGate> _logger;

    public HttpSutReadinessGate(IOptions<AutomationSettings> settings, ILogger<HttpSutReadinessGate> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The SUT did not become ready in time.</exception>
    public async Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
    {
        var sut = _settings.Sut;
        var probeUrl = new Uri(new Uri(sut.BaseUrl), sut.HealthPath);
        var budget = TimeSpan.FromSeconds(sut.ReadinessTimeoutSeconds);
        var interval = TimeSpan.FromSeconds(sut.ReadinessPollIntervalSeconds);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        var stopwatch = Stopwatch.StartNew();
        var attempt = 0;
        string? lastFailure = null;

        _logger.LogInformation("Waiting for the system under test at {ProbeUrl}.", probeUrl);

        while (stopwatch.Elapsed < budget)
        {
            attempt++;

            try
            {
                using var response = await client.GetAsync(probeUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "System under test is ready after {ElapsedSeconds:F1}s ({Attempts} attempts).",
                        stopwatch.Elapsed.TotalSeconds,
                        attempt);
                    return;
                }

                lastFailure = $"HTTP {(int)response.StatusCode}";
            }
            catch (HttpRequestException exception)
            {
                lastFailure = exception.Message;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastFailure = "request timed out";
            }

            await Task.Delay(interval, cancellationToken);
        }

        throw new InvalidOperationException(
            $"The system under test at {probeUrl} did not become ready within " +
            $"{sut.ReadinessTimeoutSeconds}s (last failure: {lastFailure ?? "none recorded"}). " +
            "Start it with `docker compose up -d --wait`, or point Automation:Sut:BaseUrl at a " +
            "running instance.");
    }
}
