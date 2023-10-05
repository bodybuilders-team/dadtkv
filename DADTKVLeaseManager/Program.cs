﻿using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

using ServerProcessChannels = Dictionary<string, ServerProcessChannel>;

internal static class Program
{
    // Entry point for the server application
    // Arguments: serverId systemConfigFilePath
    private static void Main(string[] args)
    {
        if (args.Length != 2)
            throw new ArgumentException("Invalid arguments.");

        var serverId = args[0];

        var configurationFile = Path.Combine(Environment.CurrentDirectory, args[1]);
        var systemConfiguration = SystemConfiguration.ReadSystemConfiguration(configurationFile)!;

        var processConfiguration = new ProcessConfiguration(systemConfiguration, serverId);
        var leaseManagerConfiguration = new LeaseManagerConfiguration(processConfiguration);
        var serverProcessPort = new Uri(processConfiguration.ProcessInfo.Url).Port;
        var hostname = new Uri(processConfiguration.ProcessInfo.Url).Host;

        var leaseManagersChannels =
            leaseManagerConfiguration.LeaseManagers.ToDictionary(
                processInfo => processInfo.Id,
                processInfo => new ServerProcessChannel
                {
                    ProcessInfo = processInfo,
                    GrpcChannel = GrpcChannel.ForAddress(processInfo.Url)
                });

        var transactionManagersChannels =
            leaseManagerConfiguration
                .TransactionManagers
                .ToDictionary(
                    processInfo => processInfo.Id,
                    processInfo => new ServerProcessChannel
                    {
                        ProcessInfo = processInfo,
                        GrpcChannel = GrpcChannel.ForAddress(processInfo.Url)
                    });

        var learnerServiceClients = new List<LearnerService.LearnerServiceClient>();
        foreach (var (_, transactionManagerChannel) in transactionManagersChannels)
            learnerServiceClients.Add(new LearnerService.LearnerServiceClient(transactionManagerChannel.GrpcChannel));

        foreach (var (_, leaseManagersChannel) in leaseManagersChannels)
            learnerServiceClients.Add(new LearnerService.LearnerServiceClient(leaseManagersChannel.GrpcChannel));

        var acceptorServiceClients = new List<AcceptorService.AcceptorServiceClient>();
        foreach (var (_, leaseManagerChannel) in leaseManagersChannels)
            acceptorServiceClients.Add(new AcceptorService.AcceptorServiceClient(leaseManagerChannel.GrpcChannel));

        var consensusState = new ConsensusState();

        var proposer = new Proposer(consensusState, acceptorServiceClients, learnerServiceClients,
            leaseManagerConfiguration);
        var acceptor = new Acceptor();
        var learner = new LmLearner(processConfiguration, consensusState);

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

        server.Start();

        Console.WriteLine($"Lease Manager server listening on port {serverProcessPort}");
        Console.WriteLine("Press Enter to stop the server.");
        Console.ReadLine();

        proposer.Start();

        server.ShutdownAsync().Wait();
    }
}