using DADTKVT;
using Grpc.Core;

namespace DADTKV;

internal static class Program
{
    /// <summary>
    ///     Entry point for the lease manager server application.
    /// </summary>
    /// <param name="args">Arguments: serverId systemConfigFilePath</param>
    /// <exception cref="ArgumentException">Invalid arguments.</exception>
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

        var datastore = new DataStore();
        var executedTrans = new Dictionary<LeaseId, bool>();

        var server = new Server
        {
            Services =
            {
                DADTKVService.BindService(new DADTKVServiceImpl(processConfiguration, consensusState,
                    datastore, executedTrans)),
                StateUpdateService.BindService(new StateUpdateServiceImpl(datastore)),
                LearnerService.BindService(new TmLearner(processConfiguration, consensusState,
                    executedTrans))
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