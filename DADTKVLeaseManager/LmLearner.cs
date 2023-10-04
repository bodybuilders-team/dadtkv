using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

public class LmLearner : LearnerService.LearnerServiceBase
{
    private readonly object _lockObject;
    private readonly ConsensusState _consensusState;
    private readonly List<ILeaseRequest> _leaseRequests;
    private readonly UrbReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _urbReceiver;

    public LmLearner(object lockObject, ProcessConfiguration processConfiguration, ConsensusState consensusState,
        List<ILeaseRequest> leaseRequests)
    {
        _lockObject = lockObject;
        _consensusState = consensusState;
        _leaseRequests = leaseRequests;

        var learnerServiceClients =
            processConfiguration.OtherServerProcesses
                .Select(process => GrpcChannel.ForAddress(process.URL))
                .Select(channel => new LearnerService.LearnerServiceClient(channel))
                .ToList();

        this._urbReceiver = new UrbReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient>(
            learnerServiceClients,
            LearnUrbDeliver,
            (req) => req.ServerId + req.SequenceNum,
            (client, req) => client.LearnAsync(req).ResponseAsync
        );
    }

    public override Task<LearnResponse> Learn(LearnRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            _urbReceiver.UrbProcessRequest(request);

            return Task.FromResult(new LearnResponse
            {
                Ok = true
            });
        }
    }

    private void LearnUrbDeliver(LearnRequest request)
    {
        if (request.EpochNumber <= _consensusState.WriteTimestamp)
            return;

        _consensusState.WriteTimestamp = request.EpochNumber;
        _consensusState.Value = ConsensusValueDtoConverter.ConvertFromDto(request.ConsensusValue);

        _leaseRequests.Clear();
    }
}