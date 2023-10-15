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
    private readonly ConsensusState _consensusState;
    private readonly object _consensusStateLockObject = new();
    private readonly List<LeaseService.LeaseServiceClient> _leaseServiceClients;
    private readonly TobReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _tobReceiver;

    public TmLearner(ProcessConfiguration processConfiguration, ConsensusState consensusState,
        Dictionary<LeaseId, bool> executedTrans, HashSet<LeaseId> freedLeases)
    {
        _consensusState = consensusState;

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

        _tobReceiver = new TobReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient>(
            learnerServiceClients,
            TobDeliver,
            req => req.RoundNumber,
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
        lock (_consensusStateLockObject)
        {
            ResizeConsensusStateList((int)request.RoundNumber);

            var consensusValue = ConsensusValueDtoConverter.ConvertFromDto(request.ConsensusValue);
            _consensusState.Values[(int)request.RoundNumber] = consensusValue;

            Console.Write(
                $"Received instance: {consensusValue} from {request.ServerId} with seq number " +
                $"{request.SequenceNum} and round number {request.RoundNumber}"
            );
        }
    }
}