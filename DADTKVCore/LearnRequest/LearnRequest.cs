namespace Dadtkv;

/// <summary>
///     A request to learn a consensus value.
/// </summary>
public class LearnRequest : ITobRequest<LearnRequest>
{
    public LearnRequest(ulong serverId, ulong broadcasterId, ulong roundNumber,
        ConsensusValue consensusValue, ulong sequenceNum = 0)
    {
        ServerId = serverId;
        BroadcasterId = broadcasterId;
        SequenceNum = sequenceNum;
        RoundNumber = roundNumber;
        ConsensusValue = consensusValue;
    }

    public ulong ServerId { get; set; }
    public ConsensusValue ConsensusValue { get; }
    public ulong RoundNumber { get; }
    public ulong BroadcasterId { get; }
    public ulong SequenceNum { get; set; }

    public ulong TobMessageId => RoundNumber - 1;

    public override string ToString()
    {
        return
            $"LearnRequest(ConsensusValue: {ConsensusValue}, RoundNumber: {RoundNumber}, BroadcasterId: {BroadcasterId}, SequenceNum: {SequenceNum})";
    }
}