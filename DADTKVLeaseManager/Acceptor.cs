using Grpc.Core;

namespace DADTKV;

internal class Acceptor : AcceptorService.AcceptorServiceBase
{
    private readonly ConsensusState _consensusState;
    private readonly object _lockObject;
    private readonly List<LearnerService.LearnerServiceClient> _learnerServiceClients = new();

    public Acceptor(object lockObject, ConsensusState consensusState,
        Dictionary<string, ServerProcessChannel> transactionManagersChannels)
    {
        _lockObject = lockObject;
        _consensusState = consensusState;
        //TODO: Duplicated code in proposer
        foreach (var (_, transactionManagerChannel) in transactionManagersChannels)
        {
            _learnerServiceClients.Add(new LearnerService.LearnerServiceClient(transactionManagerChannel.GrpcChannel));
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
            if (request.EpochNumber <= _consensusState.ReadTimestamp)
                return Task.FromResult(new AcceptResponse { Ok = false });

            _consensusState.WriteTimestamp =
                request.EpochNumber; // TODO does it need to be exactly the current read timestamp?
            _consensusState.Value = ConsensusValueDtoConverter.ConvertFromDto(request.Value);

            _learnerServiceClients.ForEach(client => client.Learn(new LearnRequest
            {
                ConsensusValue = request.Value,
                EpochNumber = request.EpochNumber
            }));

            return Task.FromResult(new AcceptResponse
            {
                Ok = true
            });
        }
    }
}