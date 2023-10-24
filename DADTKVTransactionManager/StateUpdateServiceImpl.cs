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
        _updateFifoUrbReceiver;

    private readonly FifoUrbReceiver<FreeLeaseRequest, FreeLeaseResponseDto,
        StateUpdateService.StateUpdateServiceClient> _freeLeaseFifoUrbReceiver;

    private readonly FifoUrbReceiver<PrepareForFreeLeaseRequest, PrepareForFreeLeaseResponseDto,
            StateUpdateService.StateUpdateServiceClient>
        _prepareForFreeLeaseFifoUrbReceiver;

    private readonly FifoUrbReceiver<ForceFreeLeaseRequest, ForceFreeLeaseResponseDto,
            StateUpdateService.StateUpdateServiceClient>
        _forceFreeLeaseFifoUrbReceiver;

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

        _updateFifoUrbReceiver =
            new FifoUrbReceiver<UpdateRequest, UpdateResponseDto, StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients,
                UpdateFifoUrbDeliver,
                (client, req) =>
                {
                    // TODO: Put in metadata
                    var upReq = UpdateRequestDtoConverter.ConvertToDto(req);
                    upReq.ServerId = _serverProcessConfiguration.ServerId;

                    return client.UpdateAsync(upReq).ResponseAsync;
                },
                serverProcessConfiguration
            );

        _freeLeaseFifoUrbReceiver =
            new FifoUrbReceiver<FreeLeaseRequest, FreeLeaseResponseDto,
                StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients,
                (request =>
                {
                    lock (_leaseQueues)
                    {
                        _leaseQueues.FreeLeases(request.LeaseId);
                        _logger.LogDebug("Lease queues after free lease request: {leaseQueues}",
                            _leaseQueues.ToString());
                    }
                }),
                (client, req) =>
                {
                    var upReq = FreeLeaseRequestDtoConverter.ConvertToDto(req);
                    upReq.ServerId = _serverProcessConfiguration.ServerId;

                    return client.FreeLeaseAsync(upReq).ResponseAsync;
                },
                serverProcessConfiguration);

        _prepareForFreeLeaseFifoUrbReceiver =
            new FifoUrbReceiver<PrepareForFreeLeaseRequest, PrepareForFreeLeaseResponseDto,
                StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients,
                (request => { }),
                (client, req) =>
                {
                    var upReq = PrepareForFreeLeaseRequestDtoConverter.ConvertToDto(req);
                    upReq.ServerId = _serverProcessConfiguration.ServerId;

                    return client.PrepareForFreeLeaseAsync(upReq).ResponseAsync;
                },
                serverProcessConfiguration);

        _forceFreeLeaseFifoUrbReceiver =
            new FifoUrbReceiver<ForceFreeLeaseRequest, ForceFreeLeaseResponseDto,
                StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients,
                (request =>
                {
                    lock (_leaseQueues)
                    {
                        _leaseQueues.FreeLeases(request.LeaseId);
                        _logger.LogDebug("Lease queues after force free lease request: {leaseQueues}",
                            _leaseQueues.ToString());
                    }
                }),
                (client, req) =>
                {
                    var upReq = ForceFreeLeaseRequestDtoConverter.ConvertToDto(req);
                    upReq.ServerId = _serverProcessConfiguration.ServerId;

                    return client.ForceFreeLeaseAsync(upReq).ResponseAsync;
                },
                serverProcessConfiguration);
    }

    /// <summary>
    ///     Executes a transaction associated with an update.
    /// </summary>
    /// <param name="request">The update request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The update response.</returns>
    public override Task<UpdateResponseDto> Update(UpdateRequestDto request, ServerCallContext context)
    {
        _serverProcessConfiguration.WaitIfBeingSuspectedBy(request.ServerId);

        // Obtain the server id from the context.Peer string by searching the clients
        _logger.LogDebug(
            $"Received Update request from server {_serverProcessConfiguration.FindServerProcessId((int)request.ServerId)}, request: {request}");

        new Thread(() =>
            _updateFifoUrbReceiver.FifoUrbProcessRequest(UpdateRequestDtoConverter.ConvertFromDto(request))
        ).Start();

        _logger.LogDebug(
            $"Responding Update request from server {_serverProcessConfiguration.FindServerProcessId((int)request.ServerId)}, request: {request}");

        return Task.FromResult(new UpdateResponseDto { Ok = true });
    }

    public override Task<FreeLeaseResponseDto> FreeLease(FreeLeaseRequestDto request, ServerCallContext context)
    {
        _serverProcessConfiguration.WaitIfBeingSuspectedBy(request.ServerId);

        _logger.LogDebug("Received FreeLease request for lease {leaseId}",
            LeaseIdDtoConverter.ConvertFromDto(request.LeaseId));

        new Thread(() =>
        {
            _freeLeaseFifoUrbReceiver.FifoUrbProcessRequest(FreeLeaseRequestDtoConverter.ConvertFromDto(request));
        }).Start();

        return Task.FromResult(new FreeLeaseResponseDto { Ok = true });
    }

    public override Task<PrepareForFreeLeaseResponseDto> PrepareForFreeLease(PrepareForFreeLeaseRequestDto request,
        ServerCallContext context)
    {
        _serverProcessConfiguration.WaitIfBeingSuspectedBy(request.ServerId);

        _logger.LogDebug("Received PrepareForFreeLease request for lease {leaseId}",
            LeaseIdDtoConverter.ConvertFromDto(request.LeaseId));
        new Thread(() =>
        {
            _prepareForFreeLeaseFifoUrbReceiver.FifoUrbProcessRequest(
                PrepareForFreeLeaseRequestDtoConverter.ConvertFromDto(request));
        }).Start();

        return Task.FromResult(new PrepareForFreeLeaseResponseDto { Ok = true });
    }

    public override Task<ForceFreeLeaseResponseDto> ForceFreeLease(ForceFreeLeaseRequestDto request,
        ServerCallContext context)
    {
        _serverProcessConfiguration.WaitIfBeingSuspectedBy(request.ServerId);

        _logger.LogDebug("Received ForceFreeLease request for lease {leaseId}",
            LeaseIdDtoConverter.ConvertFromDto(request.LeaseId));

        new Thread(() =>
            _forceFreeLeaseFifoUrbReceiver.FifoUrbProcessRequest(
                ForceFreeLeaseRequestDtoConverter.ConvertFromDto(request))).Start();

        return Task.FromResult(new ForceFreeLeaseResponseDto { Ok = true });
    }

    /// <summary>
    ///     Delivers an update request.
    /// </summary>
    /// <param name="request">The update request.</param>
    private void UpdateFifoUrbDeliver(UpdateRequest request)
    {
        if (request.BroadcasterId == _serverProcessConfiguration.ServerId)
            return;

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

            _logger.LogDebug("Lease queues after update request: {leaseQueues}", _leaseQueues.ToString());
        }
    }
}