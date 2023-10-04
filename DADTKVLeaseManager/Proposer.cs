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
    private ulong _proposalNumber;

    public Proposer(
        object lockObject,
        List<ILeaseRequest> leaseRequests,
        List<AcceptorService.AcceptorServiceClient> acceptorServiceClients,
        List<LearnerService.LearnerServiceClient> learnerServiceClients,
        LeaseManagerConfiguration leaseManagerConfiguration,
        ConsensusState consensusState
    )
    {
        _lockObject = lockObject;
        _leaseRequests = leaseRequests;
        _acceptorServiceServiceClients = acceptorServiceClients;
        _learnerServiceClients = learnerServiceClients;
        _leaseManagerConfiguration = leaseManagerConfiguration;
        _consensusState = consensusState;
        _proposalNumber =
            (ulong)_leaseManagerConfiguration.LeaseManagers.IndexOf(_leaseManagerConfiguration.ProcessInfo) + 1;
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
            // TODO: This is sus, we are always the leader?
            var currentIsLeader = _leaseManagerConfiguration.GetLeaderId(_proposalNumber) ==
                                  _leaseManagerConfiguration.ProcessInfo.Id;

            //TODO: Verify edge case where leader is crashed but not suspected

            if (!currentIsLeader)
                return;

            // broadcast prepare
            var asyncTasks = new List<Task<PrepareResponse>>();
            foreach (var acceptorServiceServiceClient in _acceptorServiceServiceClients)
            {
                var prepareRequest = new PrepareRequest { EpochNumber = _proposalNumber };
                var res = acceptorServiceServiceClient.PrepareAsync(prepareRequest);
                asyncTasks.Add(res.ResponseAsync);
            }

            _proposalNumber += (ulong)_leaseManagerConfiguration.LeaseManagers.Count;

            //TODO: Check if we should include our own highest write timestamp value 
            var highestWriteTimestamp = _consensusState.WriteTimestamp;
            ConsensusValueDto? newConsensusValue = null;

            DADTKVUtils.WaitForMajority(
                asyncTasks,
                (res) =>
                {
                    if (res.Promise)
                        return true;

                    //TODO: Should we do something if the received write timestamp is higher than epoch number?
                    if (res.WriteTimestamp <= highestWriteTimestamp)
                        return false;

                    highestWriteTimestamp = res.WriteTimestamp;
                    newConsensusValue = res.Value;

                    return false;
                }
            );

            _consensusState.WriteTimestamp = highestWriteTimestamp;
            _consensusState.Value = newConsensusValue == null
                ? _consensusState.Value
                : ConsensusValueDtoConverter.ConvertFromDto(newConsensusValue);

            ActOnPromiseMajority();
        }
    }

    private void ActOnPromiseMajority()
    {
        // We have majority, so we need to calculate the new consensus value
        var newConsensusValue = _consensusState.Value == null
            ? new ConsensusValue()
            : _consensusState.Value.DeepCopy();

        var leaseQueue = newConsensusValue.LeaseQueues;

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

        var acceptCalls = new List<Task<AcceptResponse>>();
        _acceptorServiceServiceClients.ForEach(client =>
        {
            var acceptReq = new AcceptRequest
            {
                EpochNumber = _proposalNumber,
                Value = ConsensusValueDtoConverter.ConvertToDto(newConsensusValue),
            };

            var res = client.AcceptAsync(acceptReq);
            acceptCalls.Add(res.ResponseAsync);
        });

        DADTKVUtils.WaitForMajority(acceptCalls, (res) => res.Accepted);

        Decide(newConsensusValue);
    }

    private void Decide(ConsensusValue newConsensusValue)
    {
        // TODO: acceptor.accept

        foreach (var learnerServiceClient in _learnerServiceClients)
        {
            learnerServiceClient.LearnAsync(
                new LearnRequest
                {
                    ServerId = _leaseManagerConfiguration.ProcessInfo.Id,
                    SequenceNum = _sequenceNumCounter++,
                    ConsensusValue = ConsensusValueDtoConverter.ConvertToDto(newConsensusValue),
                    EpochNumber = _proposalNumber
                });
        }


        _consensusState.WriteTimestamp = _proposalNumber;
        _consensusState.Value = newConsensusValue; // TODO: Acceptor state is not decided by consensus
        _leaseRequests.Clear();
    }

    private static void HandleFreeLeaseRequest(IReadOnlyDictionary<string, Queue<LeaseId>> leaseQueues,
        FreeLeaseRequest freeLeaseRequest)
    {
        var leaseId = LeaseIdDtoConverter.ConvertFromDto(freeLeaseRequest.LeaseId);

        foreach (var (key, queue) in leaseQueues)
        {
            if (queue.Peek().Equals(leaseId))
                queue.Dequeue();
        }
    }

    private static void HandleLeaseRequest(IDictionary<string, Queue<LeaseId>> leaseQueue, LeaseRequest leaseRequest)
    {
        var leaseId = LeaseIdDtoConverter.ConvertFromDto(leaseRequest.LeaseId);

        foreach (var leaseKey in leaseRequest.Set)
        {
            if (!leaseQueue.ContainsKey(leaseKey))
                leaseQueue.Add(leaseKey, new Queue<LeaseId>());

            //TODO: ignore request? send not okay to transaction manager?
            if (!leaseQueue[leaseKey].Contains(leaseId))
                leaseQueue[leaseKey].Enqueue(leaseId);
        }
    }
}