using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

using ServerProcessChannels = Dictionary<string, ServerProcessChannel>;

internal static class Program
{
    // Entry point for the server application
    // Arguments: serverId, hostName, configurationFile
    private static void Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Invalid arguments.");

        var serverId = args[0];

        var configurationFile = Path.Combine(Environment.CurrentDirectory, args[2]);
        var systemConfiguration = SystemConfiguration.ReadSystemConfiguration(configurationFile)!;

        var processConfiguration = new ProcessConfiguration(systemConfiguration, serverId);
        var leaseManagerConfiguration = new LeaseManagerConfiguration(processConfiguration);
        var serverProcessPort = new Uri(processConfiguration.ProcessInfo.URL).Port;
        var hostname = new Uri(processConfiguration.ProcessInfo.URL).Host;

        var lockObject = new object();
        var leaseRequests = new List<ILeaseRequest>();

        var consensusState = new ConsensusState();

        var otherLeaseManagersChannels =
            leaseManagerConfiguration.OtherLeaseManagers.ToDictionary(
                processInfo => processInfo.Id,
                processInfo => new ServerProcessChannel
                {
                    ProcessInfo = processInfo,
                    GrpcChannel = GrpcChannel.ForAddress(processInfo.URL)
                });

        var transactionManagersChannels =
            leaseManagerConfiguration.ProcessConfiguration.SystemConfiguration
                .TransactionManagers
                .ToDictionary(
                    processInfo => processInfo.Id,
                    processInfo => new ServerProcessChannel
                    {
                        ProcessInfo = processInfo,
                        GrpcChannel = GrpcChannel.ForAddress(processInfo.URL)
                    });

        var learnerServiceClients = new List<LearnerService.LearnerServiceClient>();
        foreach (var (_, transactionManagerChannel) in transactionManagersChannels)
        {
            learnerServiceClients.Add(new LearnerService.LearnerServiceClient(transactionManagerChannel.GrpcChannel));
        }

        var acceptorServiceClients = new List<AcceptorService.AcceptorServiceClient>();
        foreach (var (_, leaseManagerChannel) in otherLeaseManagersChannels)
        {
            acceptorServiceClients.Add(new AcceptorService.AcceptorServiceClient(leaseManagerChannel.GrpcChannel));
        }

        var proposer = new Proposer(lockObject, leaseRequests, acceptorServiceClients, learnerServiceClients,
            consensusState, leaseManagerConfiguration);

        var server = new Server
        {
            Services =
            {
                LeaseService.BindService(proposer),
                AcceptorService.BindService(new Acceptor(lockObject, consensusState, learnerServiceClients))
            },
            Ports = { new ServerPort(hostname, serverProcessPort, ServerCredentials.Insecure) }
        };

        server.Start();

        Console.WriteLine($"Lease Manager server listening on port {serverProcessPort}");
        Console.WriteLine("Press Enter to stop the server.");
        Console.ReadLine();

        proposer.Start();

        server.ShutdownAsync().Wait();
    }
}