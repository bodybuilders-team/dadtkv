namespace Dadtkv;

/// <summary>
///     LeaseId is a unique identifier for a lease.
/// </summary>
public class LeaseId
{
    public readonly ulong SequenceNum;
    public readonly ulong ServerId;

    public LeaseId(ulong sequenceNum, ulong serverId)
    {
        SequenceNum = sequenceNum;
        ServerId = serverId;
    }

    private bool Equals(LeaseId other)
    {
        return ServerId == other.ServerId && SequenceNum == other.SequenceNum;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((LeaseId)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ServerId, SequenceNum);
    }

    public override string ToString()
    {
        return $"(SequenceNum: {SequenceNum}, ServerId: '{ServerId}')";
    }
}