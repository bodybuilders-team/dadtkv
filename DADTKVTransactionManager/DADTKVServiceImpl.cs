using Grpc.Core;
using Grpc.Net.Client;

namespace Dadtkv;

/// <summary>
///     Implementation of the DadtkvService.
/// </summary>
public class DadtkvServiceImpl : DadtkvService.DadtkvServiceBase
{
    private readonly DataStore _dataStore;
    private readonly Dictionary<LeaseId, bool> _executedTrans;
    private readonly LeaseQueues _leaseQueues;
    private readonly List<LeaseService.LeaseServiceClient> _leaseServiceClients;
    private readonly ProcessConfiguration _processConfiguration;

    private readonly UrBroadcaster<UpdateRequest, UpdateResponseDto, StateUpdateService.StateUpdateServiceClient>
        _urBroadcaster;

    private ulong _leaseSequenceNumCounter;

    public DadtkvServiceImpl(ProcessConfiguration processConfiguration, DataStore dataStore,
        Dictionary<LeaseId, bool> executedTrans, LeaseQueues leaseQueues)
    {
        _processConfiguration = processConfiguration;
        _dataStore = dataStore;
        _executedTrans = executedTrans;
        _leaseQueues = leaseQueues;
        _leaseServiceClients = new List<LeaseService.LeaseServiceClient>();
        foreach (var leaseManager in _processConfiguration.LeaseManagers)
        {
            var channel = GrpcChannel.ForAddress(leaseManager.Url);
            _leaseServiceClients.Add(new LeaseService.LeaseServiceClient(channel));
        }

        var stateUpdateServiceClients = new List<StateUpdateService.StateUpdateServiceClient>();
        foreach (var process in processConfiguration.OtherTransactionManagers)
        {
            var channel = GrpcChannel.ForAddress(process.Url);
            stateUpdateServiceClients.Add(new StateUpdateService.StateUpdateServiceClient(channel));
        }

        _urBroadcaster =
            new UrBroadcaster<UpdateRequest, UpdateResponseDto, StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients
            );

        _processConfiguration.OtherTransactionManagers
            .Select(tm => GrpcChannel.ForAddress(tm.Url))
            .Select(channel => new StateUpdateService.StateUpdateServiceClient(channel))
            .ForEach(client => stateUpdateServiceClients.Add(client));
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
    /// <param name="request">The transaction to be executed.</param>
    /// <returns>The result of the transaction.</returns>
    private Task<TxSubmitResponseDto> TxSubmit(TxSubmitRequestDto request)
    {
        lock (_leaseQueues)
        {
            var leases = ExtractLeases(request);

            // TODO: Optimization: Fast path

            var leaseId = new LeaseId(
                _leaseSequenceNumCounter++,
                _processConfiguration.ServerId
            );

            var leaseReq = new LeaseRequest(leaseId, leases.ToList());

            foreach (var leaseServiceClient in _leaseServiceClients)
                leaseServiceClient.RequestLeaseAsync(LeaseRequestDtoConverter.ConvertToDto(leaseReq));

            _executedTrans.Add(leaseId, false);


            var start = DateTime.Now;
            while (!_leaseQueues.ObtainedLeases(leaseReq))
            {
                Monitor.Exit(_leaseQueues);
                Thread.Sleep(10);
                Monitor.Enter(_leaseQueues);
            }

            var end = DateTime.Now;
            Console.WriteLine("Time taken to obtain leases: " + (end - start).TotalMilliseconds + "ms");

            // TODO put to false and add free lease request handler
            var conflict = true;
            foreach (var (key, queue) in _leaseQueues)
                if (queue.Peek().Equals(leaseId) && queue.Count > 1)
                {
                    conflict = true;
                    break;
                }

            // Commit transaction
            var readData = ExecuteTransaction(leaseId, request.ReadSet, request.WriteSet.ToList(), conflict);

            return Task.FromResult(new TxSubmitResponseDto { ReadSet = { readData } });
        }
    }

    /// <summary>
    ///     Execute a transaction.
    /// </summary>
    /// <param name="readSet">The keys to read.</param>
    /// <param name="writeSet">The keys and values to write.</param>
    /// <returns>The values read.</returns>
    private IEnumerable<DadInt> ExecuteTransaction(LeaseId leaseId, IEnumerable<string> readSet,
        List<DadInt> writeSet,
        bool freeLease)
    {
        List<DadInt> returnReadSet;

        lock (_dataStore)
        {
            returnReadSet = _dataStore.ExecuteTransaction(readSet, writeSet);
            _executedTrans[leaseId] = true;
        }

        if (freeLease) _leaseQueues.FreeLeases(leaseId);

        _urBroadcaster.UrBroadcast(
            new UpdateRequest(
                _processConfiguration,
                _processConfiguration.ServerId,
                leaseId,
                writeSet,
                freeLease
            ),
            req => { },
            (client, req) => client.UpdateAsync(UpdateRequestDtoConverter.ConvertToDto(req)).ResponseAsync
        );

        return returnReadSet;
    }

    /// <summary>
    ///     Extract the leases from a transaction.
    /// </summary>
    /// <param name="request">The transaction.</param>
    /// <returns>The leases.</returns>
    private static HashSet<string> ExtractLeases(TxSubmitRequestDto request)
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