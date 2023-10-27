using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

/// <summary>
///     Implementation of the DadtkvService.
/// </summary>
public class DadtkvServiceImpl : DadtkvService.DadtkvServiceBase
{
    private const int ObtainLeaseTimeout = 4000;
    private readonly HashSet<LeaseId> _abortedTrans;
    private readonly DataStore _dataStore;
    private readonly Dictionary<LeaseId, bool> _executedTx;

    private readonly UrBroadcaster<ForceFreeLeaseRequest, ForceFreeLeaseResponseDto,
            StateUpdateService.StateUpdateServiceClient>
        _forceFreeLeaseUrbBroadcaster;

    private readonly LeaseQueues _leaseQueues;
    private readonly List<LeaseServiceClient> _leaseServiceClients;
    private readonly ILogger<DadtkvServiceImpl> _logger = DadtkvLogger.Factory.CreateLogger<DadtkvServiceImpl>();

    private readonly UrBroadcaster<PrepareForFreeLeaseRequest, PrepareForFreeLeaseResponseDto,
            StateUpdateService.StateUpdateServiceClient>
        _prepareForFreeLeaseUrbBroadcaster;

    private readonly ServerProcessConfiguration _serverProcessConfiguration;

    private readonly UrBroadcaster<UpdateRequest, UpdateResponseDto, StateUpdateService.StateUpdateServiceClient>
        _updateUrbBroadcaster;

    private ulong _leaseSequenceNumCounter;

    public DadtkvServiceImpl(ServerProcessConfiguration serverProcessConfiguration, DataStore dataStore,
        Dictionary<LeaseId, bool> executedTx, LeaseQueues leaseQueues, HashSet<LeaseId> abortedTrans)
    {
        _serverProcessConfiguration = serverProcessConfiguration;
        _dataStore = dataStore;
        _executedTx = executedTx;
        _leaseQueues = leaseQueues;
        _abortedTrans = abortedTrans;
        _leaseServiceClients = new List<LeaseServiceClient>();
        foreach (var leaseManager in _serverProcessConfiguration.LeaseManagers)
        {
            var channel = GrpcChannel.ForAddress(leaseManager.Url);
            _leaseServiceClients.Add(new LeaseServiceClient(new LeaseService.LeaseServiceClient(channel),
                leaseManager));
        }

        var stateUpdateServiceClients = new List<StateUpdateService.StateUpdateServiceClient>();
        foreach (var process in serverProcessConfiguration.OtherTransactionManagers)
        {
            var channel = GrpcChannel.ForAddress(process.Url);
            stateUpdateServiceClients.Add(new StateUpdateService.StateUpdateServiceClient(channel));
        }

        _updateUrbBroadcaster =
            new UrBroadcaster<UpdateRequest, UpdateResponseDto, StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients
            );

