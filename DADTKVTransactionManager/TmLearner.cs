using DADTKVTransactionManager;
using Grpc.Core;
using Grpc.Net.Client;

namespace DADTKV;

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

    private void ResizeConsensusStateList(int roundNumber)
    {
        lock (_consensusStateLockObject)
        {
            for (var i = _consensusState.Values.Count; i <= roundNumber; i++)
                _consensusState.Values.Add(null);
        }
    }

    public override Task<LearnResponse> Learn(LearnRequest request, ServerCallContext context)
    {
            _urbReceiver.UrbProcessRequest(request);
            return Task.FromResult(new LearnResponse { Ok = true });
    }

    private void LearnUrbDeliver(LearnRequest request)
    {
        lock (_consensusStateLockObject)
        {
            ResizeConsensusStateList((int)request.RoundNumber);

            _consensusState.Values[(int)request.RoundNumber] =
                ConsensusValueDtoConverter.ConvertFromDto(request.ConsensusValue);

            var leasesToBeFreed = new HashSet<LeaseId>();
            foreach (var (key, queue) in _consensusState.Values[(int)request.RoundNumber]!.LeaseQueues)
            {
                var leaseId = queue.Peek();

                if (leaseId.ServerId == _processConfiguration.ProcessInfo.Id && queue.Count > 1 &&
                    _executedTrans[leaseId])
                    leasesToBeFreed.Add(leaseId);
            }

            foreach (var leaseId in leasesToBeFreed)
            {
                foreach (var leaseServiceClient in _leaseServiceClients)
                {
                    leaseServiceClient.FreeLeaseAsync(new FreeLeaseRequest
                    {
                        LeaseId = LeaseIdDtoConverter.ConvertToDto(leaseId)
                    });
                }
            }
        }
    }
}