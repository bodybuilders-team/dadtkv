using System.Diagnostics;

namespace DADTKV;

internal static class SystemManager
{
    // Entry point for the DADTKV system
    // Arguments: systemConfigFilePath
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
        
        // Start DADTKV servers (Transaction Managers, Lease Managers)
        StartServers(configuration, configurationFile);
        return;
        Thread.Sleep(5000);

        // Start DADTKV clients
        StartClients(configuration);

        // Wait for user input to shut down the system
        Console.WriteLine("Press Enter to shut down the DADTKV system.");
        Console.ReadLine();

        // TODO: Stop DADTKV processes gracefully?
    }

    private static void StartServers(SystemConfiguration config, string configurationFile)
    {
        var solutionDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName;
        var leaseManagerExePath = Path.Combine(solutionDirectory, "DADTKVLeaseManager/bin/Debug/net6.0/DADTKVLeaseManager.exe");
        var transactionManagerExePath = Path.Combine(solutionDirectory, "DADTKVTransactionManager/bin/Debug/net6.0/DADTKVTransactionManager.exe");
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
            return;
        }
    }

    private static void StartClients(SystemConfiguration config)
    {
        foreach (var client in config.Clients)
        {
            Console.WriteLine($"Starting client {client.Id} at {client.Url}");

            Process.Start(new ProcessStartInfo
            {
                FileName = "DADTKVClient.exe",
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