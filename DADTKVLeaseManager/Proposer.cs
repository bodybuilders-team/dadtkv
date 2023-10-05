using DADTKVCore;
using Grpc.Core;

namespace DADTKV;

public class Proposer : LeaseService.LeaseServiceBase
{
    private readonly List<AcceptorService.AcceptorServiceClient> _acceptorServiceServiceClients;
    private readonly ConsensusState _consensusState;
    private readonly LeaseManagerConfiguration _leaseManagerConfiguration;
    private readonly List<ILeaseRequest> _leaseRequests;
    private readonly object _lockObject;

    private readonly UrBroadcaster<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _urBroadcaster;
    private ulong _proposalNumber;

    public Proposer(
        object lockObject,
        List<ILeaseRequest> leaseRequests,
        ConsensusState consensusState,
        List<AcceptorService.AcceptorServiceClient> acceptorServiceClients,
        List<LearnerService.LearnerServiceClient> learnerServiceClients,
        LeaseManagerConfiguration leaseManagerConfiguration
    )
    {
        _lockObject = lockObject;
        _leaseRequests = leaseRequests;
        _consensusState = consensusState;
        _acceptorServiceServiceClients = acceptorServiceClients;
        _leaseManagerConfiguration = leaseManagerConfiguration;
        _proposalNumber =
            (ulong)_leaseManagerConfiguration.LeaseManagers.IndexOf(_leaseManagerConfiguration.ProcessInfo) + 1;
        _urBroadcaster =
            new UrBroadcaster<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient>(learnerServiceClients);
    }

    public void Start()
    {
        // TODO ADD LOCK

        if (_leaseRequests.Count == 0) return;

        var roundNumber = (ulong)_consensusState.Values.Count;

        var isUpdated = updateConsensusValues();

        var myProposalValue = getMyProposalValue();

        Propose(myProposalValue, roundNumber);

        /*const int timeDelta = 1000;
        var timer = new System.Timers.Timer(timeDelta);

        timer.Elapsed += (source, e) =>
        {
            if (_leaseRequests.Count == 0)
            {
                timer.Start();
                return;
            }

            // TODO: Place LeaseServiceImpl callbacks and process requests in a proposer class
            ProcessRequests();
            timer.Start();
        };
        timer.AutoReset = false;
        timer.Start();*/
    }

    private bool updateConsensusValues()
    {
        var isUpdated = true;
        for (var i = 0; i < _consensusState.Values.Count; i++)
        {
            if (_consensusState.Values[i] != null)
                continue;

            isUpdated = false;
            Propose(new ConsensusValue(), (ulong)i);
        }

        return isUpdated;
    }

