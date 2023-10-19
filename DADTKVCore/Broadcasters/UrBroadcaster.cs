namespace Dadtkv;

/// <summary>
///     Broadcaster for the Uniform Reliable Broadcast protocol.
///     Broadcasts a request to all clients.
/// </summary>
/// <typeparam name="TR">Type of the request.</typeparam>
/// <typeparam name="TA">Type of the response.</typeparam>
/// <typeparam name="TC">Type of the client.</typeparam>
public class UrBroadcaster<TR, TA, TC> where TR : IUrbRequest<TR>
{
    private readonly List<TC> _clients;
    private ulong _sequenceNumCounter;

    public UrBroadcaster(List<TC> clients)
    {
        _sequenceNumCounter = 0;
        _clients = clients;
    }

    /// <summary>
    ///     Broadcasts a request to all clients.
    ///     If a majority of clients respond, the request is delivered.
    /// </summary>
    /// <param name="request">Request to broadcast.</param>
    /// <param name="urbDeliver">Function to deliver the request.</param>
    /// <param name="getResponse">Function to get the response from a client.</param>
    public void UrBroadcast(TR request, Action<TR> urbDeliver, Func<TC, TR, Task<TA>> getResponse)
    {
        request.SequenceNum = ConcurrentUtils.GetAndIncrement(ref _sequenceNumCounter);

        var resTasks = _clients
            .Select(client => getResponse(client, request))
            .ToList();

        resTasks.Add(Task.FromResult(default(TA))!);
        var majority = DadtkvUtils.WaitForMajority(resTasks).Result;

        if (majority)
            urbDeliver(request);
    }

    /// <summary>
    ///     Broadcasts a request to all clients.
    /// </summary>
    /// <param name="request">Request to broadcast.</param>
    /// <param name="getResponse">Function to get the response from a client.</param>
    public void UrBroadcast(TR request, Func<TC, TR, Task<TA>> getResponse)
    {
        request.SequenceNum = ConcurrentUtils.GetAndIncrement(ref _sequenceNumCounter);
        _clients.ForEach(client => getResponse(client, request));
    }
}