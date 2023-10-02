using Google.Protobuf.Collections;

namespace DADTKV;

public static class ConsensusValueDtoConverter
{
    public static ConsensusValue ConvertFromDto(ConsensusValueDto consensusValueDto)
    {
        return new ConsensusValue
        {
            LeaseQueue = new Dictionary<string, Queue<string>>(
                consensusValueDto.LeaseQueue
                    .Select(pair =>
                        new KeyValuePair<string, Queue<string>>(
                            pair.Key,
                            new Queue<string>(pair.Value.TransactionManagers)
                        )
                    )
            )
        };
    }

    public static ConsensusValueDto ConvertToDto(ConsensusValue consensusValue)
    {
        var mapDto = new MapField<string, LeaseQueueDto>();

        var map = consensusValue.LeaseQueue
            .Select(pair => new KeyValuePair<string, LeaseQueueDto>(
                pair.Key,
                new LeaseQueueDto { TransactionManagers = { pair.Value } }
            ));

        foreach (var (key, value) in map)
        {
            mapDto.Add(key, value);
        }

        return new ConsensusValueDto { LeaseQueue = { mapDto } };
    }
}