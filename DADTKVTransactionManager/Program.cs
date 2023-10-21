using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

internal static class Program
{
    private static ILogger<DadtkvServiceImpl> Logger ;

    /// <summary>
    ///     Entry point for the lease manager server application.
    /// </summary>
    /// <param name="args">Arguments: serverId systemConfigFilePath</param>
    /// <exception cref="ArgumentException">Invalid arguments.</exception>
    public static void Main(string[] args)
    {
        if (args.Length != 2)
            throw new ArgumentException(
                "Invalid arguments. Usage: DadtkvTransactionManager.exe serverId systemConfigFilePath");

        var serverId = args[0];
        DadtkvLogger.InitializeLogger(serverId);
        Logger = DadtkvLogger.Factory.CreateLogger<DadtkvServiceImpl>();
        
        var configurationFile = Path.Combine(Environment.CurrentDirectory, args[1]);
        var systemConfiguration = SystemConfiguration.ReadSystemConfiguration(configurationFile)!;

        var processConfiguration = new ServerProcessConfiguration(systemConfiguration, serverId);
        var serverProcessPort = new Uri(processConfiguration.ProcessInfo.Url).Port;
        var hostname = new Uri(processConfiguration.ProcessInfo.Url).Host;

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        LeaseQueues leaseQueues = new();
        var datastore = new DataStore();
        var executedTrans = new Dictionary<LeaseId, bool>();

        var server = new Server
        {
            Services =
            {
                DadtkvService.BindService(new DadtkvServiceImpl(processConfiguration, datastore, executedTrans,
                    leaseQueues)),
                StateUpdateService.BindService(new StateUpdateServiceImpl(processConfiguration, datastore,
                    leaseQueues)),
                LearnerService.BindService(new TmLearner(processConfiguration, executedTrans, leaseQueues))
            },
            Ports = { new ServerPort(hostname, serverProcessPort, ServerCredentials.Insecure) }
        };

        processConfiguration.TimeSlotTimer.Start();
        server.Start();

        Logger.LogInformation($"Transaction Manager server listening on port {serverProcessPort}");

        Console.WriteLine("Press Enter to stop the server.");
        Console.ReadLine();

        server.ShutdownAsync().Wait();
    }
}