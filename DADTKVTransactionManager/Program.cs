using Grpc.Core;

namespace DADTKV;

internal static class Program
{
    // Entry point for the server application
    // Arguments: serverId systemConfigFilePath
    public static void Main(string[] args)
    {
        if (args.Length != 2)
            throw new ArgumentException("Invalid arguments.");

        var serverId = args[0];

        var configurationFile = Path.Combine(Environment.CurrentDirectory, args[1]);

        var systemConfiguration = SystemConfiguration.ReadSystemConfiguration(configurationFile)!;

        var consensusState = new ConsensusState();

        var processConfiguration = new ProcessConfiguration(systemConfiguration, serverId);
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
                DADTKVService.BindService(new DadtkvServiceImpl(processConfiguration,
                    datastore, executedTrans, leaseQueues)),
                StateUpdateService.BindService(new StateUpdateServiceImpl(processConfiguration, datastore,
                    leaseQueues)),
                LearnerService.BindService(new TmLearner(processConfiguration, executedTrans, leaseQueues))
            },
            Ports = { new ServerPort(hostname, serverProcessPort, ServerCredentials.Insecure) }
        };

        server.Start();

        Console.WriteLine($"Transaction Manager server listening on port {serverProcessPort}");
        Console.WriteLine("Press Enter to stop the server.");
        Console.ReadLine();

        server.ShutdownAsync().Wait();
    }
}