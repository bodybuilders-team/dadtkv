namespace DADTKV;

public class AcceptorState
{
    public ulong ReadTimestamp = 0;
    public ConsensusValue? Value = null; //TODO: What is this used for
    public ulong WriteTimestamp = 0;
}