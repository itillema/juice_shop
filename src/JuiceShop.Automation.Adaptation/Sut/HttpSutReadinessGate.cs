using System.Diagnostics;
using System.Net.Http;
using JuiceShop.Automation.Utility.Configuration;
using JuiceShop.Automation.Utility.Sut;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuiceShop.Automation.Adaptation.Sut;

/// <summary>
/// Establishes SUT readiness over HTTP.
/// </summary>
/// <remarks>
/// <para>
/// This is an integration with an external service, spoken over a protocol, which is what makes it
/// an Adaptation concern rather than a framework one. The layer above knows only
/// <see cref="ISutReadinessGate"/>; swapping this for a gRPC health check, a database probe or a
/// message-queue ping would touch no other project.
/// </para>
/// <para>
/// It is deliberately redundant with the Compose healthcheck. The two cover different failure
/// modes: the healthcheck gates <c>docker compose up --wait</c>, but says nothing when a developer
/// starts the container by other means, points the suite at a remote environment, or runs
/// <c>dotnet test</c> in a second terminal while the container is still seeding. Juice Shop drops
/// and re-seeds its entire database on every boot, so the window in which the port is open but the
/// application is not ready is tens of seconds wide — long enough to hit constantly, and the
/// resulting empty product lists and 500s look exactly like flaky tests.
/// </para>
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
