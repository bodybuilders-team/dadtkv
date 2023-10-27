using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

/// <summary>
///     The system manager is responsible for starting the Dadtkv system.
/// </summary>
internal class SystemManager
{
    private readonly ILogger<SystemManager> _logger = DadtkvLogger.Factory.CreateLogger<SystemManager>();
    private readonly List<Process> _processes = new();

    /// <summary>
    ///     Starts the Dadtkv servers (Transaction Managers, Lease Managers).
    /// </summary>
    /// <param name="config">The system configuration.</param>
    /// <param name="configurationFile">The system configuration file.</param>
    public void StartServers(SystemConfiguration config, string configurationFile, DateTime WallTime)
    {
        StartServers(config.LeaseManagers, configurationFile, WallTime);
        Thread.Sleep(200);
        StartServers(config.TransactionManagers, configurationFile, WallTime);
    }

    private void StartServers(List<ServerProcessInfo> serverProcesses, string configurationFile, DateTime WallTime)
    {
        //var solutionDirectory = Directory.GetParent(Directory.GetCurrentDirectory())!.Parent!.Parent!.Parent!.FullName;
        var leaseManagerExePath =
            Path.Combine(Directory.GetCurrentDirectory(), "DadtkvLeaseManager/bin/Debug/net6.0/DadtkvLeaseManager.exe");
        var transactionManagerExePath = Path.Combine(Directory.GetCurrentDirectory(),
            "DadtkvTransactionManager/bin/Debug/net6.0/DadtkvTransactionManager.exe");

        foreach (var process in serverProcesses)
        {
            var wallTime = WallTime.ToString(CultureInfo.CurrentCulture);

            _logger.LogInformation(
                $"Starting {process.Role} {process.Id} at {process.Url}. Passing wall time {wallTime}");
            var fileName = process.Role.Equals("L") ? leaseManagerExePath : transactionManagerExePath;

            var p = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                ArgumentList = { process.Id, configurationFile, wallTime }
            }) ?? throw new Exception("Failed to start server process: " + process.Id);
            _processes.Add(p);
        }
    }

    /// <summary>
    ///     Starts the Dadtkv clients.
    /// </summary>
    /// <param name="config">The system configuration.</param>
    public void StartClients(SystemConfiguration config)
    {
        var solutionDirectory = Directory.GetParent(Directory.GetCurrentDirectory())!.Parent!.Parent!.Parent!.FullName;
        var clientExePath =
            Path.Combine(Directory.GetCurrentDirectory(), "DadtkvClient/bin/Debug/net6.0/DadtkvClient.exe");
        var clientScriptsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "DadtkvClient/Script");

        var tmCount = 0;

        foreach (var client in config.Clients)
        {
            _logger.LogInformation($"Starting client {client.Id} with script {client.Script}");

            var p = Process.Start(new ProcessStartInfo
            {
                FileName = clientExePath,
                ArgumentList =
                {
                    config.TransactionManagers[tmCount++ % config.TransactionManagers.Count].Url,
                    client.Id,
                    Path.Combine(clientScriptsDirectory, client.Script + ".txt")
                }
            }) ?? throw new Exception("Failed to start client process: " + client.Id);
            _processes.Add(p);
        }
    }

    /// <summary>
    ///     Shuts down the Dadtkv system.
    /// </summary>
    public void ShutDown()
    {
        foreach (var process in _processes)
            process.Kill();
    }

    /// <summary>
    ///     Checks if the Dadtkv system is running.
    /// </summary>
    /// <returns>True if the Dadtkv system is running, false otherwise.</returns>
    public bool IsRunning()
    {
        return _processes.All(process => !process.HasExited);
    }
}