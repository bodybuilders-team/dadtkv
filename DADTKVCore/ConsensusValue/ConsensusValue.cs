namespace DADTKV;

public class ConsensusValue
{
    public Dictionary<string, Queue<LeaseId>> LeaseQueues = new();

    public ConsensusValue DeepCopy()
    {
        return new ConsensusValue
        {
            LeaseQueues = LeaseQueues.ToDictionary(
                pair => pair.Key,
                pair => new Queue<LeaseId>(pair.Value)
            )
        };
    }
}