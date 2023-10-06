using Grpc.Core;
using Timer = System.Timers.Timer;

namespace DADTKV;

/// <summary>
///     The proposer is responsible for proposing values to the acceptors, and deciding on a value for a round, in the
///     Paxos consensus algorithm.
/// </summary>
public class Proposer : LeaseService.LeaseServiceBase
{
    private readonly List<AcceptorService.AcceptorServiceClient> _acceptorServiceServiceClients;
    private readonly ConsensusState _consensusState;

    private readonly ulong _initialProposalNumber;
    private readonly LeaseManagerConfiguration _leaseManagerConfiguration;
    private readonly List<ILeaseRequest> _leaseRequests = new();
    private readonly object _leaseRequestsLockObject = new();

    private readonly UrBroadcaster<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _urBroadcaster;

    public Proposer(
        ConsensusState consensusState,
        List<AcceptorService.AcceptorServiceClient> acceptorServiceClients,
        List<LearnerService.LearnerServiceClient> learnerServiceClients,
        LeaseManagerConfiguration leaseManagerConfiguration
    )
    {
        _consensusState = consensusState;
        _acceptorServiceServiceClients = acceptorServiceClients;
        _leaseManagerConfiguration = leaseManagerConfiguration;
        _urBroadcaster =
            new UrBroadcaster<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient>(learnerServiceClients);
        _initialProposalNumber =
            (ulong)_leaseManagerConfiguration.LeaseManagers.IndexOf(_leaseManagerConfiguration.ProcessInfo) + 1;
    }

    /// <summary>
    ///     Receive a lease request, adding it to the list of lease requests.
    /// </summary>
    /// <param name="request">The lease request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A lease response.</returns>
    public override Task<LeaseResponse> RequestLease(LeaseRequest request, ServerCallContext context)
    {
        lock (_leaseRequestsLockObject)
        {
            _leaseRequests.Add(request);
            return Task.FromResult(new LeaseResponse { Ok = true });
        }
    }

    /// <summary>
    ///     Receive a free lease request, adding it to the list of lease requests.
    /// </summary>
    /// <param name="request">The free lease request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A free lease response.</returns>
    public override Task<FreeLeaseResponse> FreeLease(FreeLeaseRequest request, ServerCallContext context)
    {
        lock (_leaseRequestsLockObject)
        {
            _leaseRequests.Add(request);
            return Task.FromResult(new FreeLeaseResponse { Ok = true });
        }
    }

    /// <summary>
    ///     Start the proposer and its timer.
    ///     The proposer logic includes triggering the update of the list of consensus values, being dependent on the
    ///     learner to update the consensus state object. This is needed to filter out lease requests that have already been
    ///     applied, therefore preventing the duplication of lease requests.
    ///     A new consensus round is only started if previous ones have already been decided.
    /// </summary>
    public void Start()
    {
        const int timeDelta = 1000;
        var timer = new Timer(timeDelta);

        // TODO Check timer, to be sure it is waiting for the previous consensus round to end before starting a new one (pipeline it)
        timer.Elapsed += (source, e) =>
        {
            if (_leaseRequests.Count == 0)
            {
                timer.Start();
                return;
            }

            while (!UpdateConsensusValues())
                Thread.Sleep(100);

            var roundNumber = (ulong)_consensusState.Values.Count;

            var myProposalValue = GetMyProposalValue();

            if (_leaseRequests.Count == 0)
            {
                timer.Start();
                return;
            }

            var decided = Propose(myProposalValue, _initialProposalNumber, roundNumber);

            if (!decided)
            {
                timer.Start();
                return;
            }

            // TODO Add lock?... :(
            while ((ulong)_consensusState.Values.Count <= roundNumber ||
                   _consensusState.Values[(int)roundNumber] == null)
                Thread.Sleep(100);

            timer.Start();
        };

        timer.AutoReset = false;
        timer.Start();
    }

