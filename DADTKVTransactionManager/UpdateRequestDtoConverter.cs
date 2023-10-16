using DADTKV;

namespace DADTKVTransactionManager;

public class UpdateRequestDtoConverter
{
    public static UpdateRequestDto convertToDto(UpdateRequest urbRequest)
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

    public static UpdateRequest convertFromDto(UpdateRequestDto urbRequestDto,
        ProcessConfiguration processConfiguration)
    {
        return new UpdateRequest(
            processConfiguration: processConfiguration,
            serverId: urbRequestDto.ServerId,
            sequenceNum: urbRequestDto.SequenceNum,
            leaseId: LeaseIdDtoConverter.ConvertFromDto(urbRequestDto.LeaseId),
            writeSet: urbRequestDto.WriteSet.ToList(),
            freeLease: urbRequestDto.FreeLease
        );
    }
}