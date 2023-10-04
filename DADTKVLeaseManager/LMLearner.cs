using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

public class LMLearner : LearnerService.LearnerServiceBase
{
    private readonly object _lockObject;
    private readonly ConsensusState _consensusState;
    private readonly List<ILeaseRequest> _leaseRequests;
    private readonly UrbReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _urbReceiver;

    public LMLearner(object lockObject, ProcessConfiguration processConfiguration, ConsensusState consensusState,
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
            (req) => new LearnResponse { Ok = true },
            (client, req) => client.LearnAsync(req).ResponseAsync
        );
    }

    public override Task<LearnResponse> Learn(LearnRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            return Task.FromResult(_urbReceiver.UrbProcessRequest(request));
        }
    }

    private LearnResponse LearnUrbDeliver(LearnRequest request)
    {
        if (request.EpochNumber <= _consensusState.WriteTimestamp)
            return new LearnResponse
            {
                Ok = true
            };

        _consensusState.WriteTimestamp = request.EpochNumber;
        _consensusState.Value = ConsensusValueDtoConverter.ConvertFromDto(request.ConsensusValue);

        _leaseRequests.Clear();

        return new LearnResponse
        {
            Ok = true
        };
    }
}