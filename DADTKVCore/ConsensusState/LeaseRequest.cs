namespace DADTKV;

public class LeaseRequest
{
    public LeaseId leaseId { get; set; }
    public List<string> set { get; set; } // TODO: Rename to keys
}
