using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

internal class StateUpdateServiceImpl : StateUpdateService.StateUpdateServiceBase
{
    private readonly DataStore _dataStore;
    private readonly ProcessConfiguration _processConfiguration;
    private readonly List<StateUpdateService.StateUpdateServiceClient> _stateUpdateServiceClients;

    private readonly HashSet<string> _msgIdLookup = new();

    public StateUpdateServiceImpl(ProcessConfiguration processConfiguration, DataStore dataStore)
    {
        _processConfiguration = processConfiguration;
        _dataStore = dataStore;

        _processConfiguration = processConfiguration;

        _stateUpdateServiceClients = processConfiguration.OtherTransactionManagers
            .Select(tm => GrpcChannel.ForAddress(tm.Url))
            .Select(channel => new StateUpdateService.StateUpdateServiceClient(channel))
            .ToList();
    }

    public override Task<UpdateResponse> Update(UpdateRequest request, ServerCallContext context)
    {
        lock (_msgIdLookup)
        {
            var msgId = request.ServerId + request.SequenceNum;

            if (_msgIdLookup.Contains(msgId))
                return Task.FromResult(new UpdateResponse()
                {
                    Ok = true
                });

            _msgIdLookup.Add(msgId);

            var resTasks = new List<Task<UpdateResponse>>();
            foreach (var susClient in _stateUpdateServiceClients)
            {
                var updateReq = new UpdateRequest
                {
                    ServerId = request.ServerId,
                    SequenceNum = request.SequenceNum,
                    WriteSet = { request.WriteSet }
                };

                var res = susClient.UpdateAsync(updateReq); //TODO: Check if throws exception when timeout
                resTasks.Add(res.ResponseAsync);
            }

            Task.WaitAll(resTasks.ToArray());

            UrbDeliver(request);
        }

        return Task.FromResult(new UpdateResponse()
        {
            Ok = true
        });
    }

    private void UrbDeliver(UpdateRequest request)
    {
        lock (_dataStore)
        {
            _dataStore.ExecuteTransaction(request.WriteSet);
        }
    }
}