namespace Dadtkv;

/// <summary>
///     A request to free a lease.
/// </summary>
public class FreeLeaseRequest : IUrbRequest<FreeLeaseRequest>
{
    public readonly LeaseId LeaseId;
    public ulong BroadcasterId { get; }
    public ulong SequenceNum { get; set; }

    public FreeLeaseRequest(ulong broadcasterId, LeaseId leaseId, ulong sequenceNum = 0)
    {
        BroadcasterId = broadcasterId;
        SequenceNum = sequenceNum;
        LeaseId = leaseId;
    }
}