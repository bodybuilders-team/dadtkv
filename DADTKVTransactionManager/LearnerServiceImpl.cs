using Grpc.Core;

namespace DADTKV;

public class LearnerServiceImpl : LearnerService.LearnerServiceBase
{
    private readonly object _lockObject;
    private ConsensusValue _consensusValue;
    private ulong _currentEpochNumber = 0;

    public LearnerServiceImpl(object lockObject)
    {
        _lockObject = lockObject;
    }

    public override Task<LearnResponse> Learn(LearnRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            //TODO: Check this
            if (request.EpochNumber <= _currentEpochNumber)
                return Task.FromResult(new LearnResponse
                {
                    Ok = true
                });

            _currentEpochNumber = request.EpochNumber;

            _consensusValue = ConsensusValueDtoConverter.ConvertFromDto(request.ConsensusValue);
            return Task.FromResult(new LearnResponse
            {
                Ok = true
            });
        }
    }
}