using System.Diagnostics;
using System.Net.Http;
using JuiceShop.Automation.Utility.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuiceShop.Automation.Execution.Sut;

/// <summary>
/// Blocks the run until the system under test is actually serving requests.
/// </summary>
/// <remarks>
/// <para>
/// ISTQB CTAL-TAE 2016 §3.1.4 puts "set up the SUT for test execution" in the Test Execution layer.
/// This is the concrete instance of that, and it is the answer to the common objection that the
/// Execution layer is just a rebranded test runner: NUnit has no concept of the SUT at all.
/// </para>
/// <para>
/// It is deliberately redundant with the Compose healthcheck. The two cover different failure
/// modes: the healthcheck gates <c>docker compose up --wait</c>, but says nothing when a developer
/// starts the container by other means, points the suite at a remote environment, or runs
/// <c>dotnet test</c> in a second terminal while the container is still seeding. Juice Shop drops
/// and re-seeds its entire database on every boot, so the window where the port is open but the
/// application is not ready is tens of seconds wide — long enough to hit constantly, and the
/// resulting empty product lists and 500s look exactly like flaky tests.
/// </para>
/// </remarks>
public sealed class SutReadinessGate
{
    private readonly AutomationSettings _settings;
    private readonly ILogger<SutReadinessGate> _logger;

    public SutReadinessGate(IOptions<AutomationSettings> settings, ILogger<SutReadinessGate> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Polls the SUT's health endpoint until it responds successfully or the budget expires.
    /// </summary>
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
