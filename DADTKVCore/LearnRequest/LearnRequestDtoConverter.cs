namespace Dadtkv;

/// <summary>
///     Converts a LearnRequest to and from a LearnRequestDto and vice versa.
/// </summary>
public static class LearnRequestDtoConverter
{
    public static LearnRequest ConvertFromDto(LearnRequestDto dto, ProcessConfiguration processConfiguration)
    {
        return new LearnRequest(
            processConfiguration,
            dto.ServerId,
            dto.RoundNumber,
            ConsensusValueDtoConverter.ConvertFromDto(dto.ConsensusValue),
            dto.SequenceNum
        );
    }

    public static LearnRequestDto ConvertToDto(LearnRequest request)
    {
        return new LearnRequestDto
        {
            ServerId = request.ServerId,
            SequenceNum = request.SequenceNum,
            ConsensusValue = ConsensusValueDtoConverter.ConvertToDto(request.ConsensusValue),
            RoundNumber = request.RoundNumber
        };
    }
}