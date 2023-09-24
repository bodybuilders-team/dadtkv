using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using DADTKV;

namespace DADTKVTransactionManagerServer
{
    public class DADTKVServiceImpl : DADTKVService.DADTKVServiceBase
    {
        private readonly ulong _serverId;
        private readonly Dictionary<ulong, string> _transactionManagersLookup;
        private ulong _sequenceNumCounter = 0;
        private readonly string _leaseManagerUrl;
        
        public DADTKVServiceImpl(Dictionary<ulong, string> transactionManagersLookup, ulong serverId, string leaseManagerUrl)
        {
            this._transactionManagersLookup = transactionManagersLookup;
            this._serverId = serverId;
            this._leaseManagerUrl = leaseManagerUrl;
        }

        public override Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request, ServerCallContext context)
        {
            
            // Ask for lease, etc..
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var lmChannel = GrpcChannel.ForAddress(_leaseManagerUrl);
            var lmClient = new LeaseService.LeaseServiceClient(lmChannel);
            
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
