using Grpc.Core;
using Timer = System.Timers.Timer;

namespace Dadtkv;

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
    private readonly List<LeaseRequest> _leaseRequests = new();

    private readonly UrBroadcaster<LearnRequest, LearnResponseDto, LearnerService.LearnerServiceClient> _urBroadcaster;

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
            new UrBroadcaster<LearnRequest, LearnResponseDto, LearnerService.LearnerServiceClient>(
                learnerServiceClients);
        _initialProposalNumber =
            (ulong)_leaseManagerConfiguration.LeaseManagers.IndexOf(_leaseManagerConfiguration.ProcessInfo) + 1;
    }

    /// <summary>
    ///     Receive a lease request, adding it to the list of lease requests.
    /// </summary>
    /// <param name="request">The lease request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>A lease response.</returns>
    public override Task<LeaseResponseDto> RequestLease(LeaseRequestDto request, ServerCallContext context)
    {
        lock (_leaseRequests)
        {
            _leaseRequests.Add(LeaseRequestDtoConverter.ConvertFromDto(request));
            return Task.FromResult(new LeaseResponseDto { Ok = true });
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
        timer.Elapsed += (_, _) =>
        {
            if (_leaseRequests.Count == 0)
            {
                timer.Start();
                return;
            }

            while (!UpdateConsensusValues())
                Thread.Sleep(10);

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
                Thread.Sleep(10);

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
        var myProposalValue = new ConsensusValue();

        lock (_leaseRequests)
        {
            var toRemove = new List<LeaseRequest>();

            // Update the lease queues in the proposal value
            foreach (var currentRequest in _leaseRequests)
                if (_consensusState.Values.Any(consensusValue => consensusValue.LeaseRequests.Contains(currentRequest)))
                    toRemove.Add(currentRequest);
                else
                    myProposalValue.LeaseRequests.Add(currentRequest);

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
        var asyncTasks = new List<Task<PrepareResponseDto>>();
        foreach (var acceptorServiceServiceClient in _acceptorServiceServiceClients)
        {
            var res = acceptorServiceServiceClient.PrepareAsync(
                new PrepareRequestDto
                {
                    ProposalNumber = proposalNumber,
                    RoundNumber = roundNumber
                }
            );
            asyncTasks.Add(res.ResponseAsync);
        }

        var highestWriteTimestamp = 0UL;

        return DadtkvUtils.WaitForMajority(
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
        var acceptCalls = new List<Task<AcceptResponseDto>>();
        _acceptorServiceServiceClients.ForEach(client =>
        {
            var res = client.AcceptAsync(
                new AcceptRequestDto
                {
                    ProposalNumber = proposalNumber,
                    Value = ConsensusValueDtoConverter.ConvertToDto(proposalValue),
                    RoundNumber = roundNumber
                }
            );
            acceptCalls.Add(res.ResponseAsync);
        });

        return DadtkvUtils.WaitForMajority(acceptCalls, res => res.Accepted);
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
            new LearnRequest(
                _leaseManagerConfiguration,
                _leaseManagerConfiguration.ServerId,
                roundNumber,
                newConsensusValue
            ),
            req =>
            {
                /* TODO Update the consensus round value here too? */
            },
            (client, req) => client.LearnAsync(LearnRequestDtoConverter.ConvertToDto(req)).ResponseAsync
        );
    }
}