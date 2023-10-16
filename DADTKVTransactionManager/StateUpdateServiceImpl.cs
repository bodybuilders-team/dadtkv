using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

/// <summary>
///     Implementation of the StateUpdateService.
/// </summary>
internal class StateUpdateServiceImpl : StateUpdateService.StateUpdateServiceBase
{
    private readonly DataStore _dataStore;

    private readonly FifoUrbReceiver<UpdateRequest, UpdateResponseDto, StateUpdateService.StateUpdateServiceClient>
        _fifoUrbReceiver;

    private readonly LeaseQueues _leaseQueues;
    private readonly ProcessConfiguration _processConfiguration;

    public StateUpdateServiceImpl(ProcessConfiguration processConfiguration, DataStore dataStore,
        LeaseQueues leaseQueues)
    {
        _processConfiguration = processConfiguration;
        _dataStore = dataStore;
        _leaseQueues = leaseQueues;

        var stateUpdateServiceClients = new List<StateUpdateService.StateUpdateServiceClient>();
        foreach (var process in processConfiguration.OtherTransactionManagers)
        {
            var channel = GrpcChannel.ForAddress(process.Url);
            stateUpdateServiceClients.Add(new StateUpdateService.StateUpdateServiceClient(channel));
        }

        _fifoUrbReceiver =
            new FifoUrbReceiver<UpdateRequest, UpdateResponseDto, StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients,
                UrbDeliver,
                (client, req) => client.UpdateAsync(UpdateRequestDtoConverter.ConvertToDto(req)).ResponseAsync
            );
    }

    /// <summary>
    ///     Executes a transaction associated with an update.
    /// </summary>
    /// <param name="request">The update request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The update response.</returns>
    public override Task<UpdateResponseDto> Update(UpdateRequestDto request, ServerCallContext context)
    {
        _fifoUrbReceiver.FifoUrbProcessRequest(
            UpdateRequestDtoConverter.ConvertFromDto(request, _processConfiguration));

        return Task.FromResult(new UpdateResponseDto { Ok = true });
    }

    private void UrbDeliver(UpdateRequest request)
    {
        lock (_leaseQueues)
        {
            var set = request.WriteSet.Select(dadInt => dadInt.Key).ToList();

            // TODO what if we never obtain the leases
            while (!_leaseQueues.ObtainedLeases(set, request.LeaseId))
            {
                Monitor.Exit(_leaseQueues);
                Thread.Sleep(100);
                Monitor.Enter(_leaseQueues);
            }

            lock (_dataStore)
            {
                _dataStore.ExecuteTransaction(request.WriteSet);
            }

            if (request.FreeLease)
                foreach (var (key, queue) in _leaseQueues)
                    if (queue.Count > 0 && queue.Peek().Equals(request.LeaseId))
                        queue.Dequeue();
        }
    }
}