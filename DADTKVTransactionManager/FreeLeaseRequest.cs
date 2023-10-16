using DADTKV;

namespace DADTKVTransactionManager;

public class FreeLeaseRequest : IUrbRequest<FreeLeaseRequest>
{
    private readonly ProcessConfiguration _processConfiguration;
    public ulong ServerId;
    public ulong SequenceNum { get; set; }
    public LeaseId LeaseId;

    public FreeLeaseRequest(ProcessConfiguration processConfiguration,
        ulong serverId,
        LeaseId leaseId, ulong sequenceNum = 0)
    {
        _processConfiguration = processConfiguration;
        ServerId = processConfiguration.ServerId;
        SequenceNum = sequenceNum;
        LeaseId = leaseId;
    }


    public ulong MessageId => ServerId + SequenceNum * (ulong)_processConfiguration.ServerProcesses.Count;
}