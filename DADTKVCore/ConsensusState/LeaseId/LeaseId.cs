namespace Dadtkv;

/// <summary>
///     LeaseId is a unique identifier for a lease.
/// </summary>
public class LeaseId
{
    public readonly ulong SenderId;
    public readonly ulong SequenceNum;

    public LeaseId(ulong sequenceNum, ulong senderId)
    {
        SequenceNum = sequenceNum;
        SenderId = senderId;
    }

    public override bool Equals(object? obj)
    {
        if (null == obj) return false;
        if (this == obj) return true;
        if (obj.GetType() != GetType()) return false;

        var other = (LeaseId)obj;
        return SequenceNum.Equals(other.SequenceNum) && SenderId.Equals(other.SenderId);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(SequenceNum, SenderId);
    }

    public override string ToString()
    {
        return $"(SequenceNum: {SequenceNum}, SenderId: {SenderId})";
    }
}