    /// <summary>
    ///     Update the consensus values to have a value for each previous round. These values are useful to filter out
    ///     lease requests that have already been applied.
    /// </summary>
    /// <returns>True if the consensus values were already updated, false otherwise.</returns>
    private bool UpdateConsensusValues()
    {
        var isUpdated = true;
        for (var i = 0; i < _consensusState.Values.Count; i++)
        {
            if (_consensusState.Values[i] != null)
                continue;

            isUpdated = false;
            Propose(new ConsensusValue(), _initialProposalNumber, (ulong)i);
        }

        return isUpdated;
    }

    /// <summary>
    ///     Get the proposal value for the current round. Applies the lease requests to the previous round's value, removing
    ///     from the lease requests the ones that were already applied.
    /// </summary>
    /// <returns>The proposal value for the current round.</returns>
    private ConsensusValue GetMyProposalValue()
    {
        var myProposalValue = _consensusState.Values.Count == 0
            ? new ConsensusValue()
            : _consensusState.Values[^1]!.DeepCopy();

        var leaseQueue = myProposalValue.LeaseQueues;

        lock (_leaseRequestsLockObject)
        {
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
        }

        return myProposalValue;
    }

    /// <summary>
    ///     Propose a value for a round.
    ///     This method only returns when the value is decided for the round, having potentially needed to recursively
    ///     re-propose multiple times with higher proposal numbers.
    /// </summary>
    /// <param name="myProposalValue">myProposalValue The value to propose.</param>
    /// <param name="proposalNumber">The proposal number.</param>
    /// <param name="roundNumber">The round number.</param>
    /// <returns>True if the value was decided for the round, false otherwise.</returns>
    private bool Propose(ConsensusValue myProposalValue, ulong proposalNumber, ulong roundNumber)
    {
        if (!_leaseManagerConfiguration.IsLeader())
            return false;

        // Step 1 - Prepare
        ConsensusValueDto? adoptedValue = null;
        var majorityPromised = SendPrepares(proposalNumber, roundNumber, v => adoptedValue = v);

        if (!majorityPromised)
        {
            RePropose(myProposalValue, proposalNumber, roundNumber);
            return true;
        }

        var proposalValue = adoptedValue == null
            ? myProposalValue
            : ConsensusValueDtoConverter.ConvertFromDto(adoptedValue);

        // Step 2 - Accept
        var majorityAccepted = SendAccepts(proposalValue, proposalNumber, roundNumber);

        if (majorityAccepted)
            Decide(proposalValue, roundNumber);
        else
            RePropose(myProposalValue, proposalNumber, roundNumber);

        return true;
    }

    /// <summary>
    ///     Re-propose with a higher proposal number.
    /// </summary>
    /// <param name="myProposalValue">The value to propose.</param>
    /// <param name="proposalNumber">The proposal number.</param>
    /// <param name="roundNumber">The round number.</param>
    /// <returns>True if the value was decided for the round, false otherwise.</returns>
    private void RePropose(ConsensusValue myProposalValue, ulong proposalNumber, ulong roundNumber)
    {
        Propose(
            myProposalValue,
            proposalNumber + (ulong)_leaseManagerConfiguration.LeaseManagers.Count, roundNumber
        );
    }

