using System.Text;
using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

/// <summary>
///     The learner is responsible for learning the decided value for a Paxos round.
/// </summary>
public class TmLearner : LearnerService.LearnerServiceBase
{
    private readonly ProcessConfiguration _processConfiguration;
    private readonly Dictionary<LeaseId, bool> _executedTrans;
    private readonly LeaseQueues _leaseQueues;
    private readonly TobReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _tobReceiver;

    private readonly UrBroadcaster<FreeLeaseRequest, FreeLeaseResponse, StateUpdateService.StateUpdateServiceClient>
        _urBroadcaster;

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

        _tobReceiver = new TobReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient>(
            learnerServiceClients,
            TobDeliver,
            req => req.RoundNumber,
            (client, req) => client.LearnAsync(req).ResponseAsync
        );

        _urBroadcaster =
            new UrBroadcaster<FreeLeaseRequest, FreeLeaseResponse, StateUpdateService.StateUpdateServiceClient>(
                stateUpdateServiceClients
            );
    }

    /// <summary>
    ///     Receive a learn request from the proposer, which has decided on a value for the round.
    /// </summary>
    /// <param name="request">The learn request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The learn response.</returns>
    public override Task<LearnResponse> Learn(LearnRequest request, ServerCallContext context)
    {
        _tobReceiver.TobProcessRequest(request);
        return Task.FromResult(new LearnResponse { Ok = true });
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
                leaseRequest.Set.ForEach(key =>
                {
                    if (!_leaseQueues.ContainsKey(key))
                    {
                        _leaseQueues.Add(key, new Queue<LeaseId>());
                    }

                    _leaseQueues[key].Enqueue(LeaseIdDtoConverter.ConvertFromDto(leaseRequest.LeaseId));
                });
            });

            var leasesToBeFreed = new HashSet<LeaseId>();

            foreach (var (key, queue) in _leaseQueues)
            {
                if (queue.Count == 0)
                    continue;

                var leaseId = queue.Peek();

                if (leaseId.ServerId == _processConfiguration.ProcessInfo.Id && queue.Count > 1 &&
                    _executedTrans[leaseId]
                   )
                {
                    leasesToBeFreed.Add(leaseId);
                    queue.Dequeue();
                }
            }
            
            foreach (var leaseId in leasesToBeFreed)
            {
                // TODO abstract FreeLeaseRequest to remove updateSequenceNumber
                _urBroadcaster.UrBroadcast(
                    new FreeLeaseRequest { LeaseId = LeaseIdDtoConverter.ConvertToDto(leaseId) },
                    (req, seq) => req.SequenceNum = seq,
                    (client, req) => client.FreeLeaseAsync(req).ResponseAsync
                );
            }
        }
    }
}