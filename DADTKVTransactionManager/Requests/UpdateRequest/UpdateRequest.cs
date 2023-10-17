namespace Dadtkv;

public class UpdateRequest : IUrbRequest<UpdateRequest>
{
    public UpdateRequest(ulong serverId, LeaseId leaseId,
        List<DadInt> writeSet, bool freeLease, ulong sequenceNum = 0)
    {
        SequenceNum = sequenceNum;
        ServerId = serverId;
        LeaseId = leaseId;
        WriteSet = writeSet;
        FreeLease = freeLease;
    }

    public LeaseId LeaseId { get; }
    public List<DadInt> WriteSet { get; }
    public bool FreeLease { get; }

    public ulong SequenceNum { get; set; }
    public ulong ServerId { get; }
}