    private ConsensusValue getMyProposalValue()
    {
        var myProposalValue = _consensusState.Values.Count == 0
            ? new ConsensusValue()
            : _consensusState.Values[^1].DeepCopy();

        var leaseQueue = myProposalValue.LeaseQueues;

        var toRemove = new List<ILeaseRequest>();
        // Update the lease queues in the proposal value
        foreach (var currentRequest in _leaseRequests)
            switch (currentRequest)
            {
                case LeaseRequest leaseRequest:
                    if (HandleLeaseRequest(leaseQueue, leaseRequest))
                        toRemove.Add(currentRequest);
                    break;
                case FreeLeaseRequest freeLeaseRequest:
                    if (HandleFreeLeaseRequest(leaseQueue, freeLeaseRequest))
                        toRemove.Add(currentRequest);
                    break;
            }

        toRemove.ForEach(request => _leaseRequests.Remove(request));

        return myProposalValue;
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
            return Task.FromResult(new FreeLeaseResponse { Ok = true });
        }
    }

    private void Propose(ConsensusValue myProposalValue, ulong roundNumber)
    {
        lock (_lockObject)
        {
            if (!_leaseManagerConfiguration.IsLeader())
                return;

            // Step 1 - Prepare
            ConsensusValueDto? adoptedValue = null;
            var majorityPromised = ConsensusValueDto(roundNumber, (v) => adoptedValue = v);

            if (!majorityPromised)
            {
                RePropose(myProposalValue, roundNumber);
                return;
            }

            var proposalValue = adoptedValue == null
                ? myProposalValue
                : ConsensusValueDtoConverter.ConvertFromDto(adoptedValue);

            // Step 2 - Accept
            var majorityAccepted = SendAccepts(proposalValue, roundNumber);

            if (majorityAccepted)
                Decide(proposalValue, roundNumber);
            else
                RePropose(myProposalValue, roundNumber);
        }
    }

    /**
     * Re-propose with a higher proposal number.
     *
     * @param myProposalValue The value to propose.
     * @param roundNumber The round number to propose.
     */
    private void RePropose(ConsensusValue myProposalValue, ulong roundNumber)
    {
        _proposalNumber += (ulong)_leaseManagerConfiguration.LeaseManagers.Count;
        Propose(myProposalValue, roundNumber);
    }

    private bool ConsensusValueDto(ulong roundNumber, Action<ConsensusValueDto?> updateAdoptedValue)
    {
        var asyncTasks = new List<Task<PrepareResponse>>();
        foreach (var acceptorServiceServiceClient in _acceptorServiceServiceClients)
        {
            var res = acceptorServiceServiceClient.PrepareAsync(
                new PrepareRequest
                {
                    ProposalNumber = _proposalNumber,
                    RoundNumber = roundNumber
                }
            );
            asyncTasks.Add(res.ResponseAsync);
        }

        var highestWriteTimestamp = 0UL;

        return DADTKVUtils.WaitForMajority(
            asyncTasks,
            res =>
            {
                if (!res.Promise)
                    return false;

                if (res.WriteTimestamp == 0)
                    return true;

                if (res.WriteTimestamp <= highestWriteTimestamp)
                    return true;

                highestWriteTimestamp = res.WriteTimestamp;
                updateAdoptedValue(res.Value);

                return true;
            }
        );
    }

    private bool SendAccepts(ConsensusValue proposalValue, ulong roundNumber)
    {
        var acceptCalls = new List<Task<AcceptResponse>>();
        _acceptorServiceServiceClients.ForEach(client =>
        {
            var res = client.AcceptAsync(
                new AcceptRequest
                {
                    ProposalNumber = _proposalNumber,
                    Value = ConsensusValueDtoConverter.ConvertToDto(proposalValue),
                    RoundNumber = roundNumber
                }
            );
            acceptCalls.Add(res.ResponseAsync);
        });

        return DADTKVUtils.WaitForMajority(acceptCalls, res => res.Accepted);
    }

    private void Decide(ConsensusValue newConsensusValue, ulong roundNumber)
    {
        _urBroadcaster.UrBroadcast(
            new LearnRequest
            {
                ServerId = _leaseManagerConfiguration.ProcessInfo.Id,
                ConsensusValue = ConsensusValueDtoConverter.ConvertToDto(newConsensusValue),
                RoundNumber = roundNumber
            },
            (req, seqNum) => req.SequenceNum = seqNum,
            req =>
            {
                /* TODO Update the consensus round value here too? */
            },
            (client, req) => client.LearnAsync(req).ResponseAsync
        );
    }

    private bool HandleFreeLeaseRequest(IReadOnlyDictionary<string, Queue<LeaseId>> leaseQueues,
        FreeLeaseRequest freeLeaseRequest)
    {
        var leaseId = LeaseIdDtoConverter.ConvertFromDto(freeLeaseRequest.LeaseId);

        var existed = false;
        var alreadyFreed = false;

        foreach (var (key, queue) in leaseQueues)
        {
            foreach (var consensusValue in _consensusState.Values)
            {
                if (consensusValue.LeaseQueues[key].Contains(leaseId))
                    existed = true;

                if (existed && !consensusValue.LeaseQueues[key].Contains(leaseId))
                    alreadyFreed = true;
            }

            if (alreadyFreed)
                return true;

            if (queue.Peek().Equals(leaseId))
                queue.Dequeue();
        }

        return false;
    }

    private bool HandleLeaseRequest(IDictionary<string, Queue<LeaseId>> leaseQueues, LeaseRequest leaseRequest)
    {
        var leaseId = LeaseIdDtoConverter.ConvertFromDto(leaseRequest.LeaseId);

        foreach (var leaseKey in leaseRequest.Set)
        {
            if (!leaseQueues.ContainsKey(leaseKey))
                leaseQueues.Add(leaseKey, new Queue<LeaseId>());

            if (_consensusState.Values.Any(consensusValue => consensusValue.LeaseQueues[leaseKey].Contains(leaseId)))
                return true;

            leaseQueues[leaseKey].Enqueue(leaseId);
        }

        return false;
    }
}