using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

/// <summary>
///     The learner is responsible for learning the decided value for a Paxos round.
/// </summary>
public class TmLearner : LearnerService.LearnerServiceBase
{
    private readonly Dictionary<LeaseId, bool> _executedTrans;
    private readonly LeaseQueues _leaseQueues;
    private readonly ProcessConfiguration _processConfiguration;
    private readonly TobReceiver<LearnRequest, LearnResponseDto, LearnerService.LearnerServiceClient> _tobReceiver;

    private readonly UrBroadcaster<FreeLeaseRequest, FreeLeaseResponseDto, StateUpdateService.StateUpdateServiceClient>
        _urBroadcaster;

    private readonly ILogger<TmLearner> _logger = DadtkvLogger.Factory.CreateLogger<TmLearner>();

    public TmLearner(ProcessConfiguration processConfiguration,
        Dictionary<LeaseId, bool> executedTrans,
        LeaseQueues leaseQueues)
    {
        _processConfiguration = processConfiguration;
        _executedTrans = executedTrans;
        _leaseQueues = leaseQueues;

        var learnerServiceClients = new List<LearnerService.LearnerServiceClient>();
        foreach (var process in processConfiguration.OtherServerProcesses)
        {
            var channel = GrpcChannel.ForAddress(process.Url);
            learnerServiceClients.Add(new LearnerService.LearnerServiceClient(channel));
        }

        var stateUpdateServiceClients = new List<StateUpdateService.StateUpdateServiceClient>();
        foreach (var process in processConfiguration.OtherTransactionManagers)
        {
            var channel = GrpcChannel.ForAddress(process.Url);
            stateUpdateServiceClients.Add(new StateUpdateService.StateUpdateServiceClient(channel));
        }

        _tobReceiver = new TobReceiver<LearnRequest, LearnResponseDto, LearnerService.LearnerServiceClient>(
            learnerServiceClients,
            TobDeliver,
            (client, req) => client.LearnAsync(LearnRequestDtoConverter.ConvertToDto(req)).ResponseAsync,
            processConfiguration
        );

        _urBroadcaster =
            new UrBroadcaster<FreeLeaseRequest, FreeLeaseResponseDto, StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients
            );
    }

    /// <summary>
    ///     Receive a learn request from the proposer, which has decided on a value for the round.
    /// </summary>
    /// <param name="request">The learn request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The learn response.</returns>
    public override Task<LearnResponseDto> Learn(LearnRequestDto request, ServerCallContext context)
    {
        _tobReceiver.TobProcessRequest(LearnRequestDtoConverter.ConvertFromDto(request));
        return Task.FromResult(new LearnResponseDto { Ok = true });
    }

    /// <summary>
    ///     Deliver the value to the consensus state.
    /// </summary>
    /// <param name="request">The learn request.</param>
    /// <exception cref="Exception">If the value for the round already exists.</exception>
    private void TobDeliver(LearnRequest request)
    {
        lock (_leaseQueues)
        {
            request.ConsensusValue.LeaseRequests.ForEach(leaseRequest =>
            {
                leaseRequest.Keys.ForEach(key =>
                {
                    if (!_leaseQueues.ContainsKey(key))
                        _leaseQueues.Add(key, new Queue<LeaseId>());

                    _leaseQueues[key].Enqueue(leaseRequest.LeaseId);
                });
            });

            var leasesToBeFreed = new HashSet<LeaseId>();

            foreach (var (key, queue) in _leaseQueues)
            {
                if (queue.Count == 0)
                    continue;

                var leaseId = queue.Peek();

                if (leaseId.ServerId == _processConfiguration.ServerId && queue.Count > 1 && _executedTrans[leaseId])
                {
                    leasesToBeFreed.Add(leaseId);
                    queue.Dequeue();
                }
            }

            _logger.LogDebug($"Received learn request: {request}");
            _logger.LogDebug($"Leases that were freed: {leasesToBeFreed.ToStringRep()}");
            _logger.LogDebug($"Lease queues after learn request: {_leaseQueues}");

            foreach (var leaseId in leasesToBeFreed)
                _urBroadcaster.UrBroadcast(
                    new FreeLeaseRequest(_processConfiguration.ServerId, leaseId),
                    (client, req) => client.FreeLeaseAsync(FreeLeaseRequestDtoConverter.ConvertToDto(req)).ResponseAsync
                );
        }
    }
}