using DADTKV;
using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKVT;

/// <summary>
///     Implementation of the DADTKVService.
/// </summary>
public class DADTKVServiceImpl : DADTKVService.DADTKVServiceBase
{
    private readonly DataStore _dataStore;
    private readonly Dictionary<LeaseId, bool> _executedTrans; //TODO: Maybe convert to hashset
    private readonly LeaseQueues _leaseQueues;
    private readonly List<LeaseService.LeaseServiceClient> _leaseServiceClients;
    private readonly ProcessConfiguration _processConfiguration;
    private ulong _leaseSequenceNumCounter;

    private readonly UrBroadcaster<UpdateRequest, UpdateResponse, StateUpdateService.StateUpdateServiceClient>
        _urBroadcaster;

    public DADTKVServiceImpl(ProcessConfiguration processConfiguration, DataStore dataStore,
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
            new UrBroadcaster<UpdateRequest, UpdateResponse, StateUpdateService.StateUpdateServiceClient>(
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
    public override Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request, ServerCallContext context)
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
    private Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request)
    {
        var leases = ExtractLeases(request);

        //TODO: Validate if we already have lease and there is no conflict
        // Waiting for consensus value where lease id are on the top of the queue

        //TODO: Optimization
        // Fast path, can only execute if all the lease ids we are using have not been sent free lease req
        // if (_consensusState.Value != null && CheckLeases(_consensusState.Value, leases))
        // {
        //     ExecuteTransaction(request.ReadSet, request.WriteSet);
        // }

        var leaseId = new LeaseId
        {
            ServerId = _processConfiguration.ProcessInfo.Id,
            SequenceNum = _leaseSequenceNumCounter++
        };

        var leaseReq = new LeaseRequest
        {
            LeaseId = leaseId,
            Set = leases.ToList()
        };

        foreach (var leaseServiceClient in _leaseServiceClients)
            leaseServiceClient.RequestLeaseAsync(LeaseRequestDtoConverter.ConvertToDto(leaseReq));

        _executedTrans.Add(leaseId, false);

        while (!_leaseQueues.ObtainedLeases(leaseReq))
            Thread.Sleep(100);

        var conflict = false;
        foreach (var (key, queue) in _leaseQueues)
        {
            if (queue.Peek().Equals(leaseId) && queue.Count > 1)
            {
                conflict = true;
                break;
            }
        }

        // Commit transaction
        var readData = ExecuteTransaction(leaseId, request.ReadSet, request.WriteSet, conflict);

        _executedTrans[leaseId] = true;


        return Task.FromResult(new TxSubmitResponse { ReadSet = { readData } });
    }

    /// <summary>
    ///     Execute a transaction.
    /// </summary>
    /// <param name="readSet">The keys to read.</param>
    /// <param name="writeSet">The keys and values to write.</param>
    /// <returns>The values read.</returns>
    private IEnumerable<DadInt> ExecuteTransaction(LeaseId leaseId, IEnumerable<string> readSet,
        IEnumerable<DadInt> writeSet,
        bool freeLease)
    {
        List<DadInt> returnReadSet;

        lock (_dataStore)
        {
            returnReadSet = _dataStore.ExecuteTransaction(readSet, writeSet);
        }

        _urBroadcaster.UrBroadcast(
            new UpdateRequest
            {
                ServerId = _processConfiguration.ProcessInfo.Id,
                LeaseId = LeaseIdDtoConverter.ConvertToDto(leaseId),
                WriteSet = { writeSet },
                FreeLease = freeLease
            },
            (req, seq) => req.SequenceNum = seq,
            req => { },
            (client, req) => client.UpdateAsync(req).ResponseAsync
        );

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
    public override Task<StatusResponse> Status(StatusRequest request, ServerCallContext context)
    {
        var status = new List<string>();
        lock (this)
        {
            status.Add(_dataStore.ToString());
            // status.Add(_consensusState.ToString());

            return Task.FromResult(new StatusResponse { Status = { status } });
        }
    }
}