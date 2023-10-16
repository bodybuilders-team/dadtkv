using System.Diagnostics;

namespace Dadtkv;

/// <summary>
///     The system manager is responsible for starting the Dadtkv system.
/// </summary>
internal static class SystemManager
{
    /// <summary>
    ///     Entry point for the Dadtkv system.
    /// </summary>
    /// <param name="args">Arguments: systemConfigFilePath</param>
    private static void Main(string[] args)
    {
        // Read the system configuration file
        /*var configurationFilePath = "Configuration/configuration_sample.txt";//args[0];
        var configurationFile = Path.Combine(Environment.CurrentDirectory, configurationFilePath);
        var configuration = SystemConfiguration.ReadSystemConfiguration(configurationFile);*/
        var configurationFilePath = "Configuration/configuration_sample.txt";
        var projectDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
        var configurationFile = Path.Combine(projectDirectory, configurationFilePath);
        var configuration = SystemConfiguration.ReadSystemConfiguration(configurationFile);

        if (configuration == null)
        {
            Console.WriteLine($"Failed to read system configuration file at {configurationFile}");
            return;
        }

        // Start Dadtkv servers (Transaction Managers, Lease Managers)
        StartServers(configuration, configurationFile);
        return;
        Thread.Sleep(5000);

        // Start Dadtkv clients
        StartClients(configuration);

        // Wait for user input to shut down the system
        Console.WriteLine("Press Enter to shut down the Dadtkv system.");
        Console.ReadLine();
    }

    /// <summary>
    ///     Starts the Dadtkv servers (Transaction Managers, Lease Managers).
    /// </summary>
    /// <param name="config">The system configuration.</param>
    /// <param name="configurationFile">The system configuration file.</param>
    private static void StartServers(SystemConfiguration config, string configurationFile)
    {
        var solutionDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName;
        var leaseManagerExePath =
            Path.Combine(solutionDirectory, "DadtkvLeaseManager/bin/Debug/net6.0/DadtkvLeaseManager.exe");
        var transactionManagerExePath = Path.Combine(solutionDirectory,
            "Dadtkv/bin/Debug/net6.0/Dadtkv.exe");
        Console.WriteLine(configurationFile);

        foreach (var process in config.ServerProcesses)
        {
            Console.WriteLine($"Starting {process.Role} {process.Id} at {process.Url}");

            var fileName = process.Role == "L" ? leaseManagerExePath : transactionManagerExePath;
            Console.WriteLine(fileName);

            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                ArgumentList = { process.Id, configurationFile }
            });
        }
    }

    /// <summary>
    ///     Starts the Dadtkv clients.
    /// </summary>
    /// <param name="config">The system configuration.</param>
    private static void StartClients(SystemConfiguration config)
    {
        foreach (var client in config.Clients)
        {
            Console.WriteLine($"Starting client {client.Id} at {client.Url}");

            Process.Start(new ProcessStartInfo
            {
                FileName = "DadtkvClient.exe",
                ArgumentList =
                {
                    config.TransactionManagers[0].Url!, // TODO: Ip lookup? Load balancing?
                    client.Id
                    // TODO: client.ScriptFilePath
                }
            });
        }
    }
}