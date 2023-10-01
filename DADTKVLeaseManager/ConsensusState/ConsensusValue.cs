namespace DADTKV;

public class ConsensusValue
{
    public Dictionary<string, Queue<string>> LeaseQueue = new();

    public ConsensusValue DeepCopy()
    {
        return new ConsensusValue
        {
            LeaseQueue = LeaseQueue.ToDictionary(
                pair => pair.Key,
                pair => new Queue<string>(pair.Value)
            )
        };
    }
}