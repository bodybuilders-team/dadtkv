using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

/// <summary>
///     The proposer is responsible for proposing values to the acceptors, and deciding on a value for a round, in the
///     Paxos consensus algorithm.
/// </summary>
public class Proposer : LeaseService.LeaseServiceBase
{
    private const int LeaderTimeout = 3;
    private readonly List<AcceptorService.AcceptorServiceClient> _acceptorServiceClients;
    private readonly ConsensusState _consensusState;

    private readonly ulong _initialProposalNumber;
    private readonly LeaseManagerConfiguration _leaseManagerConfiguration;
    private readonly List<LeaseRequest> _leaseRequests = new();
    private readonly ILogger<Proposer> _logger = DadtkvLogger.Factory.CreateLogger<Proposer>();
    private readonly UrBroadcaster<LearnRequest, LearnResponseDto, LearnerService.LearnerServiceClient> _urBroadcaster;

    public Proposer(
        ConsensusState consensusState,
        List<AcceptorService.AcceptorServiceClient> acceptorServiceClients,
        List<LearnerService.LearnerServiceClient> learnerServiceClients,
        LeaseManagerConfiguration leaseManagerConfiguration
    )
    {
        _consensusState = consensusState;
        _acceptorServiceClients = acceptorServiceClients;
        _leaseManagerConfiguration = leaseManagerConfiguration;
        _urBroadcaster = new UrBroadcaster<LearnRequest, LearnResponseDto, LearnerService.LearnerServiceClient>(
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
        // Do not check if is suspected because TMs do not suspect of LMs
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
        var roundNumber = 1;

        var leaderTimeoutTimestamp = DateTime.UtcNow;
        var leaderTimeoutRoundNumber = -1;
        var leaderTimeoutId = "";

        while (true)
        {
            // _logger.LogDebug("Waiting for round {roundNumber} to start", roundNumber);
            if (_leaseManagerConfiguration.CurrentTimeSlot > _leaseManagerConfiguration.NumberOfTimeSlots)
                return;

            if (roundNumber > _leaseManagerConfiguration.CurrentTimeSlot)
            {
                Thread.Sleep(100);
                continue;
            }

            lock (_consensusState)
            {
                if (_consensusState.Values.Count > roundNumber && _consensusState.Values[roundNumber] != null)
                {
                    _logger.LogDebug("Consensus value for round {roundNumber} is {value}",
                        roundNumber, _consensusState.Values[roundNumber]);

                    roundNumber++;
                    continue;
                }
            }

            // _logger.LogDebug("Trying round {roundNumber}", roundNumber);
            lock (_leaseRequests)
            {
                if (_leaseRequests.Count == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }
            }

            var myProposalValue = GetMyProposalValue();

            if (myProposalValue.LeaseRequests.Count == 0)
            {
                Thread.Sleep(10);
                continue;
            }

            var decided = Propose(myProposalValue, _initialProposalNumber, (ulong)roundNumber);

            if (!decided)
            {
                if (leaderTimeoutRoundNumber != roundNumber)
                {
                    _logger.LogDebug("Round {roundNumber} not decided - {currentTime}", roundNumber, DateTime.Now);

                    leaderTimeoutTimestamp = DateTime.Now.AddSeconds(LeaderTimeout);
                    leaderTimeoutRoundNumber = roundNumber;
                    leaderTimeoutId = _leaseManagerConfiguration.GetLeaderId();
                }

                if (leaderTimeoutRoundNumber == roundNumber && DateTime.Now > leaderTimeoutTimestamp)
                {
                    _logger.LogDebug("Leader {leaderTimeoutId} timeout for round {leaderTimeoutRoundNumber}",
                        leaderTimeoutId, leaderTimeoutRoundNumber);
                    _leaseManagerConfiguration.AddRealSuspicion(
                        leaderTimeoutId
                    );
                    leaderTimeoutRoundNumber = -1;
                    continue;
                }

                Thread.Sleep(100);
                continue;
            }

            _logger.LogDebug("Round {roundNumber} decided", roundNumber);


            lock (_consensusState)
            {
                // Wait for the consensus value to be updated (eventually because of URB)
                while (_consensusState.Values.Count <= roundNumber || _consensusState.Values[roundNumber] == null)
                {
                    Monitor.Exit(_consensusState);
                    Thread.Sleep(10);
                    Monitor.Enter(_consensusState);
                }
            }

            _logger.LogDebug("Consensus value for round {roundNumber} is {value}", roundNumber,
                _consensusState.Values[roundNumber]);

            roundNumber++;
        }
    }

    /// <summary>
    ///     Get the proposal value for the current round. Applies the lease requests to the previous round's value,
    ///     removing from the lease requests the ones that were already applied.
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
                if (_consensusState.Values.Any(consensusValue =>
                        consensusValue != null &&
                        consensusValue.LeaseRequests.Exists(req => req.Equals(currentRequest))))
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
        var currentProposalValue = myProposalValue;
        var currentProposalNumber = proposalNumber;

        while (true)
        {
            if (!_leaseManagerConfiguration.IsLeader())
            {
                _logger.LogDebug("Not leader for round {roundNumber}", roundNumber);
                return false;
            }

            _logger.LogDebug(
                $"Proposing {currentProposalValue} and proposal number {currentProposalNumber} for round {roundNumber}");

            // Step 1 - Prepare
            ConsensusValueDto? adoptedValue = null;
            var majorityPromised = SendPrepares(currentProposalNumber, roundNumber, v => adoptedValue = v);

            currentProposalValue =
                adoptedValue == null // TODO check if there's an issue with adopting if not majority promised
                    ? currentProposalValue
                    : ConsensusValueDtoConverter.ConvertFromDto(adoptedValue);

            if (!majorityPromised)
            {
                currentProposalNumber += (ulong)_leaseManagerConfiguration.LeaseManagers.Count;
                Thread.Sleep(20);
                continue;
            }

            _logger.LogDebug("Majority promised for round {roundNumber}", roundNumber);

            // Step 2 - Accept
            var majorityAccepted = SendAccepts(currentProposalValue, currentProposalNumber, roundNumber);

            if (majorityAccepted)
            {
                Decide(currentProposalValue, roundNumber);
                return true;
            }

            currentProposalNumber += (ulong)_leaseManagerConfiguration.LeaseManagers.Count;
            Thread.Sleep(20);
        }
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
        var asyncTasks = _acceptorServiceClients
            .Select(acceptorServiceServiceClient =>
                {
                    var req = new PrepareRequestDto
                    {
                        ServerId = _leaseManagerConfiguration.ServerId,
                        ProposalNumber = proposalNumber,
                        RoundNumber = roundNumber
                    };

                    var res = acceptorServiceServiceClient
                        .PrepareAsync(req); // TODO -1: Do not send to ourselves, send using methods

                    return new DadtkvUtils.TaskWithRequest<PrepareRequestDto, PrepareResponseDto>(res.ResponseAsync,
                        req);
                }
            )
            .ToList();

        var highestWriteTimestamp = 0UL;

        return DadtkvUtils.WaitForMajority(
            asyncTasks,
            countSelf: false,
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
            },
            onTimeout: req =>
            {
                // Add server id to real suspected servers
                _leaseManagerConfiguration.AddRealSuspicion(
                    _leaseManagerConfiguration.FindServerProcessId((int)req.ServerId)
                );
            },
            onSuccess: req =>
            {
                // Remove server id from real suspected servers
                _leaseManagerConfiguration.RemoveRealSuspicion(
                    _leaseManagerConfiguration.FindServerProcessId((int)req.ServerId)
                );
            }
        ).Result;
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
        var acceptCalls = new List<DadtkvUtils.TaskWithRequest<AcceptRequestDto, AcceptResponseDto>>();
        _acceptorServiceClients.ForEach(client =>
        {
            var req = new AcceptRequestDto
            {
                ServerId = _leaseManagerConfiguration.ServerId,
                ProposalNumber = proposalNumber,
                Value = ConsensusValueDtoConverter.ConvertToDto(proposalValue),
                RoundNumber = roundNumber
            };
            var res = client.AcceptAsync(req);

            acceptCalls.Add(
                new DadtkvUtils.TaskWithRequest<AcceptRequestDto, AcceptResponseDto>(res.ResponseAsync, req));
        });

