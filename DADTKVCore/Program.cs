using Microsoft.Extensions.Logging;

namespace Dadtkv;

internal static class Program
{
    private static ILogger<SystemManager> Logger;

    private const int WallTimeSecondsToPass = 10;

    /// <summary>
    ///     Entry point for the Dadtkv system.
    /// </summary>
    /// <param name="args">Arguments: systemConfigFilePath (relative to the project directory).</param>
    private static void Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Invalid arguments. Usage: DadtkvCore.exe systemConfigFilePath");

        DadtkvLogger.InitializeLogger("SystemManager");
        Logger = DadtkvLogger.Factory.CreateLogger<SystemManager>();

        var systemManager = new SystemManager();

        // Read the system configuration file
        var configurationFilePath = args[0];
        var projectDirectory = Directory.GetParent(Directory.GetCurrentDirectory())!.Parent!.Parent!.FullName;
        var configurationFile = Path.Combine(projectDirectory, configurationFilePath);
        var configuration = SystemConfiguration.ReadSystemConfiguration(configurationFile);

        if (configuration == null)
            throw new Exception($"Failed to read system configuration file at {configurationFile}");

        // Start Dadtkv servers (Transaction Managers, Lease Managers)
        systemManager.StartServers(configuration, configurationFile, DateTime.Now.AddSeconds(WallTimeSecondsToPass));
        Thread.Sleep(2000);

        // Start Dadtkv clients
        systemManager.StartClients(configuration);

        Thread.Sleep(1000);
        Logger.LogInformation(systemManager.IsRunning() ? "Dadtkv system started" : "Failed to start Dadtkv system");

        // Wait for user input to shut down the system
        Console.WriteLine("Press Enter to shut down the Dadtkv system.");
        Console.ReadLine();
        Logger.LogInformation("Shutting down the Dadtkv system...");
        systemManager.ShutDown();
    }
}