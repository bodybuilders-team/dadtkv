using System.Text;

namespace DADTKV;

// TODO: Maybe create package Consensus (com subpackages LeaseId, ConsensusValue e file ConsensusState)

/// <summary>
///     The state of the consensus algorithm.
///     Contains the list of values that have been agreed upon, for each round of the algorithm.
/// </summary>
public class ConsensusState
{
    public readonly List<ConsensusValue?> Values = new();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("ConsensusState: [");

        foreach (var value in Values)
        {
            sb.Append(value != null ? value.ToString() : "null");
            sb.Append(", ");
        }

        if (Values.Count > 0)
            sb.Length -= 2; // Remove the trailing comma and space

        sb.Append(']');
        return sb.ToString();
    }
}