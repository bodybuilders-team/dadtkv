namespace Dadtkv;

public static class FreeLeaseRequestDtoConverter
{
    public static FreeLeaseRequestDto ConvertToDto(FreeLeaseRequest request)
    {
        return new FreeLeaseRequestDto
        {
            SequenceNum = request.SequenceNum,
            ServerId = request.ServerId,
            LeaseId = LeaseIdDtoConverter.ConvertToDto(request.LeaseId)
        };
    }

    public static FreeLeaseRequest ConvertFromDto(FreeLeaseRequestDto dto)
    {
        return new FreeLeaseRequest(
            serverId: dto.ServerId,
            sequenceNum: dto.SequenceNum,
            leaseId: LeaseIdDtoConverter.ConvertFromDto(dto.LeaseId)
        );
    }
}