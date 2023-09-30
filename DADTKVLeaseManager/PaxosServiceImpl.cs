using Grpc.Core;

namespace DADTKV;

internal class PaxosServiceImpl : PaxosService.PaxosServiceBase
{
    private readonly ConsensusState _consensusState;
    private readonly object _lockObject;

    public PaxosServiceImpl(object lockObject, ConsensusState consensusState)
    {
        _lockObject = lockObject;
        _consensusState = consensusState;
    }

    public override Task<PrepareResponse> Prepare(PrepareRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            if (request.EpochNumber <= _consensusState.ReadTimestamp)
                return Task.FromResult(new PrepareResponse { WriteTimestamp = 0, Value = null });

            _consensusState.ReadTimestamp = request.EpochNumber;
            return Task.FromResult(new PrepareResponse
                {
                    Promise = true,
                    WriteTimestamp = _consensusState.WriteTimestamp,
                    Value = _consensusState.Value != null
                        ? ConsensusValueDtoConverter.ConvertToDto(_consensusState.Value)
                        : null
                }
            );
        }
    }

    public override Task<AcceptResponse> Accept(AcceptRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            if (request.EpochNumber <= _consensusState.ReadTimestamp)
                return Task.FromResult(new AcceptResponse { Accepted = false });

            _consensusState.WriteTimestamp = request.EpochNumber;
            _consensusState.Value = ConsensusValueDtoConverter.ConvertFromDto(request.Value);

            return Task.FromResult(new AcceptResponse
            {
                Accepted = true,
                WriteTimestamp = _consensusState.WriteTimestamp,
                Value = ConsensusValueDtoConverter.ConvertToDto(_consensusState.Value)
            });
        }
    }
}