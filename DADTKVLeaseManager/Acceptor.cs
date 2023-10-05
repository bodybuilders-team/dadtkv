using Grpc.Core;

namespace DADTKV;

internal class Acceptor : AcceptorService.AcceptorServiceBase
{
    private readonly List<AcceptorService.AcceptorServiceClient> _acceptorServiceServiceClients;
    private readonly AcceptorState _acceptorState = new();
    private readonly List<LearnerService.LearnerServiceClient> _learnerServiceClients;
    private readonly object _lockObject;

    public Acceptor(
        object lockObject,
        List<AcceptorService.AcceptorServiceClient> acceptorServiceClients,
        List<LearnerService.LearnerServiceClient> learnerServiceClients,
        LeaseManagerConfiguration leaseManagerConfiguration
    )
    {
        _lockObject = lockObject;
        _learnerServiceClients = learnerServiceClients;
        _acceptorServiceServiceClients = acceptorServiceClients;
    }

    public override Task<PrepareResponse> Prepare(PrepareRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            if (request.ProposalNumber <= _acceptorState.ReadTimestamp)
                return Task.FromResult(new PrepareResponse
                {
                    Promise = false,
                    WriteTimestamp = 0,
                    Value = null
                });

            _acceptorState.ReadTimestamp = request.ProposalNumber;

            /*
             * If the acceptor has not yet accepted any proposal (that is, it responded with a PROMISE to a past proposal
             * but not an ACCEPTED, it will simply respond back to the proposer with a PROMISE. However, if the acceptor
             * has already accepted an earlier message it responds to the proposer with a PROMISE that contains the
             * accepted ID and its corresponding value.
             */

            // Previously didn't respond with ACCEPTED
            if (_acceptorState.Value == null)
                return Task.FromResult(new PrepareResponse
                    {
                        Promise = true,
                        WriteTimestamp = 0,
                        Value = null
                    }
                );

            // Previously responded with ACCEPTED
            return Task.FromResult(new PrepareResponse
            {
                Promise = true,
                WriteTimestamp = _acceptorState.WriteTimestamp,
                Value = _acceptorState.Value != null
                    ? ConsensusValueDtoConverter.ConvertToDto(_acceptorState.Value)
                    : null
            });
        }
    }

    public override Task<AcceptResponse> Accept(AcceptRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            // TODO does it need to be exactly the current read timestamp? Just checked, and greater or equal seems fine
            if (request.ProposalNumber != _acceptorState.ReadTimestamp)
                return Task.FromResult(new AcceptResponse { Accepted = false });

            _acceptorState.WriteTimestamp = request.ProposalNumber;
            _acceptorState.Value = ConsensusValueDtoConverter.ConvertFromDto(request.Value);

            return Task.FromResult(new AcceptResponse { Accepted = true });
        }
    }
}