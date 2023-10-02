using Grpc.Core;
using Grpc.Net.Client;
using System.Collections.Concurrent;
using Google.Protobuf.Collections;

namespace DADTKV;

internal class StateUpdateServiceImpl : StateUpdateService.StateUpdateServiceBase
{
    private readonly ProcessConfiguration _processConfiguration;
    private readonly ConcurrentDictionary<string, ulong> _sequenceNumCounterLookup;

    public StateUpdateServiceImpl(object lockObject, ProcessConfiguration processConfiguration)
    {
        this._processConfiguration = processConfiguration;
        _sequenceNumCounterLookup = new ConcurrentDictionary<string, ulong>();

        foreach (var tm in processConfiguration.TransactionManagers)
        {
            _sequenceNumCounterLookup[tm.Id] = 0;
        }
    }

    public override Task<UpdateResponse> UpdateBroadcast(UpdateRequest request, ServerCallContext context)
    {
        var currSeqNum = _sequenceNumCounterLookup[request.ServerId];

        if (currSeqNum >= request.SequenceNum)
            return Task.FromResult(new UpdateResponse { Ok = true });

        // TODO: Needs to be atomic
        _sequenceNumCounterLookup[request.ServerId] = currSeqNum + 1;

        foreach (var tm in _processConfiguration.TransactionManagers)
        {
            var channel = GrpcChannel.ForAddress(tm.URL);
            var client = new StateUpdateService.StateUpdateServiceClient(channel);
            client.UpdateBroadcast(request);
        }

        // Deliver
        deliver(request.WriteSet);

        return Task.FromResult(new UpdateResponse { Ok = true });
    }

    private void deliver(RepeatedField<DadInt> writeSet)
    {
        executeTrans(writeSet);
    }

    private void executeTrans(RepeatedField<DadInt> writeSet)
    {
    }
}