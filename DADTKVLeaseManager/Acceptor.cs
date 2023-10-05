using Grpc.Core;

namespace DADTKV;

internal class Acceptor : AcceptorService.AcceptorServiceBase
{
    private readonly List<AcceptorState> _acceptorState = new();
    private readonly object _acceptorStateLock = new();

    private AcceptorState CurrentRoundAcceptorState(int roundNumber)
    {
        for (var i = _acceptorState.Count; i <= roundNumber; i++)
            _acceptorState.Add(new AcceptorState());
        
        return _acceptorState[roundNumber];
    }
    
    /**
     * Receive a prepare request from a proposer.
     */
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

            // Previously responded with ACCEPTED
            return Task.FromResult(new PrepareResponse
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

    /**
     * Receive an accept request from a proposer.
     */
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