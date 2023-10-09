using Grpc.Core;

namespace DADTKV;

/// <summary>
///     Acceptor in the Paxos algorithm.
///     Acceptors are the nodes that receive the prepare and accept requests from the proposers and respond with a
///     promise or accepted response.
/// </summary>
internal class Acceptor : AcceptorService.AcceptorServiceBase
{
    private readonly List<AcceptorState> _acceptorState = new();
    private readonly object _acceptorStateLock = new();

    /// <summary>
    ///     Get the acceptor state for the given round number.
    /// </summary>
    /// <param name="roundNumber">The round number to get the acceptor state for.</param>
    /// <returns>The acceptor state for the given round number.</returns>
    private AcceptorState CurrentRoundAcceptorState(int roundNumber)
    {
        for (var i = _acceptorState.Count; i <= roundNumber; i++)
            _acceptorState.Add(new AcceptorState());

        return _acceptorState[roundNumber];
    }

    /// <summary>
    ///     Receive a prepare request from a proposer.
    /// </summary>
    /// <param name="request">The prepare request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The prepare response.</returns>
    public override Task<PrepareResponse> Prepare(PrepareRequest request, ServerCallContext context)
    {
        lock (_acceptorStateLock)
        {
            var currentRoundAcceptorState = CurrentRoundAcceptorState((int)request.RoundNumber);

            if (request.ProposalNumber <= currentRoundAcceptorState.ReadTimestamp)
                return Task.FromResult(new PrepareResponse
                {
                    Promise = false,
                    WriteTimestamp = 0,
                    RoundNumber = request.RoundNumber,
                    Value = null
                });

            currentRoundAcceptorState.ReadTimestamp = request.ProposalNumber;

            /*
             * If the acceptor has not yet accepted any proposal (that is, it responded with a PROMISE to a past proposal
             * but not an ACCEPTED, it will simply respond back to the proposer with a PROMISE. However, if the acceptor
             * has already accepted an earlier message it responds to the proposer with a PROMISE that contains the
             * accepted ID and its corresponding value.
             */

            // Previously didn't respond with ACCEPTED
            if (currentRoundAcceptorState.Value == null)
                return Task.FromResult(new PrepareResponse
                    {
                        Promise = true,
                        WriteTimestamp = 0,
                        RoundNumber = request.RoundNumber,
                        Value = null
                    }
                );

            /*// Previously responded with ACCEPTED
            return Task.FromResult(new PrepareResponse
            {
                Promise = true,
                WriteTimestamp = currentRoundAcceptorState.WriteTimestamp,
                RoundNumber = request.RoundNumber,
                Value = currentRoundAcceptorState.Value != null
                    ? ConsensusValueDtoConverter.ConvertToDto(currentRoundAcceptorState.Value)
                    : null
            });*/
            return null;
        }
    }

    /// <summary>
    ///     Receive an accept request from a proposer.
    /// </summary>
    /// <param name="request">The accept request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The accept response.</returns>
    public override Task<AcceptResponse> Accept(AcceptRequest request, ServerCallContext context)
    {
        lock (_acceptorStateLock)
        {
            var currentRoundAcceptorState = CurrentRoundAcceptorState((int)request.RoundNumber);

            // TODO does it need to be exactly the current read timestamp? Just checked, and greater or equal seems fine
            if (request.ProposalNumber != currentRoundAcceptorState.ReadTimestamp)
                return Task.FromResult(new AcceptResponse { Accepted = false });

            currentRoundAcceptorState.WriteTimestamp = request.ProposalNumber;
            currentRoundAcceptorState.Value = ConsensusValueDtoConverter.ConvertFromDto(request.Value);

            return Task.FromResult(new AcceptResponse { Accepted = true });
        }
    }
}