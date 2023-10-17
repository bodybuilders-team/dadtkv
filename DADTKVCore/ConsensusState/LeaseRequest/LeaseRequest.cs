namespace Dadtkv;

/// <summary>
///     A request to acquire a lease for a set of keys.
/// </summary>
public class LeaseRequest
{
    public LeaseRequest(LeaseId leaseId, List<string> keys)
    {
        LeaseId = leaseId;
        Keys = keys;
    }

    public LeaseId LeaseId { get; }
    public List<string> Keys { get; }
}