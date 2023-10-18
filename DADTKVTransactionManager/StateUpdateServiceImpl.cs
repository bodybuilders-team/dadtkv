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

    private readonly ILogger<StateUpdateServiceImpl> _logger =
        DadtkvLogger.Factory.CreateLogger<StateUpdateServiceImpl>();

    private readonly ServerProcessConfiguration _serverProcessConfiguration;

    public StateUpdateServiceImpl(ServerProcessConfiguration serverProcessConfiguration, DataStore dataStore,
        LeaseQueues leaseQueues)
    {
        _serverProcessConfiguration = serverProcessConfiguration;
        _dataStore = dataStore;
        _leaseQueues = leaseQueues;

        var stateUpdateServiceClients = new List<StateUpdateService.StateUpdateServiceClient>();
        foreach (var process in serverProcessConfiguration.OtherTransactionManagers)
        {
            var channel = GrpcChannel.ForAddress(process.Url);
            stateUpdateServiceClients.Add(new StateUpdateService.StateUpdateServiceClient(channel));
        }

        _fifoUrbReceiver =
            new FifoUrbReceiver<UpdateRequest, UpdateResponseDto, StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients,
                FifoUrbDeliver,
                (client, req) =>
                {
                    // TODO: Put in metadata
                    var upReq = UpdateRequestDtoConverter.ConvertToDto(req);
                    upReq.ServerId = _serverProcessConfiguration.ServerId;

                    return client.UpdateAsync(upReq).ResponseAsync;
                },
                serverProcessConfiguration
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
        // Obtain the server id from the context.Peer string by searching the clients
        _logger.LogDebug($"Received Update request from server " +
                         $"{_serverProcessConfiguration.FindServerProcessId((int)request.ServerId)}, " +
                         $"request: {request}");

        new Thread(() =>
            _fifoUrbReceiver.FifoUrbProcessRequest(UpdateRequestDtoConverter.ConvertFromDto(request))
        ).Start();

        _logger.LogDebug($"Responding Update request from server " +
                         $"{_serverProcessConfiguration.FindServerProcessId((int)request.ServerId)}, " +
                         $"request: {request}");

        return Task.FromResult(new UpdateResponseDto { Ok = true });
    }

    /// <summary>
    ///     Delivers an update request.
    /// </summary>
    /// <param name="request">The update request.</param>
    private void FifoUrbDeliver(UpdateRequest request)
    {
        // TODO: Abstract this duplication check out if this
        if (request.BroadcasterId == _serverProcessConfiguration.ServerId)
            return;

        // TODO, can't be just a thread, otherwise fifo order is not guaranteed
        _logger.LogDebug($"Received Update request 2: {request}");

        lock (_leaseQueues)
        {
            var set = request.WriteSet.Select(dadInt => dadInt.Key).ToList();

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
                _leaseQueues.FreeLeases(request.LeaseId);

            _logger.LogDebug($"Lease queues after update request: {_leaseQueues}");
        }
    }
}