        _prepareForFreeLeaseUrbBroadcaster =
            new UrBroadcaster<PrepareForFreeLeaseRequest, PrepareForFreeLeaseResponseDto,
                StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients
            );

        _forceFreeLeaseUrbBroadcaster =
            new UrBroadcaster<ForceFreeLeaseRequest, ForceFreeLeaseResponseDto,
                StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients
            );
    }

    /// <summary>
    ///     Submit a transaction to be executed.
    /// </summary>
    /// <param name="request">The transaction to be executed.</param>
    /// <param name="context">The call context.</param>
    /// <returns>The result of the transaction.</returns>
    public override Task<TxSubmitResponseDto> TxSubmit(TxSubmitRequestDto request, ServerCallContext context)
    {
        lock (this) // One transaction at a time
        {
            return TxSubmit(request);
        }
    }

    /// <summary>
    ///     Submit a transaction to be executed.
    /// </summary>
    /// <param name="requestDto">The transaction to be executed.</param>
    /// <returns>The result of the transaction.</returns>
    private Task<TxSubmitResponseDto> TxSubmit(TxSubmitRequestDto requestDto)
    {
        lock (_leaseQueues)
        {
            var request = TxSubmitRequestDtoConverter.ConvertFromDto(requestDto);

            var leases = ExtractLeases(request);
            var leaseId = new LeaseId(_leaseSequenceNumCounter++, _serverProcessConfiguration.ServerId);

            var leaseReq = new LeaseRequest(leaseId, leases.ToList());
            _logger.LogDebug($"Received transaction from client : {request}, lease request id: {leaseReq.LeaseId}");

            // TODO: Optimization: Fast path
            foreach (var leaseServiceClient in _leaseServiceClients)
                // Get channel from client using reflection
                leaseServiceClient.Client.RequestLeaseAsync(LeaseRequestDtoConverter.ConvertToDto(leaseReq));

            _executedTx.Add(leaseId, false);

            var start = DateTime.Now;
            var obtainLeaseTimeoutTime = DateTime.MinValue;

            var i = 0;
            while (!_leaseQueues.ObtainedLeases(leaseReq))
            {
                if (i++ % 1000 == 0)
                    _logger.LogDebug(
                        "Waiting for leases: {leaseReq}, lease queues: {leaseQueues}, timeoutTime: {timeoutTime}",
                        leaseReq, _leaseQueues.ToString(), obtainLeaseTimeoutTime);

                lock (_abortedTrans)
                {
                    if (_abortedTrans.Contains(leaseId))
                    {
                        _logger.LogDebug("Transaction {leaseId} aborted (force freed by other transaction managers)",
                            leaseId);
                        return Task.FromResult(new TxSubmitResponseDto
                        {
                            ReadSet = { new DadIntDto { Key = "aborted", Value = 0 } }
                        });
                    }
                }

                var timeoutConflict = false;

                foreach (var key in leaseReq.Keys)
                {
                    if (!_leaseQueues.ContainsKey(key) || _leaseQueues[key].Count == 0)
                        continue;

                    var leaseOnTop = _leaseQueues[key].Peek();
                    if (leaseOnTop.Equals(leaseId))
                        continue;

                    timeoutConflict = true;
                }

                if (obtainLeaseTimeoutTime == DateTime.MinValue && timeoutConflict)
                {
                    obtainLeaseTimeoutTime = DateTime.Now.AddMilliseconds(ObtainLeaseTimeout);
                    _logger.LogDebug("Timeout defined for {timeoutTime}", obtainLeaseTimeoutTime);
                    continue;
                }

                if (DateTime.Now >= obtainLeaseTimeoutTime && obtainLeaseTimeoutTime != DateTime.MinValue)
                {
                    _logger.LogDebug("Timeout while waiting for leases: {leaseReq}, lease queues: {leaseQueues}",
                        leaseReq, _leaseQueues.ToString());

                    obtainLeaseTimeoutTime = DateTime.MinValue;

                    foreach (var key in leaseReq.Keys)
                    {
                        if (!_leaseQueues.ContainsKey(key) || _leaseQueues[key].Count == 0)
                            continue;

                        var leaseOnTop = _leaseQueues[key].Peek();
                        if (leaseOnTop.Equals(leaseId))
                            continue;

                        _logger.LogDebug("Sending prepare for forced free lease request for lease {leaseId}",
                            leaseOnTop);

                        _prepareForFreeLeaseUrbBroadcaster.UrBroadcast(
                            new PrepareForFreeLeaseRequest(
                                _serverProcessConfiguration.ServerId,
                                _serverProcessConfiguration.ServerId,
                                leaseOnTop
                            ),
                            _ =>
                            {
                                _logger.LogDebug("Sending forced free lease request for lease {leaseId}",
                                    leaseOnTop);

                                _forceFreeLeaseUrbBroadcaster.UrBroadcast(
                                    new ForceFreeLeaseRequest(
                                        _serverProcessConfiguration.ServerId,
                                        _serverProcessConfiguration.ServerId,
                                        leaseOnTop
                                    ),
                                    _ => { },
                                    (client, req) =>
                                        client.ForceFreeLeaseAsync(
                                                ForceFreeLeaseRequestDtoConverter.ConvertToDto(req))
                                            .ResponseAsync
                                );
                            },
                            /*predicate: responseDto =>
                            {
                                _logger.LogDebug("Received response for prepare for free lease request for lease {leaseId}: {responseDto}",
                                    leaseOnTop, responseDto.Ok);
                                return responseDto.Ok;
                            },*/
                            (client, req) =>
                                client.PrepareForFreeLeaseAsync(
                                        PrepareForFreeLeaseRequestDtoConverter.ConvertToDto(req))
                                    .ResponseAsync
                        );
                        break;
                    }
                }

                Monitor.Exit(_leaseQueues);
                Thread.Sleep(10);
                Monitor.Enter(_leaseQueues);
            }

            var end = DateTime.Now;
            _logger.LogDebug("Time taken to obtain leases: {timeTaken} ms", (end - start).TotalMilliseconds);

            // TODO put to false and add free lease request handler
            var conflict = true;
            foreach (var (_, queue) in _leaseQueues)
                if (queue.Count > 0 && queue.Peek().Equals(leaseId) && queue.Count > 1)
                {
                    conflict = true;
                    break;
                }

            // Commit transaction
            var readData = ExecuteTransaction(leaseId, request.ReadSet, request.WriteSet.ToList(), conflict);

            if (readData.Count == 1 && readData[0].Key == "aborted")
                _logger.LogDebug($"Transaction {request} aborted");
            else
                _logger.LogDebug($"Transaction {request} executed successfully");

            return Task.FromResult(new TxSubmitResponseDto
            {
                ReadSet = { readData.Select(DadIntDtoConverter.ConvertToDto) }
            });
        }
    }

    /// <summary>
    ///     Execute a transaction.
    /// </summary>
    /// <param name="leaseId">The lease id.</param>
    /// <param name="readSet">The keys to read.</param>
    /// <param name="writeSet">The keys and values to write.</param>
    /// <param name="freeLease">Whether to free the lease after the transaction.</param>
    /// <returns>The values read.</returns>
    private List<DadInt> ExecuteTransaction(
        LeaseId leaseId,
        IEnumerable<string> readSet,
        List<DadInt> writeSet,
        bool freeLease
    )
    {
        List<DadInt> returnReadSet;

        _logger.LogDebug("Sending update request for lease {leaseId}{freeingLeaseString}", leaseId,
            freeLease ? " and freeing the lease." : ".");
        var majority = false;

        _updateUrbBroadcaster.UrBroadcast(
            new UpdateRequest(
                _serverProcessConfiguration.ServerId,
                _serverProcessConfiguration.ServerId,
                leaseId,
                writeSet,
                freeLease
            ),
            _ => { majority = true; },
            (client, req) => client.UpdateAsync(UpdateRequestDtoConverter.ConvertToDto(req)).ResponseAsync
        );

        if (!majority)
            return new List<DadInt> { new("aborted", 0) };

        lock (_dataStore)
        {
            returnReadSet = _dataStore.ExecuteTransaction(readSet, writeSet);
            _executedTx[leaseId] = true;
        }

        if (freeLease)
            _leaseQueues.FreeLeases(leaseId);

        // If we don't get a majority, maybe we should send aborted to client
        return returnReadSet;
    }

    /// <summary>
    ///     Extract the leases from a transaction.
    /// </summary>
    /// <param name="request">The transaction.</param>
    /// <returns>The leases.</returns>
    private static HashSet<string> ExtractLeases(TxSubmitRequest request)
    {
        var leases = new HashSet<string>();
        foreach (var lease in request.WriteSet.Select(x => x.Key))
            leases.Add(lease);

        foreach (var lease in request.ReadSet)
            leases.Add(lease);

        return leases;
    }

    /// <summary>
    ///     Get the status of the service.
    ///     The status includes the data store and the consensus state.
    /// </summary>
    /// <param name="request">The status request.</param>
    /// <param name="context">The call context.</param>
    /// <returns>The status response.</returns>
    public override Task<StatusResponseDto> Status(StatusRequestDto request, ServerCallContext context)
    {
        var status = new List<string>();
        lock (this)
        {
            status.Add(_dataStore.ToString());
            // status.Add(_consensusState.ToString());

            return Task.FromResult(new StatusResponseDto { Status = { status } });
        }
    }
}