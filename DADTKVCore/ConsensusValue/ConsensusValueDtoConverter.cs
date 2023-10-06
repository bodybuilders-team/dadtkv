using Google.Protobuf.Collections;

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
            LeaseQueues = new Dictionary<string, Queue<LeaseId>>(
                consensusValueDto.LeaseQueues
                    .Select(pair =>
                        new KeyValuePair<string, Queue<LeaseId>>(
                            pair.Key,
                            new Queue<LeaseId>(pair.Value.LeaseIds.Select(LeaseIdDtoConverter.ConvertFromDto))
                        )
                    )
            )
        };
    }

    /// <summary>
    ///     Converts <see cref="ConsensusValue" /> to <see cref="ConsensusValueDto" />.
    /// </summary>
    /// <param name="consensusValue">The <see cref="ConsensusValue" /> to convert.</param>
    /// <returns>The converted <see cref="ConsensusValueDto" />.</returns>
    public static ConsensusValueDto ConvertToDto(ConsensusValue consensusValue)
    {
        var mapDto = new MapField<string, LeaseQueueDto>();

        var map = consensusValue.LeaseQueues
            .Select(pair => new KeyValuePair<string, LeaseQueueDto>(
                pair.Key,
                new LeaseQueueDto
                {
                    LeaseIds = { pair.Value.Select(LeaseIdDtoConverter.ConvertToDto) }
                }
            ));

        foreach (var (key, value) in map)
            mapDto.Add(key, value);

        return new ConsensusValueDto { LeaseQueues = { mapDto } };
    }
}