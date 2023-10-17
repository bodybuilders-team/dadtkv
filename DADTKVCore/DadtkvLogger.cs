using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace Dadtkv;

/// <summary>
///     Logger for the Dadtkv system.
/// </summary>
public static class DadtkvLogger
{
    public static readonly ILoggerFactory Factory;

    static DadtkvLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(theme: AnsiConsoleTheme.Code)
            .WriteTo.File("logs.txt")
            .CreateLogger();

        // Create LoggerFactory and add Serilog
        Factory = LoggerFactory.Create(builder => { builder.AddSerilog(); });
    }
}