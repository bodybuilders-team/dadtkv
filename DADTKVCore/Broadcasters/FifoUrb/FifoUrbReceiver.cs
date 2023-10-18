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

    private readonly ILogger<FifoUrbReceiver<TR, TA, TC>> _logger =
        DadtkvLogger.Factory.CreateLogger<FifoUrbReceiver<TR, TA, TC>>();

    private readonly Dictionary<ulong, List<FifoRequest>?> _pendingRequestsMap = new();
    private readonly ServerProcessConfiguration _serverProcessConfiguration;
    private readonly UrbReceiver<TR, TA, TC> _urbReceiver;

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
            var senderId = request.SenderId;

            _lastProcessedMessageIdMap.TryAdd(senderId, -1);

            if (!_pendingRequestsMap.ContainsKey(senderId))
                _pendingRequestsMap[senderId] = new List<FifoRequest>();


            if ((long)request.SequenceNum > _lastProcessedMessageIdMap[senderId] + 1)
            {
                _pendingRequestsMap[senderId]!.AddSorted(new FifoRequest(request));
                return;
            }

            _lastProcessedMessageIdMap[senderId]++;

            requestsToDeliver.Add(request);
            // TODO make this readable
            for (var i = 0; i < _pendingRequestsMap[senderId]!.Count; i++)
            {
                var pendingRequest = _pendingRequestsMap[senderId]![i];
                if (!pendingRequest.Request.SequenceNum.Equals((ulong)(_lastProcessedMessageIdMap[senderId] + 1)))
                    break;

                _lastProcessedMessageIdMap[senderId]++;
                requestsToDeliver.Add(pendingRequest.Request);
                _pendingRequestsMap[senderId]!.RemoveAt(i--);
            }
        }

        requestsToDeliver.ForEach(_fifoUrbDeliver);
    }

    private class FifoRequest : IComparable<FifoRequest>
    {
        public FifoRequest(TR request)
        {
            Request = request;
        }

        public TR Request { get; }

        public int CompareTo(FifoRequest? other)
        {
            return Request.SequenceNum.CompareTo(other?.Request.SequenceNum);
        }
    }
}