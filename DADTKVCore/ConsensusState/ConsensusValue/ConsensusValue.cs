using System.Text;

namespace Dadtkv;

/// <summary>
///     ConsensusValue is the value that is being agreed upon by the Paxos algorithm.
///     Contains the list of LeaseRequests that have been agreed upon.
/// </summary>
public class ConsensusValue
{
    public List<LeaseRequest> LeaseRequests = new();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("ConsensusValue: [");

        foreach (var leaseRequest in LeaseRequests)
        {
            sb.Append(leaseRequest);
            sb.Append(", ");
        }

        if (LeaseRequests.Count > 0)
            sb.Length -= 2; // Remove the trailing comma and space

        sb.Append(']');
        return sb.ToString();
    }
}