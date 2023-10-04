using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;
using Google.Protobuf.Collections;

namespace DADTKV;

internal class StateUpdateServiceImpl : StateUpdateService.StateUpdateServiceBase
{
    private readonly object _lockObject;
    private readonly DataStore _dataStore;
    private readonly ProcessConfiguration _processConfiguration;
    private readonly List<StateUpdateService.StateUpdateServiceClient> _stateUpdateServiceClients;

    public StateUpdateServiceImpl(object lockObject, ProcessConfiguration processConfiguration, DataStore dataStore)
    {
        _lockObject = lockObject;
        _processConfiguration = processConfiguration;
        _dataStore = dataStore;

        _processConfiguration = processConfiguration;

        _stateUpdateServiceClients = processConfiguration.OtherTransactionManagers
            .Select(tm => GrpcChannel.ForAddress(tm.URL))
            .Select(channel => new StateUpdateService.StateUpdateServiceClient(channel))
            .ToList();
    }



    private void UrbDeliver(UpdateRequest request)
    {
        _dataStore.ExecuteTransaction(request.WriteSet);
    }
}