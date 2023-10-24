namespace Dadtkv;

/// <summary>
///     Converter between <see cref="ForceFreeLeaseRequest" /> and <see cref="ForceFreeLeaseRequestDto" />.
/// </summary>
public static class ForceFreeLeaseRequestDtoConverter
{
    /// <summary>
    ///     Convert from <see cref="ForceFreeLeaseRequestDto" /> to <see cref="ForceFreeLeaseRequest" />.
    /// </summary>
    /// <param name="dto">The <see cref="ForceFreeLeaseRequestDto" /> to convert from.</param>
    /// <returns>The converted <see cref="ForceFreeLeaseRequest" />.</returns>
    public static ForceFreeLeaseRequest ConvertFromDto(ForceFreeLeaseRequestDto dto)
    {
        return new ForceFreeLeaseRequest(
            dto.ServerId,
            dto.BroadcasterId,
            sequenceNum: dto.SequenceNum,
            leaseId: LeaseIdDtoConverter.ConvertFromDto(dto.LeaseId)
        );
    }

    /// <summary>
    ///     Convert from <see cref="ForceFreeLeaseRequest" /> to <see cref="ForceFreeLeaseRequestDto" />.
    /// </summary>
    /// <param name="request">The <see cref="ForceFreeLeaseRequest" /> to convert from.</param>
    /// <returns>The converted <see cref="ForceFreeLeaseRequestDto" />.</returns>
    public static ForceFreeLeaseRequestDto ConvertToDto(ForceFreeLeaseRequest request)
    {
        return new ForceFreeLeaseRequestDto
        {
            ServerId = request.ServerId,
            SequenceNum = request.SequenceNum,
            BroadcasterId = request.BroadcasterId,
            LeaseId = LeaseIdDtoConverter.ConvertToDto(request.LeaseId)
        };
    }
}