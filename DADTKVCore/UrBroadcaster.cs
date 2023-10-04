using DADTKV;

namespace DADTKVCore;

public class UrBroadcaster<TR, TA, TC>
{
    private readonly HashSet<string> _msgIdLookup;
    private readonly List<TC> _clients;

    public UrBroadcaster(List<TC> clients)
    {
        _msgIdLookup = new HashSet<string>();
        _clients = clients;
    }

    public TA UrbProcessRequest(TR request, Func<TR, TA> urbDeliver, Func<TC, TR, Task<TA>> getResponse)
    {
        var resTasks = _clients
            .Select(client =>
                getResponse(client, request)
            ).ToList();

        var majority = DADTKVUtils.WaitForMajority(resTasks, (res) => true);

        if (majority)
          return  urbDeliver(request);

        return null;
    }
}