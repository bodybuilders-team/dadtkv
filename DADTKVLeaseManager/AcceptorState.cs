namespace DADTKV;

public class AcceptorState
{
    public ulong ReadTimestamp = 0;
    public ConsensusValue? Value = null;
    public ulong WriteTimestamp = 0;
}