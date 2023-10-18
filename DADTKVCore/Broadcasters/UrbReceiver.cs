using Microsoft.Extensions.Logging;

namespace Dadtkv;

/// <summary>
///     Receiver for the Uniform Reliable Broadcast protocol.
///     Receives a request and sends it to all clients.
/// </summary>
/// <typeparam name="TR">Type of the request.</typeparam>
/// <typeparam name="TA">Type of the response.</typeparam>
/// <typeparam name="TC">Type of the client.</typeparam>
public class UrbReceiver<TR, TA, TC> where TR : IUrbRequest<TR>
{
    private readonly List<TC> _clients;
    private readonly Func<TC, TR, Task<TA>> _getResponse;
    private readonly HashSet<ulong> _msgIdLookup;
    private readonly ServerProcessConfiguration _serverProcessConfiguration;
    private readonly Action<TR> _urbDeliver;

    private readonly ILogger<UrbReceiver<TR, TA, TC>> _logger =
        DadtkvLogger.Factory.CreateLogger<UrbReceiver<TR, TA, TC>>();

    public UrbReceiver(List<TC> clients, Action<TR> urbDeliver, Func<TC, TR, Task<TA>> getResponse,
        ServerProcessConfiguration serverProcessConfiguration)
    {
        _msgIdLookup = new HashSet<ulong>();
        _clients = clients;
        _urbDeliver = urbDeliver;
        _getResponse = getResponse;
        _serverProcessConfiguration = serverProcessConfiguration;
    }

    /// <summary>
    ///     Process a request.
    ///     If the request has not been processed yet, it is sent to all clients.
    ///     If the request has been processed, it is ignored.
    ///     If a majority of clients respond with a response, the request is delivered.
    /// </summary>
    /// <param name="request">Request to be processed.</param>
    public void UrbProcessRequest(TR request)
    {
        var msgId = request.ServerId + request.SequenceNum * (ulong)_serverProcessConfiguration.ServerProcesses.Count;

        lock (_msgIdLookup)
        {
            if (_msgIdLookup.Contains(msgId))
                return;

            _msgIdLookup.Add(msgId);
        }

        var resTasks = _clients
            .Select(client => _getResponse(client, request))
            .ToList();

        _logger.LogDebug($"Waiting for URB majority for request: {request} (num tasks: {resTasks.Count})");
        var majority = DadtkvUtils.WaitForMajority(resTasks, _ => true);
        _logger.LogDebug($"Finished waiting for URB, majority: {majority} for request: {request}");

        if (majority)
            _urbDeliver(request);
    }
}