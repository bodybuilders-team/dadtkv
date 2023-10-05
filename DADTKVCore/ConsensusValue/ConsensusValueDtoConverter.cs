using Google.Protobuf.Collections;

namespace DADTKV;

public static class ConsensusValueDtoConverter
{
    public static ConsensusValue ConvertFromDto(ConsensusValueDto consensusValueDto)
    {
        return new ConsensusValue
        {
            LeaseQueues = new Dictionary<string, Queue<LeaseId>>(
                consensusValueDto.LeaseQueues
                    .Select(pair =>
                        new KeyValuePair<string, Queue<LeaseId>>(
                            pair.Key,
                            new Queue<LeaseId>(
                                pair.Value.LeaseIds.Select(LeaseIdDtoConverter.ConvertFromDto)
                            )
                        )
                    )
            )
        };
    }

    public static ConsensusValueDto ConvertToDto(ConsensusValue consensusValue)
    {
        var mapDto = new MapField<string, LeaseQueueDto>();

        var map = consensusValue.LeaseQueues
            .Select(pair => new KeyValuePair<string, LeaseQueueDto>(
                pair.Key,
                new LeaseQueueDto
                {
                    LeaseIds =
                    {
                        pair.Value.Select(LeaseIdDtoConverter.ConvertToDto)
                    }
                }
            ));

        foreach (var (key, value) in map) mapDto.Add(key, value);

        return new ConsensusValueDto { LeaseQueues = { mapDto } };
    }
}