namespace DADTKV;

public class LeaseRequest
{
    public LeaseId LeaseId { get; set; }
    public List<string> Set { get; set; } // TODO: Rename to keys
}
