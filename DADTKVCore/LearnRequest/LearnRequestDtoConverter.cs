namespace Dadtkv;

/// <summary>
///     Converts a LearnRequest to and from a LearnRequestDto and vice versa.
/// </summary>
public static class LearnRequestDtoConverter
{
    public static LearnRequest ConvertFromDto(LearnRequestDto dto)
    {
        return new LearnRequest(
            dto.BroadcasterId,
            dto.RoundNumber,
            ConsensusValueDtoConverter.ConvertFromDto(dto.ConsensusValue),
            dto.SequenceNum
        );
    }

    public static LearnRequestDto ConvertToDto(LearnRequest request)
    {
        return new LearnRequestDto
        {
            BroadcasterId = request.BroadcasterId,
            SequenceNum = request.SequenceNum,
            ConsensusValue = ConsensusValueDtoConverter.ConvertToDto(request.ConsensusValue),
            RoundNumber = request.RoundNumber
        };
    }
}