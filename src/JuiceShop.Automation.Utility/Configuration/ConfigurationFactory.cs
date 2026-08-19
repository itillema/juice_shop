using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace JuiceShop.Automation.Utility.Configuration;

/// <summary>Builds the configuration root for a test run.</summary>
public static class ConfigurationFactory
{
    /// <summary>Environment variable selecting which appsettings overlay to apply.</summary>
    public const string EnvironmentVariableName = "AUTOMATION_ENVIRONMENT";

    /// <summary>appsettings.json, then the environment overlay, then environment variables.</summary>
    /// <remarks>Nesting uses a double underscore: <c>AUTOMATION__SUT__BASEURL</c>. Colons fail on Linux.</remarks>
    public static IConfigurationRoot Build()
    {
        var environment = Environment.GetEnvironmentVariable(EnvironmentVariableName) ?? "Local";

        return new ConfigurationBuilder()
            // TestDirectory, not the working directory — some runners set the latter to the repo root.
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            // reloadOnChange spawns a FileSystemWatcher per file, which can hang Linux test hosts.
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }
}
