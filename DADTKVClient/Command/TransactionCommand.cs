namespace DADTKV;

// Command to execute a transaction
// A transaction has a read set and a write set
internal class TransactionCommand : ICommand
{
    public List<string> ReadSet { get; }
    public Dictionary<string, int> WriteSet { get; }

    public TransactionCommand(List<string> readSet, Dictionary<string, int> writeSet)
    {
        ReadSet = readSet;
        WriteSet = writeSet;
    }
}