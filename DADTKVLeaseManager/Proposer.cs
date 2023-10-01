using Grpc.Core;

namespace DADTKV;

public class Proposer : LeaseService.LeaseServiceBase
{
    private readonly object _lockObject;
    private readonly List<ILeaseRequest> _leaseRequests;
    private readonly Dictionary<string, ServerProcessChannel> _otherLeaseManagerChannels;
    private readonly ConsensusState _consensusState;
    private readonly LeaseManagerConfiguration _leaseManagerConfiguration;

    private readonly List<AcceptorService.AcceptorServiceClient> _acceptorServiceClients = new();
    private readonly List<LearnerService.LearnerServiceClient> _learnerServiceClients = new();

    private ulong EpochNumber => _consensusState.WriteTimestamp + 1;

    public Proposer(
        object lockObject,
        List<ILeaseRequest> leaseRequests,
        Dictionary<string, ServerProcessChannel> otherLeaseManagerChannels,
        Dictionary<string, ServerProcessChannel> transactionManagersChannels,
        ConsensusState consensusState,
        LeaseManagerConfiguration leaseManagerConfiguration
    )
    {
        _lockObject = lockObject;
        _leaseRequests = leaseRequests;
        _otherLeaseManagerChannels = otherLeaseManagerChannels;
        _consensusState = consensusState;
        _leaseManagerConfiguration = leaseManagerConfiguration;

        foreach (var (_, leaseManagerChannel) in _otherLeaseManagerChannels)
        {
            _acceptorServiceClients.Add(new AcceptorService.AcceptorServiceClient(leaseManagerChannel.GrpcChannel));
        }

        foreach (var (_, transactionManagerChannel) in transactionManagersChannels)
        {
            _learnerServiceClients.Add(new LearnerService.LearnerServiceClient(transactionManagerChannel.GrpcChannel));
        }
    }

    public void Start()
    {
        const int timeDelta = 1000;
        var timer = new System.Timers.Timer(timeDelta);

        timer.Elapsed += (source, e) =>
        {
            // TODO: Place LeaseServiceImpl callbacks and process requests in a proposer class
            ProcessRequests();
            timer.Start();
        };
        timer.AutoReset = false;
        timer.Start();
    }

    public override Task<LeaseResponse> RequestLease(LeaseRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            _leaseRequests.Add(request);
            return Task.FromResult(new LeaseResponse { Ok = true });
        }
    }

    public override Task<FreeLeaseResponse> FreeLease(FreeLeaseRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            _leaseRequests.Add(request);
            return Task.FromResult(new FreeLeaseResponse() { Ok = true });
        }
    }

    private void ProcessRequests()
    {
        lock (_lockObject)
        {
            var currentIsLeader = _leaseManagerConfiguration.GetLeaderId() ==
                                  _leaseManagerConfiguration.ProcessConfiguration.ProcessInfo.Id;

            //TODO: Verify edge case where leader is crashed but not suspected

            if (!currentIsLeader)
                return;

            var numPromises = 0;
            var highestWriteTimestamp = _consensusState
                .WriteTimestamp; //TODO: Check if we should include our own highest write timestamp value 

            var newConsensusValue = _consensusState.Value;

            // broadcast prepare
            foreach (var client in _acceptorServiceClients)
            {
                // TODO: Make Async
                var response = client.Prepare(new PrepareRequest
                {
                    EpochNumber = EpochNumber // TODO: Do not send prepare when in middle of consensus phase.
                });

                if (response.Promise)
                    numPromises++;

                //TODO: Should we do something if the received write timestamp is higher than epoch number?

                if (response.WriteTimestamp <= highestWriteTimestamp) continue;

                highestWriteTimestamp = response.WriteTimestamp;
                newConsensusValue =
                    ConsensusValueDtoConverter
                        .ConvertFromDto(response
                            .Value); //TODO: Fix, no need to convert, only for the last one. but only if it doesn't become a mess!
            }

            //TODO: Adopt the highest consensus value
            // consensusState.WriteTimestamp = highestWriteTimestamp;
            // consensusState.Value = newConsensusValue;

            if (numPromises <= _leaseManagerConfiguration.LeaseManagers.Count / 2)
                return;

            ActOnMajority();
        }
    }

    private void ActOnMajority()
    {
        // We have majority, so we need to calculate the new consensus value
        var newConsensusValue = _consensusState.Value == null
            ? new ConsensusValue()
            : _consensusState.Value.DeepCopy();

        var leaseQueue = newConsensusValue.LeaseQueue;

        foreach (var currentRequest in _leaseRequests)
        {
            switch (currentRequest)
            {
                case LeaseRequest leaseRequest:
                    HandleLeaseRequest(leaseQueue, leaseRequest);
                    break;
                case FreeLeaseRequest freeLeaseRequest:
                    HandleFreeLeaseRequest(leaseQueue, freeLeaseRequest);
                    break;
            }
        }

        // TODO: Add URB for Acceptors
        _acceptorServiceClients.ForEach(client => client.Accept(new AcceptRequest
        {
            EpochNumber = EpochNumber,
            Value = ConsensusValueDtoConverter.ConvertToDto(newConsensusValue)
        }));

        Decide(newConsensusValue);
    }

    private void Decide(ConsensusValue newConsensusValue)
    {
        // TODO: acceptor.accept
        
        _consensusState.WriteTimestamp = EpochNumber;
        _consensusState.Value = newConsensusValue;
        
        _learnerServiceClients.ForEach(client => client.Learn(new LearnRequest
        {
            ConsensusValue = ConsensusValueDtoConverter.ConvertToDto(newConsensusValue),
            EpochNumber = EpochNumber
        }));
    }

    private static void HandleFreeLeaseRequest(IReadOnlyDictionary<string, Queue<string>> leaseQueue,
        FreeLeaseRequest freeLeaseRequest)
    {
        foreach (var leaseKey in freeLeaseRequest.Set)
        {
            if (!leaseQueue.ContainsKey(leaseKey))
                continue; //TODO: ignore request? send not okay to transaction manager?

            if (leaseQueue[leaseKey].Peek() == freeLeaseRequest.ClientID)
                leaseQueue[leaseKey].Dequeue();
            //TODO: else ignore request? send not okay to transaction manager?
        }
    }

    private static void HandleLeaseRequest(IDictionary<string, Queue<string>> leaseQueue, LeaseRequest leaseRequest)
    {
        foreach (var leaseKey in leaseRequest.Set)
        {
            if (!leaseQueue.ContainsKey(leaseKey))
                leaseQueue.Add(leaseKey, new Queue<string>());

            if (!leaseQueue[leaseKey]
                    .Contains(leaseRequest
                        .ClientID)) //TODO: ignore request? send not okay to transaction manager?
                leaseQueue[leaseKey].Enqueue(leaseRequest.ClientID);
        }
    }
}