namespace Dadtkv;

/// <summary>
///     A simple in-memory data store that stores key-value pairs.
/// </summary>
public class DataStore
{
    private readonly Dictionary<string, int> _dataStorage = new();

    /// <summary>
    ///     Executes a transaction by writing the values in the write set to the data store.
    /// </summary>
    /// <param name="writeSet">The write set of the transaction.</param>
    public void ExecuteTransaction(IEnumerable<DadInt> writeSet)
    {
        foreach (var dadInt in writeSet)
            _dataStorage[dadInt.Key] = dadInt.Value;
    }

    /// <summary>
    ///     Executes a transaction by reading the values in the read set from the data store, and then
    ///     writing the values in the write set to the data store.
    /// </summary>
    /// <param name="readSet">The read set of the transaction.</param>
    /// <param name="writeSet">The write set of the transaction.</param>
    /// <returns>The values read from the data store.</returns>
    public List<DadInt> ExecuteTransaction(IEnumerable<string> readSet, IEnumerable<DadInt> writeSet)
    {
        var readData = new List<DadInt>();

        // TODO: Change DadInt to include null value
        foreach (var key in readSet)
            readData.Add(new DadInt(
                key,
                _dataStorage.GetValueOrNull(key)
            ));

        ExecuteTransaction(writeSet);

        return readData;
    }

    public override string ToString()
    {
        return $"DataStore( DataStorage: {_dataStorage.ToStringRep()})";
    }
}