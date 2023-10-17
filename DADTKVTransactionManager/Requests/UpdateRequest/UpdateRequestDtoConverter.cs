namespace Dadtkv;

public static class UpdateRequestDtoConverter
{
    public static UpdateRequestDto ConvertToDto(UpdateRequest urbRequest)
    {
        return new UpdateRequestDto
        {
            SequenceNum = urbRequest.SequenceNum,
            ServerId = urbRequest.ServerId,
            LeaseId = LeaseIdDtoConverter.ConvertToDto(urbRequest.LeaseId),
            WriteSet = { urbRequest.WriteSet.Select(DadIntDtoConverter.ConvertToDto).ToList() },
            FreeLease = urbRequest.FreeLease
        };
    }

    public static UpdateRequest ConvertFromDto(UpdateRequestDto urbRequestDto)
    {
        return new UpdateRequest(
            serverId: urbRequestDto.ServerId,
            sequenceNum: urbRequestDto.SequenceNum,
            leaseId: LeaseIdDtoConverter.ConvertFromDto(urbRequestDto.LeaseId),
            writeSet: urbRequestDto.WriteSet.Select(DadIntDtoConverter.ConvertFromDto).ToList(),
            freeLease: urbRequestDto.FreeLease
        );
    }
}