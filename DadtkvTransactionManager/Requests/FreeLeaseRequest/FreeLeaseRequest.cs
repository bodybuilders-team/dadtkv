namespace Dadtkv;

/// <summary>
///     A request to free a lease.
/// </summary>
public class FreeLeaseRequest : IUrbRequest<FreeLeaseRequest>
{
    public readonly LeaseId LeaseId;

    public FreeLeaseRequest(ulong serverId, ulong broadcasterId, LeaseId leaseId, ulong sequenceNum = 0)
    {
        ServerId = serverId;
        BroadcasterId = broadcasterId;
        SequenceNum = sequenceNum;
        LeaseId = leaseId;
    }

    public ulong ServerId { get; }
    public ulong BroadcasterId { get; }
    public ulong SequenceNum { get; set; }
}