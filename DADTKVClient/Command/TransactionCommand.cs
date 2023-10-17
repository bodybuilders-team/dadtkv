namespace Dadtkv;

/// <summary>
///     Command to execute a transaction.
/// </summary>
internal class TransactionCommand : ICommand
{
    public TransactionCommand(List<string> readSet, Dictionary<string, int> writeSet)
    {
        ReadSet = readSet;
        WriteSet = writeSet;
    }

    public List<string> ReadSet { get; }
    public Dictionary<string, int> WriteSet { get; }

    public override string ToString()
    {
        return $"(ReadSet: {ReadSet.ToStringRep()}, WriteSet: {WriteSet.ToStringRep()})";
    }
}