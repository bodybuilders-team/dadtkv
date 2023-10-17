namespace Dadtkv;

/// <summary>
///     Converts a LeaseRequest to and from a LeaseRequestDto and vice versa.
/// </summary>
public static class LeaseRequestDtoConverter
{
    public static LeaseRequest ConvertFromDto(LeaseRequestDto leaseRequestDto)
    {
        return new LeaseRequest(
            LeaseIdDtoConverter.ConvertFromDto(leaseRequestDto.LeaseId),
            leaseRequestDto.Keys.Select(s => s).ToList()
        );
    }

    public static LeaseRequestDto ConvertToDto(LeaseRequest leaseRequest)
    {
        return new LeaseRequestDto
        {
            LeaseId = LeaseIdDtoConverter.ConvertToDto(leaseRequest.LeaseId),
            Keys = { leaseRequest.Keys }
        };
    }
}