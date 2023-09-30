using Grpc.Core;

namespace DADTKV
{
    class PaxosServiceImpl : PaxosService.PaxosServiceBase
    {
        private readonly ConsensusState _consensusState;
        private readonly object _lockObject;

        public PaxosServiceImpl(object lockObject, ConsensusState consensusState)
        {
            this._lockObject = lockObject;
            _consensusState = consensusState;
        }

        public override Task<PrepareResponse> Prepare(PrepareRequest request, ServerCallContext context)
        {
            lock (_lockObject)
            {
                if (request.EpochNumber > _consensusState.ReadTimestamp)
                {
                    _consensusState.ReadTimestamp = request.EpochNumber;
                    return Task.FromResult(new PrepareResponse
                        {
                            WriteTimestamp = _consensusState.WriteTimestamp,
                            Value = _consensusState.Value != null
                                ? ConsensusValueDtoConverter.ConvertToDto(_consensusState.Value)
                                : null
                        }
                    );
                }
                else
                {
                    return Task.FromResult(new PrepareResponse { WriteTimestamp = 0, Value = null });
                }
            }
        }

        public override Task<AcceptResponse> Accept(AcceptRequest request, ServerCallContext context)
        {
            return base.Accept(request, context);
        }
    }
}