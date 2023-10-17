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
        return $"(LeaseRequests: {LeaseRequests.ToStringRep()})";
    }
}