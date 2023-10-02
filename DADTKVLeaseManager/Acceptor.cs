using Grpc.Core;

namespace DADTKV;

internal class Acceptor : AcceptorService.AcceptorServiceBase
{
    private readonly ConsensusState _consensusState;
    private readonly object _lockObject;
    private readonly List<LearnerService.LearnerServiceClient> _learnerServiceClients;
    private readonly List<AcceptorService.AcceptorServiceClient> _acceptorServiceServiceClients;

    private readonly Dictionary<string, HashSet<ulong>> _sequenceNumCounterLookup;

    public Acceptor(
        object lockObject,
        ConsensusState consensusState,
        List<AcceptorService.AcceptorServiceClient> acceptorServiceClients,
        List<LearnerService.LearnerServiceClient> learnerServiceClients,
        LeaseManagerConfiguration leaseManagerConfiguration
    )
    {
        _lockObject = lockObject;
        _consensusState = consensusState;
        _learnerServiceClients = learnerServiceClients;
        _acceptorServiceServiceClients = acceptorServiceClients;

        _sequenceNumCounterLookup = new Dictionary<string, HashSet<ulong>>();

        foreach (var lm in leaseManagerConfiguration.LeaseManagers)
        {
            _sequenceNumCounterLookup[lm.Id] = new HashSet<ulong>();
        }
    }

    public override Task<PrepareResponse> Prepare(PrepareRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            if (request.EpochNumber <= _consensusState.ReadTimestamp)
                return Task.FromResult(new PrepareResponse
                {
                    Promise = false,
                    WriteTimestamp = _consensusState.WriteTimestamp,
                    Value = _consensusState.Value != null
                        ? ConsensusValueDtoConverter.ConvertToDto(_consensusState.Value)
                        : null
                });

            _consensusState.ReadTimestamp = request.EpochNumber;
            return Task.FromResult(new PrepareResponse
                {
                    Promise = true,
                    WriteTimestamp = _consensusState.WriteTimestamp,
                    Value = _consensusState.Value != null
                        ? ConsensusValueDtoConverter.ConvertToDto(_consensusState.Value)
                        : null
                }
            );
        }
    }

    public override Task<AcceptResponse> Accept(AcceptRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            var currSeqNumSet = _sequenceNumCounterLookup[request.ServerId];
            if (currSeqNumSet.Contains(request.SequenceNum))
                return Task.FromResult(new AcceptResponse { Accepted = true }); // TODO: check this

            _sequenceNumCounterLookup[request.ServerId].Add(request.SequenceNum);

            // TODO does it need to be exactly the current read timestamp?
            if (request.EpochNumber != _consensusState.ReadTimestamp)
                return Task.FromResult(new AcceptResponse { Accepted = false });

            // TODO: Rebroadcast to other acceptors
            var asyncTasks = new List<AsyncUnaryCall<AcceptResponse>>();
            foreach (var acceptServiceClient in _acceptorServiceServiceClients)
            {
                var res = acceptServiceClient.AcceptAsync(request);
                asyncTasks.Add(res);
            }

            DADTKVUtils.WaitForMajority(
                asyncTasks,
                (res, cde) =>
                {
                    if (!res.Accepted) //TODO: Is this necessary?
                        throw new Exception();

                    cde.Signal();
                    return Task.CompletedTask;
                }
            );

            Decide(request);

            return Task.FromResult(new AcceptResponse { Accepted = true });
        }
    }

    private void Decide(AcceptRequest request)
    {
        // TODO does it need to be exactly the current read timestamp?
        _consensusState.WriteTimestamp = request.EpochNumber;
        _consensusState.Value = ConsensusValueDtoConverter.ConvertFromDto(request.Value);

        _learnerServiceClients.ForEach(client => client.Learn(new LearnRequest
        {
            ConsensusValue = request.Value,
            EpochNumber = request.EpochNumber
        }));
    }
}