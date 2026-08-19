using NUnit.Framework;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace JuiceShop.Automation.Utility.Logging;

/// <summary>Serilog sink routing log events into NUnit's per-test output channels.</summary>
/// <remarks>
/// Hand-rolled because Serilog.Sinks.NUnit was last published in 2020 and targets NUnit 3.
/// Both channels are written: <c>Out</c> is attributed to the test and lands in the .trx and Allure
/// report; <c>Progress</c> streams to the console. <c>Console.WriteLine</c> replaces neither — under
/// parallel execution its output interleaves and is attributed to no test.
/// </remarks>
internal sealed class NUnitTestContextSink : ILogEventSink
{
    private const string OutputTemplate =
        "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    private readonly MessageTemplateTextFormatter _formatter = new(OutputTemplate);

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        // Outside a test context — assembly teardown, say — touching TestContext would throw.
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
            // The writer is torn down just before the last async log events drain. Losing a
            // trailing line beats failing a good test.
        }
    }
}
