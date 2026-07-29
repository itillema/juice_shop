using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace JuiceShop.Automation.Utility.Configuration;

/// <summary>
/// Builds the configuration root for a test run.
/// </summary>
public static class ConfigurationFactory
{
    /// <summary>Environment variable selecting which appsettings overlay to apply.</summary>
    public const string EnvironmentVariableName = "AUTOMATION_ENVIRONMENT";

    /// <summary>
    /// Builds configuration from appsettings.json, an optional per-environment overlay, and
    /// environment variables — in that precedence order, last wins.
    /// </summary>
    /// <remarks>
    /// Two details here are load-bearing and easy to get wrong:
    /// <list type="bullet">
    /// <item>
    /// The base path is <see cref="TestContext"/>'s TestDirectory, not the current working
    /// directory. Several runners set the working directory to the solution root rather than the
    /// binary's folder, in which case the JSON file is simply not found and every setting silently
    /// falls back to its default.
    /// </item>
    /// <item>
    /// <c>reloadOnChange</c> is false. It spawns a FileSystemWatcher per file, which leaks and can
    /// hang test hosts on Linux containers, and configuration cannot usefully change mid-run anyway.
    /// </item>
    /// </list>
    /// Environment overrides use a double underscore for nesting:
    /// <c>AUTOMATION__SUT__BASEURL=http://juice-shop:3000</c>. The colon form does not work on Linux.
    /// </remarks>
    public static IConfigurationRoot Build()
    {
        var environment = Environment.GetEnvironmentVariable(EnvironmentVariableName) ?? "Local";

        return new ConfigurationBuilder()
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }
}
