using Microsoft.Extensions.Logging;

namespace Dadtkv;

/// <summary>
///     Receiver for the FIFO Uniform Reliable Broadcast protocol.
/// </summary>
/// <typeparam name="TR">Type of the request.</typeparam>
/// <typeparam name="TA">Type of the response.</typeparam>
/// <typeparam name="TC">Type of the client.</typeparam>
public class FifoUrbReceiver<TR, TA, TC> where TR : IUrbRequest<TR>
{
    private readonly Action<TR> _fifoUrbDeliver;
    private readonly Dictionary<ulong, long> _lastProcessedMessageIdMap = new();
    private readonly Dictionary<ulong, List<FifoRequest>?> _pendingRequestsMap = new();
    private readonly ServerProcessConfiguration _serverProcessConfiguration;
    private readonly UrbReceiver<TR, TA, TC> _urbReceiver;

    private readonly ILogger<FifoUrbReceiver<TR, TA, TC>> _logger =
        DadtkvLogger.Factory.CreateLogger<FifoUrbReceiver<TR, TA, TC>>();

    public FifoUrbReceiver(List<TC> clients, Action<TR> fifoUrbDeliver, Func<TC, TR, Task<TA>> getResponse,
        ServerProcessConfiguration serverProcessConfiguration)
    {
        _fifoUrbDeliver = fifoUrbDeliver;
        _serverProcessConfiguration = serverProcessConfiguration;
        _urbReceiver = new UrbReceiver<TR, TA, TC>(clients, UrbDeliver, getResponse, serverProcessConfiguration);
    }

    public void FifoUrbProcessRequest(TR request)
    {
        _urbReceiver.UrbProcessRequest(request);
    }

    private void UrbDeliver(TR request)
    {
        _logger.LogDebug(
            $"Received Fifo Urb Request: {request}");
        var requestsToDeliver = new List<TR>();

        lock (this)
        {
            var serverId = request.ServerId;

            _lastProcessedMessageIdMap.TryAdd(serverId, -1);

            if (!_pendingRequestsMap.ContainsKey(serverId))
                _pendingRequestsMap[serverId] = new List<FifoRequest>();


            if ((long)request.SequenceNum > _lastProcessedMessageIdMap[serverId] + 1)
            {
                _pendingRequestsMap[serverId]!.AddSorted(new FifoRequest(request));
                return;
            }

            _lastProcessedMessageIdMap[serverId]++;

            requestsToDeliver.Add(request);
            // TODO make this readable
            for (var i = 0; i < _pendingRequestsMap[serverId]!.Count; i++)
            {
                var pendingRequest = _pendingRequestsMap[serverId]![i];
                if (!pendingRequest.Request.SequenceNum.Equals((ulong)(_lastProcessedMessageIdMap[serverId] + 1)))
                    break;

                _lastProcessedMessageIdMap[serverId]++;
                requestsToDeliver.Add(pendingRequest.Request);
                _pendingRequestsMap[serverId]!.RemoveAt(i--);
            }
        }

        requestsToDeliver.ForEach(_fifoUrbDeliver);
    }

    private class FifoRequest : IComparable<FifoRequest>
    {
        public TR Request { get; }

        public FifoRequest(TR request)
        {
            Request = request;
        }

        public int CompareTo(FifoRequest? other)
        {
            return Request.SequenceNum.CompareTo(other?.Request.SequenceNum);
        }
    }
}