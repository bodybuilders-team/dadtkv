using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

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

    private readonly ILogger<StateUpdateServiceImpl> _logger =
        DadtkvLogger.Factory.CreateLogger<StateUpdateServiceImpl>();

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
                FifoUrbDeliver,
                (client, req) => client.UpdateAsync(UpdateRequestDtoConverter.ConvertToDto(req)).ResponseAsync,
                processConfiguration
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
        _logger.LogDebug($"Received Update request: {request}");
        _fifoUrbReceiver.FifoUrbProcessRequest(
            UpdateRequestDtoConverter.ConvertFromDto(request));

        return Task.FromResult(new UpdateResponseDto { Ok = true });
    }

    private void FifoUrbDeliver(UpdateRequest request)
    {
        lock (_leaseQueues)
        {
            var set = request.WriteSet.Select(dadInt => dadInt.Key).ToList();
            _logger.LogDebug($"Received Update request 2: {request}");


            // TODO what if we never obtain the leases
            while (!_leaseQueues.ObtainedLeases(set, request.LeaseId))
            {
                Monitor.Exit(_leaseQueues);
                Thread.Sleep(10);
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

            _logger.LogDebug($"Lease queues after update request: {_leaseQueues}");
        }
    }
}