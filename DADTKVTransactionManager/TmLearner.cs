using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

public class TmLearner : LearnerService.LearnerServiceBase
{
    private readonly object _lockObject;
    private readonly ConsensusState _consensusState;
    private readonly Dictionary<LeaseId, bool> _executedTrans;
    private readonly List<LeaseService.LeaseServiceClient> _leaseServiceClients;
    private readonly UrbReceiver<LearnRequest, LearnResponse, LearnerService.LearnerServiceClient> _urbReceiver;
    private readonly ProcessConfiguration _processConfiguration;

    public TmLearner(object lockObject, ProcessConfiguration processConfiguration, ConsensusState consensusState,
        Dictionary<LeaseId, bool> executedTrans)
    {
        _lockObject = lockObject;
        _processConfiguration = processConfiguration;
        _consensusState = consensusState;
        _executedTrans = executedTrans;

        var learnerServiceClients = new List<LearnerService.LearnerServiceClient>();
        foreach (var process in processConfiguration.OtherServerProcesses)
        {
            var channel = GrpcChannel.ForAddress(process.URL);
            learnerServiceClients.Add(new LearnerService.LearnerServiceClient(channel));
        }

        this._leaseServiceClients = new List<LeaseService.LeaseServiceClient>();
        foreach (var process in processConfiguration.OtherServerProcesses)
        {
            var channel = GrpcChannel.ForAddress(process.URL);
            learnerServiceClients.Add(new LearnerService.LearnerServiceClient(channel));
        }

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
            return Task.FromResult(new LearnResponse { Ok = true });
        }
    }

    private void LearnUrbDeliver(LearnRequest request)
    {
        if (request.ProposalNumber <= _consensusState.WriteTimestamp)
            return;

        _consensusState.WriteTimestamp = request.ProposalNumber;
        _consensusState.Values = ConsensusValueDtoConverter.ConvertFromDto(request.ConsensusValue);

        var leasesToBeFreed = new HashSet<LeaseId>();
        foreach (var (key, queue) in _consensusState.Values.LeaseQueues)
        {
            var leaseId = queue.Peek();

            if (leaseId.ServerId == _processConfiguration.ProcessInfo.Id && queue.Count > 1 && _executedTrans[leaseId])
                leasesToBeFreed.Add(leaseId);
        }


        foreach (var leaseId in leasesToBeFreed)
        {
            foreach (var leaseServiceClient in _leaseServiceClients)
            {
                leaseServiceClient.FreeLease(new FreeLeaseRequest
                {
                    LeaseId = LeaseIdDtoConverter.ConvertToDto(leaseId)
                });
            }
        }
    }
}