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

    public override bool Equals(object? obj)
    {
        if (null == obj) return false;
        if (this == obj) return true;
        if (obj.GetType() != this.GetType()) return false;
        
        var other = (LeaseId)obj;
        return SequenceNum == other.SequenceNum && ServerId == other.ServerId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(SequenceNum, ServerId);
    }

    public override string ToString()
    {
        return $"(SequenceNum: {SequenceNum}, ServerId: {ServerId})";
    }
}