    /// <summary>
    ///     Send prepare requests to the acceptors.
    /// </summary>
    /// <param name="proposalNumber">The proposal number.</param>
    /// <param name="roundNumber">The round number.</param>
    /// <param name="updateAdoptedValue">The function to update the adopted value.</param>
    /// <returns>
    ///     True if a majority of acceptors promised to not accept a proposal with a higher proposal number, false
    ///     otherwise.
    /// </returns>
    private bool SendPrepares(ulong proposalNumber, ulong roundNumber, Action<ConsensusValueDto?> updateAdoptedValue)
    {
        var asyncTasks = new List<Task<PrepareResponse>>();
        foreach (var acceptorServiceServiceClient in _acceptorServiceServiceClients)
        {
            var res = acceptorServiceServiceClient.PrepareAsync(
                new PrepareRequest
                {
                    ProposalNumber = proposalNumber,
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

    /// <summary>
    ///     Send accept requests to the acceptors.
    /// </summary>
    /// <param name="proposalValue">The value to propose.</param>
    /// <param name="proposalNumber">The proposal number.</param>
    /// <param name="roundNumber">The round number.</param>
    /// <returns>True if a majority of acceptors accepted the proposal, false otherwise.</returns>
    private bool SendAccepts(ConsensusValue proposalValue, ulong proposalNumber, ulong roundNumber)
    {
        var acceptCalls = new List<Task<AcceptResponse>>();
        _acceptorServiceServiceClients.ForEach(client =>
        {
            var res = client.AcceptAsync(
                new AcceptRequest
                {
                    ProposalNumber = proposalNumber,
                    Value = ConsensusValueDtoConverter.ConvertToDto(proposalValue),
                    RoundNumber = roundNumber
                }
            );
            acceptCalls.Add(res.ResponseAsync);
        });

        return DADTKVUtils.WaitForMajority(acceptCalls, res => res.Accepted);
    }

    /// <summary>
    ///     Decide on a value for a round, broadcasting it to the learners.
    /// </summary>
    /// <param name="newConsensusValue">The value to decide on.</param>
    /// <param name="roundNumber">The round number.</param>
    /// <returns>True if the value was decided for the round, false otherwise.</returns>
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

    /// <summary>
    ///     Handle a free lease request.
    ///     This method checks if the lease request has already been applied in previous rounds, returning true if that is
    ///     the case.
    ///     If the lease request has not been applied, it is applied to the lease queues (removing the lease id from the
    ///     queue).
    /// </summary>
    /// <param name="leaseQueues">The lease queues to apply to.</param>
    /// <param name="freeLeaseRequest">The free lease request to handle.</param>
    /// <returns>True if the lease request has already been applied, false otherwise.</returns>
    private bool HandleFreeLeaseRequest(IReadOnlyDictionary<string, Queue<LeaseId>> leaseQueues,
        FreeLeaseRequest freeLeaseRequest)
    {
        var leaseId = LeaseIdDtoConverter.ConvertFromDto(freeLeaseRequest.LeaseId);

        foreach (var (key, queue) in leaseQueues)
        {
            var existed = false;
            var alreadyFreed = false;

            foreach (var consensusValue in _consensusState.Values)
            {
                if (consensusValue == null)
                    continue;

                if (consensusValue.LeaseQueues.ContainsKey(key) && consensusValue.LeaseQueues[key].Contains(leaseId))
                    existed = true;

                if (existed && consensusValue.LeaseQueues.ContainsKey(key) &&
                    !consensusValue.LeaseQueues[key].Contains(leaseId))
                    alreadyFreed = true;
            }

            if (alreadyFreed) // If one key was freed, all others have been freed too
                return true;

            if (queue.Peek().Equals(leaseId))
                queue.Dequeue();
        }

        return false;
    }

    /// <summary>
    ///     Handle a lease request.
    ///     This method checks if the lease request has already been applied in previous rounds, returning true if that is
    ///     the case.
    ///     If the lease request has not been applied, it is applied to the lease queues (adding the lease id to the queue).
    /// </summary>
    /// <param name="leaseQueues">The lease queues to apply to.</param>
    /// <param name="leaseRequest">The lease request to handle.</param>
    /// <returns>True if the lease request has already been applied, false otherwise.</returns>
    private bool HandleLeaseRequest(IDictionary<string, Queue<LeaseId>> leaseQueues, LeaseRequest leaseRequest)
    {
        var leaseId = LeaseIdDtoConverter.ConvertFromDto(leaseRequest.LeaseId);

        foreach (var leaseKey in leaseRequest.Set)
        {
            if (!leaseQueues.ContainsKey(leaseKey))
                leaseQueues.Add(leaseKey, new Queue<LeaseId>());

            if (_consensusState.Values.Any(consensusValue =>
                    consensusValue != null && consensusValue.LeaseQueues.ContainsKey(leaseKey) &&
                    consensusValue.LeaseQueues[leaseKey].Contains(leaseId))
               )
                return true;

            leaseQueues[leaseKey].Enqueue(leaseId);
        }

        return false;
    }
}