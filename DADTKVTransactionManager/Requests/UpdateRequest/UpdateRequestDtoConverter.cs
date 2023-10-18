namespace Dadtkv;

public static class UpdateRequestDtoConverter
{
    public static UpdateRequestDto ConvertToDto(UpdateRequest urbRequest)
    {
        return new UpdateRequestDto
        {
            ServerId = urbRequest.ServerId,
            SenderId = urbRequest.SenderId,
            SequenceNum = urbRequest.SequenceNum,
            LeaseId = LeaseIdDtoConverter.ConvertToDto(urbRequest.LeaseId),
            WriteSet = { urbRequest.WriteSet.Select(DadIntDtoConverter.ConvertToDto).ToList() },
            FreeLease = urbRequest.FreeLease
        };
    }

    public static UpdateRequest ConvertFromDto(UpdateRequestDto updateRequestDto)
    {
        return new UpdateRequest(
            updateRequestDto.ServerId,
            updateRequestDto.SenderId,
            sequenceNum: updateRequestDto.SequenceNum,
            leaseId: LeaseIdDtoConverter.ConvertFromDto(updateRequestDto.LeaseId),
            writeSet: updateRequestDto.WriteSet.Select(DadIntDtoConverter.ConvertFromDto).ToList(),
            freeLease: updateRequestDto.FreeLease
        );
    }
}