using Serilog;
using Serilog.Events;

namespace JuiceShop.Automation.Utility.Logging;

/// <summary>
/// Builds the Serilog logger that sits behind <c>ILogger&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// Serilog is the implementation; Microsoft.Extensions.Logging is the abstraction the rest of the
/// solution codes against. That split means the Adaptation layer's page objects depend only on
/// <c>ILogger&lt;T&gt;</c> and would survive swapping the logging backend.
/// </remarks>
public static class LoggerFactoryBuilder
{
    /// <summary>Creates the run-wide Serilog logger.</summary>
    /// <param name="logDirectory">Directory for the rolling run log.</param>
    /// <param name="minimumLevel">Minimum level to emit.</param>
    public static Serilog.Core.Logger Build(string logDirectory, LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        Directory.CreateDirectory(logDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.Sink(new NUnitTestContextSink())
            .WriteTo.File(
                path: Path.Combine(logDirectory, "run-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();
    }
}
