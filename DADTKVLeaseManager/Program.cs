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
        if (args.Length != 3)
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

        var channels =
            processConfiguration.OtherServerProcesses.ToDictionary(
                processInfo => processInfo.Id,
                processInfo => new ServerProcessChannel
                {
                    ProcessInfo = processInfo,
                    GrpcChannel = GrpcChannel.ForAddress(processInfo.URL)
                });

        var server = new Server
        {
            Services =
            {
                LeaseService.BindService(new LeaseServiceImpl(lockObject, leaseRequests)),
                PaxosService.BindService(new PaxosServiceImpl(lockObject, consensusState))
            },
            Ports = { new ServerPort(hostname, serverProcessPort, ServerCredentials.Insecure) }
        };

        server.Start();

        Console.WriteLine($"Lease Manager server listening on port {serverProcessPort}");
        Console.WriteLine("Press Enter to stop the server.");
        Console.ReadLine();

        const int timeDelta = 1000;
        var timer = new System.Timers.Timer(timeDelta);

        timer.Elapsed += (source, e) =>
        {
            // TODO: Place LeaseServiceImpl callbacks and process requests in a proposer class
            ProcessRequests(lockObject, channels, leaseRequests, consensusState, leaseManagerConfiguration);
            timer.Start();
        };
        timer.AutoReset = false;
        timer.Start();

        server.ShutdownAsync().Wait();
    }

    private static void ProcessRequests(
        object lockObject,
        ServerProcessChannels serverProcessChannels,
        List<ILeaseRequest> leaseRequests,
        ConsensusState consensusState,
        LeaseManagerConfiguration leaseManagerConfiguration
    )
    {
        lock (lockObject)
        {
            var epochNumber = consensusState.WriteTimestamp + 1;


            var currentIsLeader = leaseManagerConfiguration.GetLeaderId() ==
                                  leaseManagerConfiguration.ProcessConfiguration.ProcessInfo.Id;

            //TODO: Verify edge case where leader is crashed but not suspected

            if (!currentIsLeader)
                return;

            var numPromises = 0;
            var highestWriteTimestamp = consensusState
                .WriteTimestamp; //TODO: Check if we should include our own highest write timestamp value 

            var newConsensusValue = consensusState.Value;

            // broadcast prepare
            foreach (var (_, serverProcessChannel) in serverProcessChannels)
            {
                var client = new PaxosService.PaxosServiceClient(serverProcessChannel.GrpcChannel);

                // TODO: Make Async
                var response = client.Prepare(new PrepareRequest
                {
                    EpochNumber = epochNumber // TODO: Do not send prepare when in middle of consensus phase.
                });

                if (response.Promise)
                    numPromises++;
                
                //TODO: Should we do something if the received write timestamp is higher than epoch number?

                if (response.WriteTimestamp <= highestWriteTimestamp) continue;

                highestWriteTimestamp++;
                newConsensusValue =
                    ConsensusValueDtoConverter
                        .ConvertFromDto(response
                            .Value); //TODO: Fix, no need to convert, only for the last one. but only if it doesn't become a mess!
            }

            //TODO: Adopt the highest consensus value
            // consensusState.WriteTimestamp = highestWriteTimestamp;
            // consensusState.Value = newConsensusValue;

            if (numPromises <= serverProcessChannels.Count / 2)
                return;

            
            // We have majority, so we need to calculate the new consensus value
            var leaseQueue = newConsensusValue.LeaseQueue; // TODO: Do not override previous consensus value

            foreach (var currentRequest in leaseRequests)
            {
                switch (currentRequest)
                {
                    case LeaseRequest leaseRequest:

                        foreach (var leaseKey in leaseRequest.Set)
                        {
                            if (!leaseQueue.ContainsKey(leaseKey))
                                leaseQueue.Add(leaseKey, new Queue<string>());

                            if (!leaseQueue[leaseKey].Contains(leaseRequest.ClientID)) //TODO: ignore request? send not okay to transaction manager?
                                leaseQueue[leaseKey].Enqueue(leaseRequest.ClientID);
                        }
                        break;
                    case FreeLeaseRequest freeLeaseRequest:
                        foreach (var leaseKey in freeLeaseRequest.Set)
                        {
                            if (!leaseQueue.ContainsKey(leaseKey))
                                continue; //TODO: ignore request? send not okay to transaction manager?

                            if (leaseQueue[leaseKey].Peek() == freeLeaseRequest.ClientID)
                                leaseQueue[leaseKey].Dequeue();
                            //TODO: else ignore request? send not okay to transaction manager?
                        }

                        break;
                }
            }

            foreach (var (_, serverProcessChannel) in serverProcessChannels) //TODO: Only to lease managers
            {
                var client = new PaxosService.PaxosServiceClient(serverProcessChannel.GrpcChannel);

               var response = client.Accept(new AcceptRequest
                {
                    EpochNumber = epochNumber,
                    Value = ConsensusValueDtoConverter.ConvertToDto(newConsensusValue)
                });
                
               // Check if we got accept majority
            }
        }
    }
}