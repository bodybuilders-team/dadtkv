using DADTKV;

namespace DADTKVTransactionManager;

public class FreeLeaseRequestDtoConverter
{
    public static FreeLeaseRequestDto convertToDto(FreeLeaseRequest request)
    {
        return new FreeLeaseRequestDto
        {
            SequenceNum = request.SequenceNum,
            ServerId = request.ServerId,
            LeaseId = LeaseIdDtoConverter.ConvertToDto(request.LeaseId)
        };
    }

    public static FreeLeaseRequest convertFromDto(FreeLeaseRequestDto dto, ProcessConfiguration processConfiguration)
    {
        return new FreeLeaseRequest(
            processConfiguration: processConfiguration,
            sequenceNum: dto.SequenceNum,
            serverId: dto.ServerId,
            leaseId: LeaseIdDtoConverter.ConvertFromDto(dto.LeaseId)
        );
    }
}