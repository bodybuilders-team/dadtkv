namespace Dadtkv;

/// <summary>
///     A request to learn a consensus value.
/// </summary>
public class LearnRequest : ITobRequest<LearnRequest>
{
    public LearnRequest(ulong senderId, ulong roundNumber,
        ConsensusValue consensusValue, ulong sequenceNum = 0)
    {
        SenderId = senderId;
        SequenceNum = sequenceNum;
        RoundNumber = roundNumber;
        ConsensusValue = consensusValue;
    }

    public ConsensusValue ConsensusValue { get; }
    public ulong RoundNumber { get; }
    public ulong SenderId { get; }
    public ulong SequenceNum { get; set; }

    public ulong TobMessageId => RoundNumber;

    public override string ToString()
    {
        return
            $"LearnRequest(ConsensusValue: {ConsensusValue}, RoundNumber: {RoundNumber}, SenderId: {SenderId}, SequenceNum: {SequenceNum})";
    }
}