namespace Dadtkv.Requests.TxSubmitRequest;

public class TxSubmitRequest
{
    public readonly string ClientId;
    public readonly List<string> ReadSet;
    public readonly List<DadInt> WriteSet;

    public TxSubmitRequest(string clientId, List<string> readSet, List<DadInt> writeSet)
    {
        ClientId = clientId;
        ReadSet = readSet;
        WriteSet = writeSet;
    }

    public override string ToString()
    {
        return
            $"TxSubmitRequest(ClientId: {ClientId}, ReadSet: {ReadSet.ToStringRep()}, WriteSet: {WriteSet.ToStringRep()})";
    }
}