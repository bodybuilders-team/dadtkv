namespace Dadtkv;

/// <summary>
///     Converter between <see cref="FreeLeaseRequest" /> and <see cref="FreeLeaseRequestDto" />.
/// </summary>
public static class FreeLeaseRequestDtoConverter
{
    /// <summary>
    ///     Convert from <see cref="FreeLeaseRequestDto" /> to <see cref="FreeLeaseRequest" />.
    /// </summary>
    /// <param name="dto">The <see cref="FreeLeaseRequestDto" /> to convert from.</param>
    /// <returns>The converted <see cref="FreeLeaseRequest" />.</returns>
    public static FreeLeaseRequest ConvertFromDto(FreeLeaseRequestDto dto)
    {
        return new FreeLeaseRequest(
            dto.ServerId,
            dto.BroadcasterId,
            sequenceNum: dto.SequenceNum,
            leaseId: LeaseIdDtoConverter.ConvertFromDto(dto.LeaseId)
        );
    }

    /// <summary>
    ///     Convert from <see cref="FreeLeaseRequest" /> to <see cref="FreeLeaseRequestDto" />.
    /// </summary>
    /// <param name="request">The <see cref="FreeLeaseRequest" /> to convert from.</param>
    /// <returns>The converted <see cref="FreeLeaseRequestDto" />.</returns>
    public static FreeLeaseRequestDto ConvertToDto(FreeLeaseRequest request)
    {
        return new FreeLeaseRequestDto
        {
            ServerId = request.ServerId,
            SequenceNum = request.SequenceNum,
            BroadcasterId = request.BroadcasterId,
            LeaseId = LeaseIdDtoConverter.ConvertToDto(request.LeaseId)
        };
    }
}