        return DadtkvUtils.WaitForMajority(
            acceptCalls,
            false,
            res => res.Accepted,
            onTimeout: req =>
            {
                // Add server id to real suspected servers
                _leaseManagerConfiguration.AddRealSuspicion(
                    _leaseManagerConfiguration.FindServerProcessId((int)req.ServerId)
                );
            },
            onSuccess: req =>
            {
                // Remove server id from real suspected servers
                _leaseManagerConfiguration.RemoveRealSuspicion(
                    _leaseManagerConfiguration.FindServerProcessId((int)req.ServerId)
                );
            }
        ).Result;
    }

    /// <summary>
    ///     Decide on a value for a round, broadcasting it to the learners.
    /// </summary>
    /// <param name="newConsensusValue">The value to decide on.</param>
    /// <param name="roundNumber">The round number.</param>
    /// <returns>True if the value was decided for the round, false otherwise.</returns>
    private void Decide(ConsensusValue newConsensusValue, ulong roundNumber)
    {
        _logger.LogDebug("Deciding {newConsensusValue} for round {roundNumber}", newConsensusValue, roundNumber);

        // TODO -1: Decide in learners instead.
        _urBroadcaster.UrBroadcast(
            new LearnRequest(
                _leaseManagerConfiguration.ServerId,
                _leaseManagerConfiguration.ServerId,
                roundNumber,
                newConsensusValue
            ),
            (client, req) => client.LearnAsync(LearnRequestDtoConverter.ConvertToDto(req)).ResponseAsync
        );
    }
}