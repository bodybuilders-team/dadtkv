using DADTKV;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKVT;

/// <summary>
///     Implementation of the DADTKVService.
/// </summary>
public class DADTKVServiceImpl : DADTKVService.DADTKVServiceBase
{
    private readonly ConsensusState _consensusState;
    private readonly DataStore _dataStore;
    private readonly Dictionary<LeaseId, bool> _executedTrans; //TODO: Maybe convert to hashset
    private readonly List<LeaseService.LeaseServiceClient> _leaseServiceClients;
    private readonly ServerProcessConfiguration _serverProcessConfiguration;
    private readonly List<StateUpdateService.StateUpdateServiceClient> _stateUpdateServiceClients = new();
    private ulong _leaseSequenceNumCounter;
    private ulong _susSequenceNumCounter;

    public DADTKVServiceImpl(ServerProcessConfiguration serverProcessConfiguration,
        ConsensusState consensusState, DataStore dataStore, Dictionary<LeaseId, bool> executedTrans)
    {
        _serverProcessConfiguration = serverProcessConfiguration;
        _consensusState = consensusState;
        _dataStore = dataStore;
        _executedTrans = executedTrans;
        _leaseServiceClients = new List<LeaseService.LeaseServiceClient>();
        foreach (var leaseManager in _serverProcessConfiguration.LeaseManagers)
        {
            var channel = GrpcChannel.ForAddress(leaseManager.Url);
            _leaseServiceClients.Add(new LeaseService.LeaseServiceClient(channel));
        }

        _serverProcessConfiguration.OtherTransactionManagers
            .Select(tm => GrpcChannel.ForAddress(tm.Url))
            .Select(channel => new StateUpdateService.StateUpdateServiceClient(channel))
            .ForEach(client => _stateUpdateServiceClients.Add(client));
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
            ServerId = _serverProcessConfiguration.ProcessInfo.Id,
            SequenceNum = _leaseSequenceNumCounter++
        };

        var leaseReq = new LeaseRequest
        {
            LeaseId = LeaseIdDtoConverter.ConvertToDto(leaseId),
            Set = { leases }
        };

        foreach (var leaseServiceClient in _leaseServiceClients) leaseServiceClient.RequestLeaseAsync(leaseReq);

        _executedTrans.Add(leaseId, false);

        // TODO: Optimization
        // Verify if we need to check leases of this leaseReq.leaseId or just the lease keys

        // Waiting for consensus value where lease id are on the top of the queue
        var reachedConsensus = false;
        while (!reachedConsensus)
        {
            for (var i = _consensusState.Values.Count - 1; i >= 0; i--)
            {
                var consensusValue = _consensusState.Values[i];

                if (consensusValue == null || !CheckLeases(consensusValue, leaseReq))
                    continue;

                reachedConsensus = true;
            }

            if (reachedConsensus) break;

            Thread.Sleep(100);
        }

        // Commit transaction
        var readData = ExecuteTransaction(request.ReadSet, request.WriteSet);

        // TODO: Optimization
        // Check conflicts in our lease request
        // Immediately free the lease request if there is conflict in any of the leases

        _executedTrans[leaseId] = true;

        return Task.FromResult(new TxSubmitResponse { ReadSet = { readData } });
    }

    /// <summary>
    ///     Execute a transaction.
    /// </summary>
    /// <param name="readSet">The keys to read.</param>
    /// <param name="writeSet">The keys and values to write.</param>
    /// <returns>The values read.</returns>
    private IEnumerable<DadInt> ExecuteTransaction(IEnumerable<string> readSet, IEnumerable<DadInt> writeSet)
    {
        var resTasks = new List<Task<UpdateResponse>>();
        foreach (var susClient in _stateUpdateServiceClients)
        {
            var updateReq = new UpdateRequest
            {
                ServerId = _serverProcessConfiguration.ProcessInfo.Id,
                SequenceNum = _susSequenceNumCounter++,
                WriteSet = { writeSet }
            };

            //TODO: Check if throws exception when timeout
            var res = susClient.UpdateAsync(updateReq);
            resTasks.Add(res.ResponseAsync);
        }

        Task.WaitAll(resTasks.ToArray());

        lock (_dataStore)
        {
            return _dataStore.ExecuteTransaction(readSet, writeSet);
        }
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
    ///     Checks if the leases of a request are on the top of the queue.
    /// </summary>
    /// <param name="consensusStateValue">The consensus value.</param>
    /// <param name="leaseReq">The lease request.</param>
    /// <returns>True if the leases are on the top of the queue, false otherwise.</returns>
    private static bool CheckLeases(ConsensusValue consensusStateValue, LeaseRequest leaseReq)
    {
        var leaseId = LeaseIdDtoConverter.ConvertFromDto(leaseReq.LeaseId);

        foreach (var lease in leaseReq.Set)
            if (!consensusStateValue.LeaseQueues.ContainsKey(lease) ||
                !consensusStateValue.LeaseQueues[lease].Peek().Equals(leaseId)
               )
                return false;

        return true;
    }

    private bool CheckLeases(ConsensusValue consensusStateValue, HashSet<string> leases)
    {
        var queues = consensusStateValue.LeaseQueues;

        var leaseIds = new HashSet<LeaseId>();

        foreach (var lease in leases)
        {
            var leaseId = queues[lease].Peek();

            if (leaseId.ServerId != _serverProcessConfiguration.ProcessInfo.Id)
                return false;

            leaseIds.Add(leaseId);
        }

        return checkForFreeLeaseReq(leaseIds);
    }

    private bool checkForFreeLeaseReq(HashSet<LeaseId> leaseIds)
    {
        throw new NotImplementedException();
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
            status.Add(_consensusState.ToString());

            return Task.FromResult(new StatusResponse { Status = { status } });
        }
    }
}