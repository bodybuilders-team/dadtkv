using Grpc.Core;

namespace DADTKV;

public class Proposer : LeaseService.LeaseServiceBase
{
    private readonly object _lockObject;
    private readonly List<ILeaseRequest> _leaseRequests;
    private readonly ConsensusState _consensusState;
    private readonly LeaseManagerConfiguration _leaseManagerConfiguration;

    private readonly List<AcceptorService.AcceptorServiceClient> _acceptorServiceServiceClients;
    private readonly List<LearnerService.LearnerServiceClient> _learnerServiceClients;

    private ulong _sequenceNumCounter;

    private ulong EpochNumber => _consensusState.WriteTimestamp + 1;

    public Proposer(
        object lockObject,
        List<ILeaseRequest> leaseRequests,
        List<AcceptorService.AcceptorServiceClient> acceptorServiceClients,
        List<LearnerService.LearnerServiceClient> learnerServiceClients,
        ConsensusState consensusState,
        LeaseManagerConfiguration leaseManagerConfiguration
    )
    {
        _lockObject = lockObject;
        _leaseRequests = leaseRequests;
        _acceptorServiceServiceClients = acceptorServiceClients;
        _learnerServiceClients = learnerServiceClients;
        _consensusState = consensusState;
        _leaseManagerConfiguration = leaseManagerConfiguration;
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

            //TODO: Check if we should include our own highest write timestamp value 
            var highestWriteTimestamp = _consensusState.WriteTimestamp;
            var newConsensusValue = _consensusState.Value;

            // broadcast prepare
            var asyncTasks = new List<AsyncUnaryCall<PrepareResponse>>();
            foreach (var acceptorServiceServiceClient in _acceptorServiceServiceClients)
            {
                var prepareRequest = new PrepareRequest { EpochNumber = EpochNumber };
                var res = acceptorServiceServiceClient.PrepareAsync(prepareRequest);
                asyncTasks.Add(res);
            }

            DADTKVUtils.WaitForMajority(
                asyncTasks,
                (res, cde) =>
                {
                    if (res.Promise)
                        cde.Signal();
                    else
                    {
                        //TODO: Should we do something if the received write timestamp is higher than epoch number?
                        if (res.WriteTimestamp <= highestWriteTimestamp)
                            return Task.CompletedTask;

                        highestWriteTimestamp = res.WriteTimestamp;
                        //TODO: Fix, no need to convert, only for the last one. but only if it doesn't become a mess!
                        newConsensusValue = ConsensusValueDtoConverter.ConvertFromDto(res.Value);
                    }

                    return Task.CompletedTask;
                }
            );

            //TODO: Adopt the highest consensus value
            // consensusState.WriteTimestamp = highestWriteTimestamp;
            // consensusState.Value = newConsensusValue;

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

        var asyncUnaryCalls = new List<AsyncUnaryCall<AcceptResponse>>();
        _acceptorServiceServiceClients.ForEach(client =>
        {
            var acceptReq = new AcceptRequest
            {
                EpochNumber = EpochNumber,
                Value = ConsensusValueDtoConverter.ConvertToDto(newConsensusValue),
                ServerId = _leaseManagerConfiguration.ProcessConfiguration.ProcessInfo.Id,
                SequenceNum = _sequenceNumCounter++
            };

            var res = client.AcceptAsync(acceptReq);
            asyncUnaryCalls.Add(res);
        });

        DADTKVUtils.WaitForMajority(
            asyncUnaryCalls,
            (res, cde) =>
            {
                if (!res.Accepted) //TODO: Is this necessary?
                    throw new Exception();

                cde.Signal();
                return Task.CompletedTask;
            }
        );

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

            //TODO: ignore request? send not okay to transaction manager?
            if (!leaseQueue[leaseKey].Contains(leaseRequest.ClientID))
                leaseQueue[leaseKey].Enqueue(leaseRequest.ClientID);
        }
    }
}