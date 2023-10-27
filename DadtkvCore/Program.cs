using Microsoft.Extensions.Logging;

namespace Dadtkv;

internal static class Program
{
    private const int WallTimeSecondsToPass = 10;

    /// <summary>
    ///     Entry point for the Dadtkv system.
    /// </summary>
    /// <param name="args">Arguments: systemConfigFilePath (relative to solution)</param>
    private static void Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Invalid arguments. Usage: DadtkvCore.exe systemConfigFilePath");

        DadtkvLogger.InitializeLogger("SystemManager");
        var logger = DadtkvLogger.Factory.CreateLogger<SystemManager>();

        var systemManager = new SystemManager();

        // Read the system configuration file
        var configurationFile = Path.Combine(Environment.CurrentDirectory, args[0]);
        var systemConfiguration = SystemConfiguration.ReadSystemConfiguration(configurationFile);

        if (systemConfiguration == null)
            throw new Exception($"Failed to read system configuration file at {configurationFile}");

        // Start Dadtkv servers (Transaction Managers, Lease Managers)
        systemManager.StartServers(systemConfiguration, configurationFile,
            DateTime.Now.AddSeconds(WallTimeSecondsToPass));
        Thread.Sleep(2000);

        // Start Dadtkv clients
        systemManager.StartClients(systemConfiguration);

        Thread.Sleep(1000);
        logger.LogInformation(systemManager.IsRunning() ? "Dadtkv system started" : "Failed to start Dadtkv system");

        // Wait for user input to shut down the system
        Console.WriteLine("Press Enter to shut down the Dadtkv system.");
        Console.ReadLine();
        logger.LogInformation("Shutting down the Dadtkv system...");
        systemManager.ShutDown();
    }
}