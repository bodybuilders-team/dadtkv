namespace Dadtkv;

/// <summary>
///     A request to learn a consensus value.
/// </summary>
public class LearnRequest : ITobRequest<LearnRequest>
{
    private readonly ProcessConfiguration _processConfiguration;

    public LearnRequest(ProcessConfiguration processConfiguration, ulong serverId, ulong roundNumber,
        ConsensusValue consensusValue, ulong sequenceNum = 0)
    {
        _processConfiguration = processConfiguration;
        ServerId = serverId;
        SequenceNum = sequenceNum;
        RoundNumber = roundNumber;
        ConsensusValue = consensusValue;
    }

    public ulong RoundNumber { get; }
    public ConsensusValue ConsensusValue { get; }
    public ulong UrbMessageId => ServerId + SequenceNum * (ulong)_processConfiguration.ServerProcesses.Count;
    public ulong ServerId { get; }
    public ulong SequenceNum { get; set; }
    public ulong TobMessageId { get; set; }

    public int CompareTo(LearnRequest? other)
    {
        return TobMessageId.CompareTo(other?.TobMessageId);
    }
}