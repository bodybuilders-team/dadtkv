namespace DADTKV;

/// <summary>
/// Command to execute a transaction.
/// </summary>
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