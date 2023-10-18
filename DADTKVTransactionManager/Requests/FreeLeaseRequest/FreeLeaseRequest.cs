namespace Dadtkv;

public class FreeLeaseRequest : IUrbRequest<FreeLeaseRequest>
{
    public readonly LeaseId LeaseId;

    public FreeLeaseRequest(ulong senderId, LeaseId leaseId, ulong sequenceNum = 0)
    {
        SenderId = senderId;
        SequenceNum = sequenceNum;
        LeaseId = leaseId;
    }

    public ulong SenderId { get; }
    public ulong SequenceNum { get; set; }
}