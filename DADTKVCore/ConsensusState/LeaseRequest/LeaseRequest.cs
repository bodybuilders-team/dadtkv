using System.Text;

namespace Dadtkv;

/// <summary>
///     A request to acquire a lease for a set of keys.
/// </summary>
public class LeaseRequest
{
    public LeaseId LeaseId { get; }
    public List<string> Keys { get; }

    public LeaseRequest(LeaseId leaseId, List<string> keys)
    {
        LeaseId = leaseId;
        Keys = keys;
    }
    
    protected bool Equals(LeaseRequest other)
    {
        return LeaseId.Equals(other.LeaseId);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((LeaseRequest)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LeaseId, Keys);
    }

    public override string ToString()
    {
        return $"(LeaseId: {LeaseId}, Keys: {string.Join(",", Keys.ToArray())})";
    }
}