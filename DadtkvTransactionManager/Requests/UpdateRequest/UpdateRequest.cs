namespace Dadtkv;

/// <summary>
///     A request to update a lease.
/// </summary>
public class UpdateRequest : IUrbRequest<UpdateRequest>
{
    public UpdateRequest(ulong serverId, ulong broadcasterId, LeaseId leaseId,
        List<DadInt> writeSet, bool freeLease, ulong sequenceNum = 0)
    {
        ServerId = serverId;
        SequenceNum = sequenceNum;
        BroadcasterId = broadcasterId;
        LeaseId = leaseId;
        WriteSet = writeSet;
        FreeLease = freeLease;
    }

    public ulong ServerId { get; }
    public LeaseId LeaseId { get; }
    public List<DadInt> WriteSet { get; }
    public bool FreeLease { get; }
    public ulong BroadcasterId { get; }
    public ulong SequenceNum { get; set; }

    public override string ToString()
    {
        return
            $"UpdateRequest(LeaseId: {LeaseId}, WriteSet: {WriteSet.ToStringRep()}, FreeLease: {FreeLease}, SequenceNum: {SequenceNum}, BroadcasterId: {BroadcasterId})";
    }
}