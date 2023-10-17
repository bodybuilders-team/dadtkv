using Grpc.Core;

namespace Dadtkv;

/// <summary>
///     Acceptor in the Paxos algorithm.
///     Acceptors are the nodes that receive the prepare and accept requests from the proposers and respond with a
///     promise or accepted response.
/// </summary>
internal class Acceptor : AcceptorService.AcceptorServiceBase
{
    private readonly List<AcceptorState> _acceptorState = new();

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
    public override Task<PrepareResponseDto> Prepare(PrepareRequestDto request, ServerCallContext context)
    {
        lock (_acceptorState)
        {
            var currentRoundAcceptorState = CurrentRoundAcceptorState((int)request.RoundNumber);

            if (request.ProposalNumber <= currentRoundAcceptorState.ReadTimestamp)
                return Task.FromResult(new PrepareResponseDto
                {
                    Promise = false,
                    WriteTimestamp = 0,
                    RoundNumber = request.RoundNumber,
                    Value = null
                });

            currentRoundAcceptorState.ReadTimestamp = request.ProposalNumber;

            // Previously didn't respond with ACCEPTED
            if (currentRoundAcceptorState.Value == null)
                return Task.FromResult(new PrepareResponseDto
                    {
                        Promise = true,
                        WriteTimestamp = 0,
                        RoundNumber = request.RoundNumber,
                        Value = null
                    }
                );

            // Previously responded with ACCEPTED
            return Task.FromResult(new PrepareResponseDto
            {
                Promise = true,
                WriteTimestamp = currentRoundAcceptorState.WriteTimestamp,
                RoundNumber = request.RoundNumber,
                Value = currentRoundAcceptorState.Value != null
                    ? ConsensusValueDtoConverter.ConvertToDto(currentRoundAcceptorState.Value)
                    : null
            });
        }
    }

    /// <summary>
    ///     Receive an accept request from a proposer.
    /// </summary>
    /// <param name="request">The accept request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The accept response.</returns>
    public override Task<AcceptResponseDto> Accept(AcceptRequestDto request, ServerCallContext context)
    {
        lock (_acceptorState)
        {
            var currentRoundAcceptorState = CurrentRoundAcceptorState((int)request.RoundNumber);

            // TODO does it need to be exactly the current read timestamp? Just checked, and greater or equal seems fine
            if (!request.ProposalNumber.Equals(currentRoundAcceptorState.ReadTimestamp))
                return Task.FromResult(new AcceptResponseDto { Accepted = false });

            currentRoundAcceptorState.WriteTimestamp = request.ProposalNumber;
            currentRoundAcceptorState.Value = ConsensusValueDtoConverter.ConvertFromDto(request.Value);

            return Task.FromResult(new AcceptResponseDto { Accepted = true });
        }
    }
}