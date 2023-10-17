namespace Dadtkv;

public class FreeLeaseRequest : IUrbRequest<FreeLeaseRequest> // TODO: Just URB? or FIFO URB? or TOB?
{
    private readonly ProcessConfiguration _processConfiguration;
    public readonly LeaseId LeaseId;

    public FreeLeaseRequest(ProcessConfiguration processConfiguration, LeaseId leaseId, ulong sequenceNum = 0)
    {
        _processConfiguration = processConfiguration;
        ServerId = processConfiguration.ServerId;
        SequenceNum = sequenceNum;
        LeaseId = leaseId;
    }

    public ulong MessageId => ServerId + SequenceNum * (ulong)_processConfiguration.ServerProcesses.Count;
    public ulong ServerId { get; }
    public ulong SequenceNum { get; set; }
}