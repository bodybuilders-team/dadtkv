namespace Dadtkv;

/// <summary>
///     Converter between <see cref="UpdateRequest" /> and <see cref="UpdateRequestDto" />.
/// </summary>
public static class UpdateRequestDtoConverter
{
    /// <summary>
    ///     Convert from <see cref="UpdateRequest" /> to <see cref="UpdateRequestDto" />.
    /// </summary>
    /// <param name="urbRequest">The <see cref="UpdateRequest" /> to convert from.</param>
    /// <returns>The converted <see cref="UpdateRequestDto" />.</returns>
    public static UpdateRequestDto ConvertToDto(UpdateRequest urbRequest)
    {
        return new UpdateRequestDto
        {
            ServerId = urbRequest.ServerId,
            BroadcasterId = urbRequest.BroadcasterId,
            SequenceNum = urbRequest.SequenceNum,
            LeaseId = LeaseIdDtoConverter.ConvertToDto(urbRequest.LeaseId),
            WriteSet = { urbRequest.WriteSet.Select(DadIntDtoConverter.ConvertToDto).ToList() },
            FreeLease = urbRequest.FreeLease
        };
    }

    /// <summary>
    ///     Convert from <see cref="UpdateRequestDto" /> to <see cref="UpdateRequest" />.
    /// </summary>
    /// <param name="dto">The <see cref="UpdateRequestDto" /> to convert from.</param>
    /// <returns>The converted <see cref="UpdateRequest" />.</returns>
    public static UpdateRequest ConvertFromDto(UpdateRequestDto dto)
    {
        return new UpdateRequest(
            dto.ServerId,
            dto.BroadcasterId,
            sequenceNum: dto.SequenceNum,
            leaseId: LeaseIdDtoConverter.ConvertFromDto(dto.LeaseId),
            writeSet: dto.WriteSet.Select(DadIntDtoConverter.ConvertFromDto).ToList(),
            freeLease: dto.FreeLease
        );
    }
}