namespace Dadtkv;

/// <summary>
///     Converts a LeaseRequest to and from a LeaseRequestDto and vice versa.
/// </summary>
public static class LeaseRequestDtoConverter
{
    /// <summary>
    ///     Converts a <see cref="LeaseRequestDto" /> to a <see cref="LeaseRequest" />.
    /// </summary>
    /// <param name="leaseRequestDto">The <see cref="LeaseRequestDto" /> to convert.</param>
    /// <returns>The converted <see cref="LeaseRequest" />.</returns>
    public static LeaseRequest ConvertFromDto(LeaseRequestDto leaseRequestDto)
    {
        return new LeaseRequest(
            LeaseIdDtoConverter.ConvertFromDto(leaseRequestDto.LeaseId),
            leaseRequestDto.Keys.Select(s => s).ToList()
        );
    }

    /// <summary>
    ///     Converts a <see cref="LeaseRequest" /> to a <see cref="LeaseRequestDto" />.
    /// </summary>
    /// <param name="leaseRequest">The <see cref="LeaseRequest" /> to convert.</param>
    /// <returns>The converted <see cref="LeaseRequestDto" />.</returns>
    public static LeaseRequestDto ConvertToDto(LeaseRequest leaseRequest)
    {
        return new LeaseRequestDto
        {
            LeaseId = LeaseIdDtoConverter.ConvertToDto(leaseRequest.LeaseId),
            Keys = { leaseRequest.Keys }
        };
    }
}