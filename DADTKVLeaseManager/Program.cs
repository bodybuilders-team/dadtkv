﻿using Grpc.Core;
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


        var leaseManagersChannels =
            leaseManagerConfiguration.LeaseManagers.ToDictionary(
                processInfo => processInfo.Id,
                processInfo => new ServerProcessChannel
                {
                    ProcessInfo = processInfo,
                    GrpcChannel = GrpcChannel.ForAddress(processInfo.URL)
                });

        var transactionManagersChannels =
            leaseManagerConfiguration
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

        foreach (var (_, leaseManagersChannel) in leaseManagersChannels)
        {
            learnerServiceClients.Add(new LearnerService.LearnerServiceClient(leaseManagersChannel.GrpcChannel));
        }

        var acceptorServiceClients = new List<AcceptorService.AcceptorServiceClient>();
        foreach (var (_, leaseManagerChannel) in leaseManagersChannels)
        {
            acceptorServiceClients.Add(new AcceptorService.AcceptorServiceClient(leaseManagerChannel.GrpcChannel));
        }

        var consensusState = new ConsensusState();

        var proposer = new Proposer(lockObject, leaseRequests, acceptorServiceClients, learnerServiceClients,
            leaseManagerConfiguration, consensusState);
        var acceptor = new Acceptor(lockObject, acceptorServiceClients, learnerServiceClients,
            leaseManagerConfiguration);
        var learner = new LmLearner(lockObject, processConfiguration, consensusState, leaseRequests);
        var server = new Server
        {
            Services =
            {
                LeaseService.BindService(proposer),
                AcceptorService.BindService(acceptor)
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