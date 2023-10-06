using DADTKV;

namespace DADTKVTransactionManager;

public class DataStore
{
    private readonly Dictionary<string, int> _dataStore = new();

    public void ExecuteTransaction(IEnumerable<DadInt> writeSet)
    {
        foreach (var dadInt in writeSet) _dataStore[dadInt.Key] = dadInt.Value;
    }

    public List<DadInt> ExecuteTransaction(IEnumerable<string> readSet, IEnumerable<DadInt> writeSet)
    {
        var readData = new List<DadInt>();

        //TODO: Change DadInt to include null value
        foreach (var key in readSet)
            readData.Add(new DadInt
            {
                Key = key,
                Value = _dataStore.GetValueOrNull(key)
            });

        ExecuteTransaction(writeSet);

        return readData;
    }
}