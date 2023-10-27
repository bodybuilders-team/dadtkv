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

    private bool Equals(LeaseRequest other)
    {
        return LeaseId.Equals(other.LeaseId);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((LeaseRequest)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LeaseId, Keys);
    }

    public override string ToString()
    {
        return $"LeaseRequest(LeaseId: {LeaseId}, Keys: {string.Join(",", Keys.ToArray())})";
    }
}