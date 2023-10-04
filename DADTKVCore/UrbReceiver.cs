using DADTKV;

namespace DADTKVTransactionManager;

public class UrbReceiver<TR, TA, TC>
{
    private readonly HashSet<string> _msgIdLookup;
    private readonly List<TC> _clients;
    private readonly Func<TR, TA> _urbDeliver;
    private readonly Func<TR, string> _getMessageId;
    private readonly Func<TR, TA> _onDuplicate;
    private readonly Func<TC, TR, Task<TA>> _getResponse;

    public UrbReceiver(List<TC> clients, Func<TR, TA> urbDeliver, Func<TR, string> getMessageId,
        Func<TR, TA> onDuplicate, Func<TC, TR, Task<TA>> getResponse)
    {
        _msgIdLookup = new HashSet<string>();
        _clients = clients;
        _urbDeliver = urbDeliver;
        _getMessageId = getMessageId;
        _onDuplicate = onDuplicate;
        _getResponse = getResponse;
    }

    public TA UrbProcessRequest(TR request)
    {
        var msgId = _getMessageId(request);

        if (_msgIdLookup.Contains(msgId))
            return _onDuplicate(request);

        _msgIdLookup.Add(msgId);

        var resTasks = _clients
            .Select(client =>
                _getResponse(client, request)
            ).ToList();

        DADTKVUtils.WaitForMajority(resTasks, (res) => true);

        return _urbDeliver(request);
    }
}