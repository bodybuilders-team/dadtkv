using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

/// <summary>
///     The learner is responsible for learning the decided value for a Paxos round.
/// </summary>
public class LmLearner : LearnerService.LearnerServiceBase
{
    private readonly ConsensusState _consensusState;

    private readonly ILogger<LmLearner> _logger = DadtkvLogger.Factory.CreateLogger<LmLearner>();
    private readonly ServerProcessConfiguration _serverProcessConfiguration;
    private readonly UrbReceiver<LearnRequest, LearnResponseDto, LearnerService.LearnerServiceClient> _urbReceiver;

    public LmLearner(ServerProcessConfiguration serverProcessConfiguration, ConsensusState consensusState)
    {
        _consensusState = consensusState;
        _serverProcessConfiguration = serverProcessConfiguration;

        var learnerServiceClients = serverProcessConfiguration.OtherServerProcesses
            .Select(process => GrpcChannel.ForAddress(process.Url))
            .Select(channel => new LearnerService.LearnerServiceClient(channel))
            .ToList();

        _urbReceiver = new UrbReceiver<LearnRequest, LearnResponseDto, LearnerService.LearnerServiceClient>(
            learnerServiceClients,
            LearnUrbDeliver,
            (client, req) => client.LearnAsync(LearnRequestDtoConverter.ConvertToDto(req)).ResponseAsync,
            serverProcessConfiguration
        );
    }

    /// <summary>
    ///     Resize the consensus state list to fit the round number.
    /// </summary>
    /// <param name="roundNumber">The round number.</param>
    private void ResizeConsensusStateList(int roundNumber)
    {
        for (var i = _consensusState.Values.Count; i <= roundNumber; i++)
            _consensusState.Values.Add(null);
    }

    /// <summary>
    ///     Receive a learn request from the proposer, which has decided on a value for the round.
    /// </summary>
    /// <param name="request">The learn request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The learn response.</returns>
    public override Task<LearnResponseDto> Learn(LearnRequestDto request, ServerCallContext context)
    {
        _serverProcessConfiguration.WaitIfBeingSuspectedBy(request.ServerId);

        var req = LearnRequestDtoConverter.ConvertFromDto(request);
        req.ServerId = _serverProcessConfiguration.ServerId;

        _urbReceiver.UrbProcessRequest(req);

        return Task.FromResult(new LearnResponseDto { Ok = true });
    }

    /// <summary>
    ///     Deliver the value to the consensus state.
    /// </summary>
    /// <param name="request">The learn request.</param>
    /// <exception cref="Exception">If the value for the round already exists.</exception>
    private void LearnUrbDeliver(LearnRequest request)
    {
        lock (_consensusState)
        {
            ResizeConsensusStateList((int)request.RoundNumber);

            if (_consensusState.Values[(int)request.RoundNumber] != null)
                _logger.LogDebug($"Value for the round already exists." +
                                 $"Previous: {_consensusState.Values[(int)request.RoundNumber]}, " +
                                 $"Current: {request.ConsensusValue}");

            _logger.LogDebug($"Learned value for round {request.RoundNumber}: {request.ConsensusValue}");
            _consensusState.Values[(int)request.RoundNumber] = request.ConsensusValue;
        }
    }
}