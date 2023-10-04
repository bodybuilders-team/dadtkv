using DADTKV;

namespace DADTKVTransactionManager;

public class UrbReceiver<TR, TA, TC>
{
    private readonly HashSet<string> _msgIdLookup;
    private readonly List<TC> _clients;
    private readonly Action<TR> _urbDeliver;
    private readonly Func<TR, string> _getMessageId;
    private readonly Func<TC, TR, Task<TA>> _getResponse;

    public UrbReceiver(List<TC> clients, Action<TR> urbDeliver, Func<TR, string> getMessageId,
        Func<TC, TR, Task<TA>> getResponse)
    {
        _msgIdLookup = new HashSet<string>();
        _clients = clients;
        _urbDeliver = urbDeliver;
        _getMessageId = getMessageId;
        _getResponse = getResponse;
    }

    public void UrbProcessRequest(TR request)
    {
        var msgId = _getMessageId(request);

        if (_msgIdLookup.Contains(msgId))
            return;

        _msgIdLookup.Add(msgId);

        var resTasks = _clients
            .Select(client =>
                _getResponse(client, request)
            ).ToList();

        var majority = DADTKVUtils.WaitForMajority(resTasks, (res) => true);

        if (majority)
            _urbDeliver(request);
    }
}