using Serilog;
using Serilog.Events;

namespace JuiceShop.Automation.Utility.Logging;

/// <summary>Builds the Serilog logger behind <c>ILogger&lt;T&gt;</c>.</summary>
/// <remarks>Serilog is the implementation; the rest of the solution codes against the abstraction.</remarks>
public static class LoggerFactoryBuilder
{
    /// <summary>Creates the run-wide Serilog logger.</summary>
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
