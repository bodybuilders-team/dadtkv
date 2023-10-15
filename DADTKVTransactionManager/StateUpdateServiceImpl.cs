using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

/// <summary>
///     Implementation of the StateUpdateService.
/// </summary>
internal class StateUpdateServiceImpl : StateUpdateService.StateUpdateServiceBase
{
    private readonly DataStore _dataStore;
    private readonly LeaseQueues _leaseQueues;
    

    private readonly UrbReceiver<UpdateRequest, UpdateResponse, StateUpdateService.StateUpdateServiceClient, string>
        _tobReceiver;

    public StateUpdateServiceImpl(ProcessConfiguration processConfiguration, DataStore dataStore,
        LeaseQueues leaseQueues)
    {
        _dataStore = dataStore;
        _leaseQueues = leaseQueues;

        var stateUpdateServiceClients = new List<StateUpdateService.StateUpdateServiceClient>();
        foreach (var process in processConfiguration.OtherTransactionManagers)
        {
            var channel = GrpcChannel.ForAddress(process.Url);
            stateUpdateServiceClients.Add(new StateUpdateService.StateUpdateServiceClient(channel));
        }

        _tobReceiver =
            new TobReceiver<UpdateRequest, UpdateResponse, StateUpdateService.StateUpdateServiceClient, string>(
                stateUpdateServiceClients,
                UrbDeliver,
                req => req.ServerId + "-" + req.SequenceNum,
                (client, req) => client.UpdateAsync(req).ResponseAsync
            );
        
        
    }

    /// <summary>
    ///     Executes a transaction associated with an update.
    /// </summary>
    /// <param name="request">The update request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The update response.</returns>
    public override Task<UpdateResponse> Update(UpdateRequest request, ServerCallContext context)
    {
        _tobReceiver.UrbProcessRequest(request);

        return Task.FromResult(new UpdateResponse
        {
            Ok = true
        });
    }

    public void UrbDeliver(UpdateRequest request)
    {
        lock (_leaseQueues)
        {
            var set = request.WriteSet.Select(dadInt => dadInt.Key).ToList();
            var leaseId = LeaseIdDtoConverter.ConvertFromDto(request.LeaseId);

            // TODO what if we never obtain the leases
            while (!_leaseQueues.ObtainedLeases(set, leaseId))
            {
                Thread.Sleep(100);
            }

            lock (_dataStore)
            {
                _dataStore.ExecuteTransaction(request.WriteSet);
            }

            if (request.FreeLease)
            {
                foreach (var (key, queue) in _leaseQueues)
                {
                    if (queue.Count > 0 && queue.Peek().Equals(leaseId))
                    {
                        queue.Dequeue();
                    }
                }
            }
        }
    }
}