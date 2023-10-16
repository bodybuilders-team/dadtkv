namespace DADTKV;

public class FreeLeaseRequest : IUrbRequest<FreeLeaseRequest>
{
    private readonly ProcessConfiguration _processConfiguration;
    public readonly LeaseId LeaseId;
    public readonly ulong ServerId;

    public ulong SequenceNum { get; set; }
    public ulong MessageId => ServerId + SequenceNum * (ulong)_processConfiguration.ServerProcesses.Count;

    public FreeLeaseRequest(ProcessConfiguration processConfiguration, LeaseId leaseId, ulong sequenceNum = 0)
    {
        _processConfiguration = processConfiguration;
        ServerId = processConfiguration.ServerId;
        SequenceNum = sequenceNum;
        LeaseId = leaseId;
    }
}