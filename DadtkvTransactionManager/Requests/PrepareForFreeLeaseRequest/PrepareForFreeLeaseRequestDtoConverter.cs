namespace Dadtkv;

/// <summary>
///     Converter between <see cref="PrepareForFreeLeaseRequest" /> and <see cref="PrepareForFreeLeaseRequestDto" />.
/// </summary>
public static class PrepareForFreeLeaseRequestDtoConverter
{
    /// <summary>
    ///     Convert from <see cref="PrepareForFreeLeaseRequestDto" /> to <see cref="PrepareForFreeLeaseRequest" />.
    /// </summary>
    /// <param name="dto">The <see cref="PrepareForFreeLeaseRequestDto" /> to convert from.</param>
    /// <returns>The converted <see cref="PrepareForFreeLeaseRequest" />.</returns>
    public static PrepareForFreeLeaseRequest ConvertFromDto(PrepareForFreeLeaseRequestDto dto)
    {
        return new PrepareForFreeLeaseRequest(
            dto.ServerId,
            dto.BroadcasterId,
            sequenceNum: dto.SequenceNum,
            leaseId: LeaseIdDtoConverter.ConvertFromDto(dto.LeaseId)
        );
    }

    /// <summary>
    ///     Convert from <see cref="PrepareForFreeLeaseRequest" /> to <see cref="PrepareForFreeLeaseRequestDto" />.
    /// </summary>
    /// <param name="request">The <see cref="PrepareForFreeLeaseRequest" /> to convert from.</param>
    /// <returns>The converted <see cref="PrepareForFreeLeaseRequestDto" />.</returns>
    public static PrepareForFreeLeaseRequestDto ConvertToDto(PrepareForFreeLeaseRequest request)
    {
        return new PrepareForFreeLeaseRequestDto
        {
            ServerId = request.ServerId,
            SequenceNum = request.SequenceNum,
            BroadcasterId = request.BroadcasterId,
            LeaseId = LeaseIdDtoConverter.ConvertToDto(request.LeaseId)
        };
    }
}