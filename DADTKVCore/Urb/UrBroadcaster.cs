namespace DADTKV;

/// <summary>
/// Broadcaster for the Uniform Reliable Broadcast protocol.
/// Broadcasts a request to all clients.
/// </summary>
/// <typeparam name="TR">Type of the request.</typeparam>
/// <typeparam name="TA">Type of the response.</typeparam>
/// <typeparam name="TC">Type of the client.</typeparam>
public class UrBroadcaster<TR, TA, TC>
{
    private readonly List<TC> _clients;
    private ulong _sequenceNumCounter;

    public UrBroadcaster(List<TC> clients)
    {
        _sequenceNumCounter = 0;
        _clients = clients;
    }

    /// <summary>
    /// Broadcasts a request to all clients.
    /// If a majority of clients respond, the request is delivered.
    /// </summary>
    /// <param name="request">Request to broadcast.</param>
    /// <param name="updateSequenceNumber">Function to update the sequence number of the request.</param>
    /// <param name="urbDeliver">Function to deliver the request.</param>
    /// <param name="getResponse">Function to get the response from a client.</param>
    public void UrBroadcast(
        TR request,
        Action<TR, ulong> updateSequenceNumber,
        Action<TR> urbDeliver,
        Func<TC, TR, Task<TA>> getResponse
    )
    {
        updateSequenceNumber(request, _sequenceNumCounter++);

        var resTasks = _clients
            .Select(client => getResponse(client, request))
            .ToList();

        var majority = DADTKVUtils.WaitForMajority(resTasks, res => true);

        if (majority)
            urbDeliver(request);
    }
}