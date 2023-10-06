namespace DADTKV;

/// <summary>
/// ConsensusValue is the value that is being agreed upon by the Paxos algorithm.
/// Contains the lease queues for each key in the system.
/// </summary>
public class ConsensusValue
{
    public Dictionary<string, Queue<LeaseId>> LeaseQueues = new();

    /// <summary>
    /// Deep copies the ConsensusValue.
    /// </summary>
    /// <returns>A deep copy of the ConsensusValue.</returns>
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