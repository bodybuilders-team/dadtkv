using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

internal static class Program
{
    /// <summary>
    ///     Entry point for the lease manager server application.
    /// </summary>
    /// <param name="args">Arguments: serverId systemConfigFilePath</param>
    /// <exception cref="ArgumentException">Invalid arguments.</exception>
    public static void Main(string[] args)
    {
        if (args.Length != 3)
            throw new ArgumentException(
                "Invalid arguments. Usage: DadtkvTransactionManager.exe serverId systemConfigFilePath wallTime");

        var serverId = args[0];

        DadtkvLogger.InitializeLogger(serverId);
        var logger = DadtkvLogger.Factory.CreateLogger<DadtkvServiceImpl>();
        var wallTime = args.Length == 3 ? args[2] : null;

        var configurationFile = Path.Combine(Environment.CurrentDirectory, args[1]);
        var systemConfiguration = SystemConfiguration.ReadSystemConfiguration(configurationFile);

        var processConfiguration = new ServerProcessConfiguration(systemConfiguration, serverId);
        var serverProcessPort = new Uri(processConfiguration.ProcessInfo.Url).Port;
        var hostname = new Uri(processConfiguration.ProcessInfo.Url).Host;

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        LeaseQueues leaseQueues = new();
        var datastore = new DataStore();
        var executedTx = new Dictionary<LeaseId, bool>();
        var abortedTrans = new HashSet<LeaseId>();

        var server = new Server
        {
            Services =
            {
                DadtkvService.BindService(new DadtkvServiceImpl(processConfiguration, datastore, executedTx,
                    leaseQueues, abortedTrans)),
                StateUpdateService.BindService(new StateUpdateServiceImpl(processConfiguration, datastore,
                    leaseQueues, abortedTrans)),
                LearnerService.BindService(new TmLearner(processConfiguration, executedTx, leaseQueues))
            },
            Ports = { new ServerPort(hostname, serverProcessPort, ServerCredentials.Insecure) }
        };

        var actualWallTime = wallTime != null ? DateTime.Parse(wallTime) : processConfiguration.WallTime;

        while (DateTime.Now < actualWallTime) Thread.Sleep(10);

        processConfiguration.StartTimer();
        server.Start();

        logger.LogInformation($"Transaction Manager server listening on port {serverProcessPort}");

        while (processConfiguration.CurrentTimeSlot < processConfiguration.NumberOfTimeSlots)
        {
        }

        logger.LogInformation("Transaction Manager {serverId} stopping...", serverId);
        server.ShutdownAsync().Wait();
        logger.LogInformation("Transaction Manager {serverId} stopped.", serverId);
    }
}