using Microsoft.Extensions.Logging;

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
    
    private readonly ILogger<UrBroadcaster<TR, TA, TC>> _logger = DadtkvLogger.Factory.CreateLogger<UrBroadcaster<TR, TA, TC>>();

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
            .Select(client =>
                new DadtkvUtils.TaskWithRequest<TR, TA>(getResponse(client, request), request)
            )
            .ToList();

        resTasks.Add(new DadtkvUtils.TaskWithRequest<TR, TA>(Task.FromResult(default(TA))!, request));
        var majority = DadtkvUtils.WaitForMajority(resTasks, timeout: 0).Result;

        if (majority)
            urbDeliver(request);
    }
    
    /// <summary>
    ///     Broadcasts a request to all clients.
    ///     If a majority of clients respond, the request is delivered.
    /// </summary>
    /// <param name="request">Request to broadcast.</param>
    /// <param name="urbDeliver">Function to deliver the request.</param>
    /// <param name="getResponse">Function to get the response from a client.</param>
    public void UrBroadcast(TR request, Action<TR> urbDeliver, Func<TA, bool> predicate, Func<TC, TR, Task<TA>> getResponse)
    {
        request.SequenceNum = ConcurrentUtils.GetAndIncrement(ref _sequenceNumCounter);

        var resTasks = _clients
            .Select(client =>
                new DadtkvUtils.TaskWithRequest<TR, TA>(getResponse(client, request), request)
            )
            .ToList();

        resTasks.Add(new DadtkvUtils.TaskWithRequest<TR, TA>(Task.FromResult(default(TA))!, request));
        
        _logger.LogDebug($"Broadcasting URB. Waiting for majority of {resTasks.Count} tasks for request {request}");
        
        var majority = DadtkvUtils.WaitForMajority(resTasks, countSelf: true, predicate, timeout: 0).Result;
        
        _logger.LogDebug($"URB Majority is {majority} for request {request}");

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