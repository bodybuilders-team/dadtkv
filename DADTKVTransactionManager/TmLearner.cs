using System.Text;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

// TODO: Rename to Learner? Is inside the LearnerManager project.

/// <summary>
///     The learner is responsible for learning the decided value for a Paxos round.
/// </summary>
public class TmLearner : LearnerService.LearnerServiceBase
{
    private readonly ConsensusState _consensusState;
    private readonly object _consensusStateLockObject = new();
    private readonly Dictionary<LeaseId, bool> _executedTrans;
    private readonly List<LeaseService.LeaseServiceClient> _leaseServiceClients;
    private readonly ProcessConfiguration _processConfiguration;
    private readonly UrbReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _urbReceiver;

    public TmLearner(ProcessConfiguration processConfiguration, ConsensusState consensusState,
        Dictionary<LeaseId, bool> executedTrans)
    {
        _processConfiguration = processConfiguration;
        _consensusState = consensusState;
        _executedTrans = executedTrans;

        var learnerServiceClients = new List<LearnerService.LearnerServiceClient>();
        foreach (var process in processConfiguration.OtherServerProcesses)
        {
            var channel = GrpcChannel.ForAddress(process.Url);
            learnerServiceClients.Add(new LearnerService.LearnerServiceClient(channel));
        }

        _leaseServiceClients = new List<LeaseService.LeaseServiceClient>();
        foreach (var process in processConfiguration.LeaseManagers)
        {
            var channel = GrpcChannel.ForAddress(process.Url);
            _leaseServiceClients.Add(new LeaseService.LeaseServiceClient(channel));
        }

        _urbReceiver = new UrbReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient>(
            learnerServiceClients,
            LearnUrbDeliver,
            req => req.ServerId + req.SequenceNum,
            (client, req) => client.LearnAsync(req).ResponseAsync
        );
    }

    /// <summary>
    ///     Resize the consensus state list to fit the round number.
    /// </summary>
    /// <param name="roundNumber">The round number.</param>
    private void ResizeConsensusStateList(int roundNumber)
    {
        lock (_consensusStateLockObject)
        {
            for (var i = _consensusState.Values.Count; i <= roundNumber; i++)
                _consensusState.Values.Add(null);
        }
    }

    /// <summary>
    ///     Receive a learn request from the proposer, which has decided on a value for the round.
    /// </summary>
    /// <param name="request">The learn request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The learn response.</returns>
    public override Task<LearnResponse> Learn(LearnRequest request, ServerCallContext context)
    {
        _urbReceiver.UrbProcessRequest(request);
        return Task.FromResult(new LearnResponse { Ok = true });
    }

    /// <summary>
    ///     Deliver the value to the consensus state.
    /// </summary>
    /// <param name="request">The learn request.</param>
    /// <exception cref="Exception">If the value for the round already exists.</exception>
    private void LearnUrbDeliver(LearnRequest request)
    {
        lock (_consensusStateLockObject)
        {
            ResizeConsensusStateList((int)request.RoundNumber);
            
            var consensusValue = ConsensusValueDtoConverter.ConvertFromDto(request.ConsensusValue);
            _consensusState.Values[(int)request.RoundNumber] = consensusValue;

            var leasesToBeFreed = new HashSet<LeaseId>();
            foreach (var (key, queue) in consensusValue.LeaseQueues)
            {
                if(queue.Count == 0)
                    continue;
                
                var leaseId = queue.Peek();

                if (leaseId.ServerId == _processConfiguration.ProcessInfo.Id && queue.Count > 1 &&
                    _executedTrans[leaseId]
                   )
                    leasesToBeFreed.Add(leaseId);
            }
            
            // Create string with list of leases to be freed
            var sb = new StringBuilder();
            foreach (var leaseId in leasesToBeFreed)
            {
                sb.Append(leaseId);
                sb.Append(", ");
            }

            Console.Write($"Received instance: {consensusValue} from {request.ServerId} with seq number {request.SequenceNum} and round number {request.RoundNumber} and will free the following leases: [{sb}] \n\n");
            
            foreach (var leaseId in leasesToBeFreed)
            foreach (var leaseServiceClient in _leaseServiceClients)
                leaseServiceClient.FreeLeaseAsync(new FreeLeaseRequest
                {
                    LeaseId = LeaseIdDtoConverter.ConvertToDto(leaseId)
                });
        }
    }
}