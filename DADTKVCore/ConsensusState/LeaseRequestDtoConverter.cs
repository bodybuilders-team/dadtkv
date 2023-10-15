using Google.Protobuf.Collections;

namespace DADTKV;

public static class LeaseRequestDtoConverter
{
    public static LeaseRequest ConvertFromDto(LeaseRequestDto leaseRequestDto)
    {
        return new LeaseRequest
        {
            LeaseId = LeaseIdDtoConverter.ConvertFromDto(leaseRequestDto.LeaseId),
            Set = leaseRequestDto.Set.Select(s => s).ToList()
        };
    }

    public static LeaseRequestDto ConvertToDto(LeaseRequest leaseRequest)
    {
        return new LeaseRequestDto
        {
            LeaseId = LeaseIdDtoConverter.ConvertToDto(leaseRequest.LeaseId),
            Set = { leaseRequest.Set }
        };
    }
}