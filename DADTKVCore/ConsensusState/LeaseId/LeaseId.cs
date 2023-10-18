namespace Dadtkv;

/// <summary>
///     LeaseId is a unique identifier for a lease.
/// </summary>
public class LeaseId
{
    public readonly ulong BroadcasterId;
    public readonly ulong SequenceNum;

    public LeaseId(ulong sequenceNum, ulong broadcasterId)
    {
        SequenceNum = sequenceNum;
        BroadcasterId = broadcasterId;
    }

    public override bool Equals(object? obj)
    {
        if (null == obj) return false;
        if (this == obj) return true;
        if (obj.GetType() != GetType()) return false;

        var other = (LeaseId)obj;
        return SequenceNum.Equals(other.SequenceNum) && BroadcasterId.Equals(other.BroadcasterId);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(SequenceNum, BroadcasterId);
    }

    public override string ToString()
    {
        return $"(SequenceNum: {SequenceNum}, BroadcasterId: {BroadcasterId})";
    }
}