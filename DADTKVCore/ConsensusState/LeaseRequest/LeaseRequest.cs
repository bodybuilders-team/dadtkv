namespace Dadtkv;

public class LeaseRequest
{
    public LeaseRequest(LeaseId leaseId, List<string> set)
    {
        LeaseId = leaseId;
        Set = set;
    }

    public LeaseId LeaseId { get; }
    public List<string> Set { get; } // TODO: Rename to keys
}