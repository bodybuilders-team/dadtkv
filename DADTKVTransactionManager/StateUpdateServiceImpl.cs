using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

internal class StateUpdateServiceImpl : StateUpdateService.StateUpdateServiceBase
{
    private readonly DataStore _dataStore;

    public StateUpdateServiceImpl(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public override Task<UpdateResponse> Update(UpdateRequest request, ServerCallContext context)
    {
        lock (_dataStore)
        {
            _dataStore.ExecuteTransaction(request.WriteSet);
            return Task.FromResult(new UpdateResponse()
            {
                Ok = true
            });
        }
    }
}