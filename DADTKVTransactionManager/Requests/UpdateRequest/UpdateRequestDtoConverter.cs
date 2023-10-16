namespace DADTKV;

public static class UpdateRequestDtoConverter
{
    public static UpdateRequestDto ConvertToDto(UpdateRequest urbRequest)
    {
        return new UpdateRequestDto
        {
            SequenceNum = urbRequest.SequenceNum,
            ServerId = urbRequest.ServerId,
            LeaseId = LeaseIdDtoConverter.ConvertToDto(urbRequest.LeaseId),
            WriteSet = { urbRequest.WriteSet },
            FreeLease = urbRequest.FreeLease
        };
    }

    public static UpdateRequest ConvertFromDto(UpdateRequestDto urbRequestDto,
        ProcessConfiguration processConfiguration)
    {
        return new UpdateRequest(
            processConfiguration,
            urbRequestDto.ServerId,
            sequenceNum: urbRequestDto.SequenceNum,
            leaseId: LeaseIdDtoConverter.ConvertFromDto(urbRequestDto.LeaseId),
            writeSet: urbRequestDto.WriteSet.ToList(),
            freeLease: urbRequestDto.FreeLease
        );
    }
}