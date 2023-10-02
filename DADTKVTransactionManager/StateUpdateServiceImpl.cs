using Grpc.Core;
using Grpc.Net.Client;
using System.Collections.Concurrent;
using Google.Protobuf.Collections;

namespace DADTKV;

internal class StateUpdateServiceImpl : StateUpdateService.StateUpdateServiceBase
{
    private readonly ProcessConfiguration _processConfiguration;
    private readonly ConcurrentDictionary<string, HashSet<ulong>> _sequenceNumCounterLookup;
    private readonly List<StateUpdateService.StateUpdateServiceClient> _stateUpdateServiceClients;

    public StateUpdateServiceImpl(object lockObject, ProcessConfiguration processConfiguration)
    {
        this._processConfiguration = processConfiguration;
        _sequenceNumCounterLookup = new ConcurrentDictionary<string, HashSet<ulong>>();

        foreach (var tm in processConfiguration.SystemConfiguration.TransactionManagers)
        {
            _sequenceNumCounterLookup[tm.Id] = new HashSet<ulong>();
        }

        this._stateUpdateServiceClients = _processConfiguration.OtherTransactionManagers
            .Select(tm => GrpcChannel.ForAddress(tm.URL))
            .Select(channel => new StateUpdateService.StateUpdateServiceClient(channel))
            .ToList();
    }

    public override Task<UpdateResponse> Update(UpdateRequest request, ServerCallContext context)
    {
        var currSeqNumSet = _sequenceNumCounterLookup[request.ServerId];

        if (currSeqNumSet.Contains(request.SequenceNum))
            return Task.FromResult(new UpdateResponse { Ok = true }); //TODO: Should it be okay?

        _sequenceNumCounterLookup[request.ServerId].Add(request.SequenceNum);

        foreach (var stateUpdateServiceClient in _stateUpdateServiceClients)
        {
            stateUpdateServiceClient.Update(request);
        }

        //TODO: Needs majority to deliver
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