using Grpc.Core;

namespace DADTKV;

internal class Acceptor : AcceptorService.AcceptorServiceBase
{
    private readonly AcceptorState _acceptorState = new();
    private readonly object _lockObject;
    private readonly List<LearnerService.LearnerServiceClient> _learnerServiceClients;
    private readonly List<AcceptorService.AcceptorServiceClient> _acceptorServiceServiceClients;

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
            if (request.EpochNumber <= _acceptorState.ReadTimestamp)
                return Task.FromResult(new PrepareResponse
                {
                    Promise = false,
                    WriteTimestamp = _acceptorState.WriteTimestamp,
                    Value = _acceptorState.Value != null
                        ? ConsensusValueDtoConverter.ConvertToDto(_acceptorState.Value)
                        : null
                });

            _acceptorState.ReadTimestamp = request.EpochNumber;
            return Task.FromResult(new PrepareResponse
                {
                    Promise = true,
                    WriteTimestamp = _acceptorState.WriteTimestamp,
                    Value = _acceptorState.Value != null
                        ? ConsensusValueDtoConverter.ConvertToDto(_acceptorState.Value)
                        : null
                }
            );
        }
    }

    public override Task<AcceptResponse> Accept(AcceptRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            // TODO does it need to be exactly the current read timestamp?
            if (request.EpochNumber != _acceptorState.ReadTimestamp)
                return Task.FromResult(new AcceptResponse { Accepted = false });

            _acceptorState.WriteTimestamp = request.EpochNumber;
            _acceptorState.Value = ConsensusValueDtoConverter.ConvertFromDto(request.Value);

            return Task.FromResult(new AcceptResponse { Accepted = true });
        }
    }
}