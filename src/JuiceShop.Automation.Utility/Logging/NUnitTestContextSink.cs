using NUnit.Framework;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace JuiceShop.Automation.Utility.Logging;

/// <summary>
/// Serilog sink that routes log events into NUnit's per-test output channels.
/// </summary>
/// <remarks>
/// <para>
/// Written by hand rather than taken as a dependency: the community <c>Serilog.Sinks.NUnit</c>
/// package was last published in 2020 and targets NUnit 3.
/// </para>
/// <para>
/// It writes to both channels on purpose, because they do different jobs:
/// <list type="bullet">
/// <item><see cref="TestContext.Out"/> is captured and attributed to the currently executing test,
/// so it lands inside the .trx and in the per-test body of the Allure report. That is what makes a
/// failure self-explanatory to someone reading the report a week later.</item>
/// <item><see cref="TestContext.Progress"/> streams straight to the console, which is what you
/// actually watch during a CI run.</item>
/// </list>
/// <c>Console.WriteLine</c> is not a substitute for either: under parallel execution its output
/// interleaves across tests and is attributed to none of them.
/// </para>
/// </remarks>
internal sealed class NUnitTestContextSink : ILogEventSink
{
    private const string OutputTemplate =
        "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    private readonly MessageTemplateTextFormatter _formatter = new(OutputTemplate);

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        // Outside a test context (for example during assembly-level teardown) there is nowhere
        // meaningful to write, and touching TestContext would throw.
        if (TestContext.CurrentContext?.Test.ID is null)
        {
            return;
        }

        using var writer = new StringWriter();
        _formatter.Format(logEvent, writer);
        var rendered = writer.ToString();

        try
        {
            TestContext.Out.Write(rendered);
            TestContext.Progress.Write(rendered);
        }
        catch (ObjectDisposedException)
        {
            // The test's output writer is torn down slightly before the last async log events
            // drain. Losing a trailing log line is preferable to failing an otherwise good test.
        }
    }
}
