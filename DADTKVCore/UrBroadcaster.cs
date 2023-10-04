using DADTKV;

namespace DADTKVCore;

public class UrBroadcaster<TR, TA, TC>
{
    private readonly List<TC> _clients;
    private ulong _sequenceNumCounter;

    public UrBroadcaster(List<TC> clients)
    {
        _sequenceNumCounter = 0;
        _clients = clients;
    }

    public void UrBroadcast(TR request,
        Action<TR, ulong> updateSequenceNumber,
        Action<TR> urbDeliver,
        Func<TC, TR, Task<TA>> getResponse)
    {
        updateSequenceNumber(request, _sequenceNumCounter++);

        var resTasks = _clients
            .Select(client =>
                getResponse(client, request)
            ).ToList();

        var majority = DADTKVUtils.WaitForMajority(resTasks, (res) => true);

        if (majority)
            urbDeliver(request);
    }
}