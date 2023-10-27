namespace Dadtkv;

/// <summary>
///     State of an acceptor in the Paxos algorithm.
///     Contains:
///     - ReadTimestamp: Timestamp of the last read request.
///     - Value: Value of the last read request.
///     - WriteTimestamp: Timestamp of the last write request.
/// </summary>
public class AcceptorState
{
    public ulong ReadTimestamp = 0;
    public ConsensusValue? Value = null;
    public ulong WriteTimestamp = 0;
}