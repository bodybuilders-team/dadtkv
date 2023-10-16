using DADTKVTransactionManager;

namespace DADTKV;

public class LearnRequest : IFifoUrbRequest<LearnRequest>
{
    private readonly ProcessConfiguration _processConfiguration;
    public ulong ServerId { get; }
    public ulong RoundNumber { get; }
    public ConsensusValue ConsensusValue { get; }
    public ulong SequenceNum { get; set; }

    public ulong MessageId => ServerId + SequenceNum * (ulong)(_processConfiguration.ServerProcesses.Count);

    public LearnRequest(ProcessConfiguration processConfiguration, ulong serverId, ulong roundNumber,
        ConsensusValue consensusValue, ulong sequenceNum = 0)
    {
        _processConfiguration = processConfiguration;
        ServerId = serverId;
        SequenceNum = sequenceNum;
        RoundNumber = roundNumber;
        ConsensusValue = consensusValue;
    }

    public int CompareTo(LearnRequest? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        var serverIdComparison = ServerId.CompareTo(other.ServerId);
        if (serverIdComparison != 0) return serverIdComparison;
        var roundNumberComparison = RoundNumber.CompareTo(other.RoundNumber);
        if (roundNumberComparison != 0) return roundNumberComparison;
        return SequenceNum.CompareTo(other.SequenceNum);
    }
}