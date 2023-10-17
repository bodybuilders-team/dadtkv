namespace Dadtkv;

/// <summary>
///     A request to learn a consensus value.
/// </summary>
public class LearnRequest : ITobRequest<LearnRequest>
{
    public LearnRequest(ulong serverId, ulong roundNumber,
        ConsensusValue consensusValue, ulong sequenceNum = 0)
    {
        ServerId = serverId;
        SequenceNum = sequenceNum;
        RoundNumber = roundNumber;
        ConsensusValue = consensusValue;
    }

    public ConsensusValue ConsensusValue { get; }
    public ulong RoundNumber { get; }
    public ulong ServerId { get; }
    public ulong SequenceNum { get; set; }

    public ulong TobMessageId => RoundNumber;

    public override string ToString()
    {
        return
            $"(ConsensusValue: {ConsensusValue}, RoundNumber: {RoundNumber}, ServerId: {ServerId}, SequenceNum: {SequenceNum})";
    }
}