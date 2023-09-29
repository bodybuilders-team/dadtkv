using Grpc.Core;

namespace DADTKV
{
    class PaxosServiceImpl : PaxosService.PaxosServiceBase
    {
        private ulong readTimestamp = 0;
        private ulong writeTimestamp = 0;
        private LeaseConsensusValue? leaseQueue = null;
        private Object lockObject;

        public PaxosServiceImpl(Object lockObject)
        {
            this.lockObject = lockObject;
        }

        public override Task<Promise> Prepare(PrepareRequest request, ServerCallContext context)
        {
            lock (lockObject)
            {
                if (request.EpochNumber > readTimestamp)
                {
                    readTimestamp = request.EpochNumber;
                    return Task.FromResult(new Promise
                        { WriteTimestamp = writeTimestamp, Value = leaseQueue }
                    );
                }
                else
                {
                    return Task.FromResult(new Promise { WriteTimestamp = 0, Value = null });
                }
            }
        }

        public override Task<AcceptResponse> Accept(AcceptRequest request, ServerCallContext context)
        {
            return base.Accept(request, context);
        }
    }
}