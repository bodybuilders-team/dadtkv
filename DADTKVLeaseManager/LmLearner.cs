using System.Diagnostics;
using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

public class LmLearner : LearnerService.LearnerServiceBase
{
    private readonly ConsensusState _consensusState;
    private readonly object _consensusStateLockObject = new();
    private readonly UrbReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _urbReceiver;

    public LmLearner(ProcessConfiguration processConfiguration, ConsensusState consensusState)
    {
        _consensusState = consensusState;

        var learnerServiceClients =
            processConfiguration.OtherServerProcesses
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

    private void ResizeConsensusStateList(int roundNumber)
    {
        lock (_consensusStateLockObject)
        {
            for (var i = _consensusState.Values.Count; i <= roundNumber; i++)
                _consensusState.Values.Add(null);
        }
    }

    /**
     * Receive a learn request from the proposer, which has decided on a value for the round.
     */
    public override Task<LearnResponse> Learn(LearnRequest request, ServerCallContext context)
    {
        _urbReceiver.UrbProcessRequest(request);

        return Task.FromResult(new LearnResponse
        {
            Ok = true
        });
    }

    /**
     * Deliver the value to the consensus state.
     *
     * @param request The learn request.
     */
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