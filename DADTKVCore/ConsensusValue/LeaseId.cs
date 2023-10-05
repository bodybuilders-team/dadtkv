namespace DADTKV;

public class LeaseId
{
    public ulong SequenceNum = 0;
    public string ServerId = "";

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
}