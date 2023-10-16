namespace DADTKV;

public class LeaseRequest
{
    public LeaseId LeaseId { get; }
    public List<string> Set { get; } // TODO: Rename to keys

    public LeaseRequest(LeaseId leaseId, List<string> set)
    {
        LeaseId = leaseId;
        Set = set;
    }
}