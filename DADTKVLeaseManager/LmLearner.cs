using System.Diagnostics;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

// TODO: Rename to Learner? Is inside the LearnerManager project.

/// <summary>
///     The learner is responsible for learning the decided value for a Paxos round.
/// </summary>
public class LmLearner : LearnerService.LearnerServiceBase
{
    private readonly ConsensusState _consensusState;
    private readonly object _consensusStateLockObject = new();
    private readonly UrbReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _urbReceiver;

    public LmLearner(ProcessConfiguration processConfiguration, ConsensusState consensusState)
    {
        _consensusState = consensusState;

        var learnerServiceClients = processConfiguration.OtherServerProcesses
            .Select(process => GrpcChannel.ForAddress(process.Url))
            .Select(channel => new LearnerService.LearnerServiceClient(channel))
            .ToList();

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

            if (_consensusState.Values[(int)request.RoundNumber] != null)
                Debug.WriteLine($"Value for the round already exists." +
                                $"Previous: {_consensusState.Values[(int)request.RoundNumber]}, " +
                                $"Current: {ConsensusValueDtoConverter.ConvertFromDto(request.ConsensusValue)}");

            _consensusState.Values[(int)request.RoundNumber] =
                ConsensusValueDtoConverter.ConvertFromDto(request.ConsensusValue);
        }
    }
}