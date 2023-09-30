namespace DADTKV;


public class ConsensusState
{
    public ulong ReadTimestamp = 0;
    public ulong WriteTimestamp = 0;
    public ConsensusValue? Value = null;
}