namespace Dadtkv;

public class FreeLeaseRequest : IUrbRequest<FreeLeaseRequest>
{
    public readonly LeaseId LeaseId;

    public FreeLeaseRequest(ulong broadcasterId, LeaseId leaseId, ulong sequenceNum = 0)
    {
        BroadcasterId = broadcasterId;
        SequenceNum = sequenceNum;
        LeaseId = leaseId;
    }

    public ulong BroadcasterId { get; }
    public ulong SequenceNum { get; set; }
}