using Grpc.Core;
using Grpc.Net.Client;
using DADTKV;

namespace DADTKVT
{
    public class DADTKVServiceImpl : DADTKVService.DADTKVServiceBase
    {
        private readonly string _serverId;
        private readonly Dictionary<string, string> _transactionManagersLookup;
        private ulong _sequenceNumCounter = 0; // TODO needs to be atomic
        private readonly string _leaseManagerUrl;

        public DADTKVServiceImpl(Dictionary<string, string> transactionManagersLookup, string serverId,
            string leaseManagerUrl)
        {
            _transactionManagersLookup = transactionManagersLookup;
            _serverId = serverId;
            _leaseManagerUrl = leaseManagerUrl;
        }

        public override Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request, ServerCallContext context)
        {
            // Ask for lease, etc..
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var lmChannel = GrpcChannel.ForAddress(_leaseManagerUrl);
            var lmClient = new LeaseService.LeaseServiceClient(lmChannel);
            lmClient.RequestLease(new LeaseRequest
            {
                ClientID = _serverId,
                Set = { request.WriteSet.Select(x => x.Key) }
            });

            // Commit transaction
            foreach (var (id, url) in _transactionManagersLookup)
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                var channel = GrpcChannel.ForAddress(url);
                var client = new StateUpdateService.StateUpdateServiceClient(channel);

                var updateReq = new UpdateRequest
                {
                    ServerId = _serverId,
                    SequenceNum = _sequenceNumCounter++,
                    WriteSet = { request.WriteSet }
                };

                client.UpdateBroadcast(updateReq);
            }


            // ...
            return null;
        }

        public override Task<StatusResponse> Status(StatusRequest request, ServerCallContext context)
        {
            // ...
            return null;
        }
    }
}