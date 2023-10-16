namespace DADTKV;

public class UpdateRequest : IFifoUrbRequest<UpdateRequest>
{
    private readonly ProcessConfiguration _processConfiguration;

    public LeaseId LeaseId { get; }
    public List<DadInt> WriteSet { get; }
    public bool FreeLease { get; }
    public ulong SequenceNum { get; set; }
    public ulong ServerId { get; }

    public ulong MessageId => ServerId + SequenceNum * (ulong)_processConfiguration.ServerProcesses.Count;

    public UpdateRequest(ProcessConfiguration processConfiguration, ulong serverId, LeaseId leaseId,
        List<DadInt> writeSet, bool freeLease, ulong sequenceNum = 0)
    {
        _processConfiguration = processConfiguration;
        SequenceNum = sequenceNum;
        ServerId = serverId;
        LeaseId = leaseId;
        WriteSet = writeSet;
        FreeLease = freeLease;
    }

    public int CompareTo(UpdateRequest? other)
    {
        if (this == other) return 0;
        if (null == other) return 1;

        return MessageId.CompareTo(other.MessageId);
    }
}