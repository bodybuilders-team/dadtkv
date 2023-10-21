using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace Dadtkv;

/// <summary>
///     Logger for the Dadtkv system.
/// </summary>
public static class DadtkvLogger
{
    public static ILoggerFactory Factory;

    public static void InitializeLogger(string processId)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] - " + processId +
                                " - {SourceContext} - {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        // Create LoggerFactory and add Serilog
        Factory = LoggerFactory.Create(builder => { builder.AddSerilog(); });
    }
}