using System.Text;

namespace DADTKV;

/// <summary>
///     ConsensusValue is the value that is being agreed upon by the Paxos algorithm.
///     Contains the lease queues for each key in the system.
/// </summary>
public class ConsensusValue
{
    public Dictionary<string, Queue<LeaseId>> LeaseQueues = new();

    /// <summary>
    ///     Deep copies the ConsensusValue.
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

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("ConsensusValue: {");
        foreach (var kvp in LeaseQueues)
        {
            sb.Append($"'{kvp.Key}': [");
            foreach (var leaseId in kvp.Value)
                sb.Append($"(SequenceNum: {leaseId.SequenceNum}, ServerId: '{leaseId.ServerId}'), ");

            if (kvp.Value.Count > 0)
                sb.Length -= 2; // Remove the trailing comma and space

            sb.Append("], ");
        }

        if (LeaseQueues.Count > 0)
            sb.Length -= 2; // Remove the trailing comma and space

        sb.Append('}');
        return sb.ToString();
    }
}