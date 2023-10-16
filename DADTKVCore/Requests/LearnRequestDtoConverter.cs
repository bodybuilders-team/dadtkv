namespace DADTKV;

public class LearnRequestDtoConverter
{
    public static LearnRequest convertFromDto(LearnRequestDto dto, ProcessConfiguration processConfiguration)
    {
        return new LearnRequest(
            processConfiguration: processConfiguration,
            serverId: dto.ServerId,
            roundNumber: dto.RoundNumber,
            consensusValue: ConsensusValueDtoConverter.ConvertFromDto(dto.ConsensusValue),
            sequenceNum: dto.SequenceNum);
    }

    public static LearnRequestDto convertToDto(LearnRequest request)
    {
        return new LearnRequestDto
        {
            ServerId = request.ServerId,
            SequenceNum = request.SequenceNum,
            ConsensusValue = ConsensusValueDtoConverter.ConvertToDto(request.ConsensusValue),
            RoundNumber = request.RoundNumber,
        };
    }
}