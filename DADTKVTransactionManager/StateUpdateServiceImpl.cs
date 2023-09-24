using Grpc.Core;
using Grpc.Net.Client;
using System.Collections.Concurrent;
using DADTKV;
using Google.Protobuf.Collections;

namespace DADTKVTransactionManagerServer
{
    internal class StateUpdateServiceImpl : StateUpdateService.StateUpdateServiceBase
    {
        private readonly ConcurrentDictionary<ulong, ulong> _sequenceNumCounterLookup;
        private readonly Dictionary<ulong, string> _transactionManagersLookup;

        public StateUpdateServiceImpl(Dictionary<ulong, string> transactionManagersLookup)
        {
            _transactionManagersLookup = transactionManagersLookup;
            _sequenceNumCounterLookup = new ConcurrentDictionary<ulong, ulong>();

            foreach (var (id, url) in transactionManagersLookup)
            {
                _sequenceNumCounterLookup[id] = 0;
            }
        }

        public override Task<UpdateResponse> UpdateBroadcast(UpdateRequest request, ServerCallContext context)
        {
            var currSeqNum = _sequenceNumCounterLookup[request.ServerId];

            if (currSeqNum >= request.SequenceNum)
                return Task.FromResult(new UpdateResponse { Ok = true });

            // TODO: Needs to be atomic
            _sequenceNumCounterLookup[request.ServerId] = currSeqNum + 1;

            foreach (var (id, url) in _transactionManagersLookup)
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                var channel = GrpcChannel.ForAddress(url);
                var client = new StateUpdateService.StateUpdateServiceClient(channel);
                client.UpdateBroadcast(request);
            }

            executeTrans(request.WriteSet);

            return Task.FromResult(new UpdateResponse { Ok = true });
        }

        private void executeTrans(RepeatedField<DadInt> writeSet)
        {
        }
    }
}