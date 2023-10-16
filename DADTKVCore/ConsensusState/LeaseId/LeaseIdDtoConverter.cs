namespace DADTKV;

/// <summary>
///     Converter class for converting between <see cref="LeaseId" /> and <see cref="LeaseIdDto" />.
/// </summary>
public static class LeaseIdDtoConverter
{
    /// <summary>
    ///     Converts a <see cref="LeaseIdDto" /> to a <see cref="LeaseId" />.
    /// </summary>
    /// <param name="leaseIdDto">The <see cref="LeaseIdDto" /> to convert.</param>
    /// <returns>The converted <see cref="LeaseId" />.</returns>
    public static LeaseId ConvertFromDto(LeaseIdDto leaseIdDto)
    {
        return new LeaseId(
            serverId: leaseIdDto.ServerId,
            sequenceNum: leaseIdDto.SequenceNum
        );
    }

    /// <summary>
    ///     Converts a <see cref="LeaseId" /> to a <see cref="LeaseIdDto" />.
    /// </summary>
    /// <param name="leaseId">The <see cref="LeaseId" /> to convert.</param>
    /// <returns>The converted <see cref="LeaseIdDto" />.</returns>
    public static LeaseIdDto ConvertToDto(LeaseId leaseId)
    {
        return new LeaseIdDto
        {
            ServerId = leaseId.ServerId,
            SequenceNum = leaseId.SequenceNum
        };
    }
}