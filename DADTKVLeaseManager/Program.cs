using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

internal static class Program
{
    private static ILogger<LmLearner> Logger;

    /// <summary>
    ///     Entry point for the lease manager server application.
    /// </summary>
    /// <param name="args">Arguments: serverId systemConfigFilePath</param>
    /// <exception cref="ArgumentException">Invalid arguments.</exception>
    private static void Main(string[] args)
    {
        if (args.Length != 2)
            throw new ArgumentException(
                "Invalid arguments. Usage: DadtkvLeaseManager.exe serverId systemConfigFilePath");

        var serverId = args[0];

        DadtkvLogger.InitializeLogger(serverId);
        Logger = DadtkvLogger.Factory.CreateLogger<LmLearner>();
        
        var configurationFile = Path.Combine(Environment.CurrentDirectory, args[1]);
        var systemConfiguration = SystemConfiguration.ReadSystemConfiguration(configurationFile);

        var leaseManagerConfig = new LeaseManagerConfiguration( new ServerProcessConfiguration(systemConfiguration, serverId));
        var serverProcessPort = new Uri(leaseManagerConfig.ProcessInfo.Url).Port;
        var hostname = new Uri(leaseManagerConfig.ProcessInfo.Url).Host;

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var leaseManagersChannels =
            leaseManagerConfig.LeaseManagers.ToDictionary(
                processInfo => processInfo.Id,
                processInfo => GrpcChannel.ForAddress(processInfo.Url)
            );

        var transactionManagersChannels =
            leaseManagerConfig
                .TransactionManagers.ToDictionary(
                    processInfo => processInfo.Id,
                    processInfo => GrpcChannel.ForAddress(processInfo.Url)
                );

        var learnerServiceClients = new List<LearnerService.LearnerServiceClient>();
        foreach (var (_, transactionManagerChannel) in transactionManagersChannels)
            learnerServiceClients.Add(new LearnerService.LearnerServiceClient(transactionManagerChannel));

        foreach (var (_, leaseManagersChannel) in leaseManagersChannels)
            learnerServiceClients.Add(new LearnerService.LearnerServiceClient(leaseManagersChannel));

        var acceptorServiceClients = new List<AcceptorService.AcceptorServiceClient>();
        foreach (var (_, leaseManagerChannel) in leaseManagersChannels)
            acceptorServiceClients.Add(new AcceptorService.AcceptorServiceClient(leaseManagerChannel));

        var consensusState = new ConsensusState();

        var proposer = new Proposer(consensusState, acceptorServiceClients, learnerServiceClients, leaseManagerConfig);
        var acceptor = new Acceptor(leaseManagerConfig);
        var learner = new LmLearner(leaseManagerConfig, consensusState);

        var server = new Server
        {
            Services =
            {
                LeaseService.BindService(proposer),
                AcceptorService.BindService(acceptor),
                LearnerService.BindService(learner)
            },
            Ports = { new ServerPort(hostname, serverProcessPort, ServerCredentials.Insecure) }
        };
        
        while (DateTime.Now < leaseManagerConfig.WallTime)
        {
            Thread.Sleep(10);
        }

        leaseManagerConfig.StartTimer();
        server.Start();
        proposer.Start();

        Logger.LogInformation($"Lease Manager server listening on port {serverProcessPort}");
        Console.WriteLine("Press Enter to stop the server.");
        Console.ReadLine();

        server.ShutdownAsync().Wait();
    }
}