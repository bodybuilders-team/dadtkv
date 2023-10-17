namespace Dadtkv;

public class FreeLeaseRequest : IUrbRequest<FreeLeaseRequest>
{
    public readonly LeaseId LeaseId;

    public FreeLeaseRequest(ulong serverId, LeaseId leaseId, ulong sequenceNum = 0)
    {
        ServerId = serverId;
        SequenceNum = sequenceNum;
        LeaseId = leaseId;
    }

    public ulong ServerId { get; }
    public ulong SequenceNum { get; set; }
}