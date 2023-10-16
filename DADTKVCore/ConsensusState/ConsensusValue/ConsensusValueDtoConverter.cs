namespace DADTKV;

/// <summary>
///     Converts <see cref="ConsensusValue" /> to <see cref="ConsensusValueDto" /> and vice versa.
/// </summary>
public static class ConsensusValueDtoConverter
{
    /// <summary>
    ///     Converts <see cref="ConsensusValueDto" /> to <see cref="ConsensusValue" />.
    /// </summary>
    /// <param name="consensusValueDto">The <see cref="ConsensusValueDto" /> to convert.</param>
    /// <returns>The converted <see cref="ConsensusValue" />.</returns>
    public static ConsensusValue ConvertFromDto(ConsensusValueDto consensusValueDto)
    {
        return new ConsensusValue
        {
            LeaseRequests = consensusValueDto.LeaseRequests.Select(LeaseRequestDtoConverter.ConvertFromDto).ToList()
        };
    }

    /// <summary>
    ///     Converts <see cref="ConsensusValue" /> to <see cref="ConsensusValueDto" />.
    /// </summary>
    /// <param name="consensusValue">The <see cref="ConsensusValue" /> to convert.</param>
    /// <returns>The converted <see cref="ConsensusValueDto" />.</returns>
    public static ConsensusValueDto ConvertToDto(ConsensusValue consensusValue)
    {
        return new ConsensusValueDto
        {
            LeaseRequests = { consensusValue.LeaseRequests.Select(LeaseRequestDtoConverter.ConvertToDto) }
        };
    }
}