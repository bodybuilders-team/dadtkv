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
    private readonly Dictionary<ulong, long> _lastProcessedSequenceNumMap = new();

    private readonly ILogger<FifoUrbReceiver<TR, TA, TC>> _logger =
        DadtkvLogger.Factory.CreateLogger<FifoUrbReceiver<TR, TA, TC>>();

    private readonly Dictionary<ulong, List<FifoRequest>?> _pendingRequestsMap = new();
    private readonly UrbReceiver<TR, TA, TC> _urbReceiver;

    public FifoUrbReceiver(List<TC> clients, Action<TR> fifoUrbDeliver, Func<TC, TR, Task<TA>> getResponse,
        ServerProcessConfiguration serverProcessConfiguration)
    {
        _fifoUrbDeliver = fifoUrbDeliver;
        _urbReceiver = new UrbReceiver<TR, TA, TC>(clients, UrbDeliver, getResponse, serverProcessConfiguration);
    }

    /// <summary>
    ///     Processes a request.
    /// </summary>
    /// <param name="request">The request to process.</param>
    public void FifoUrbProcessRequest(TR request)
    {
        _urbReceiver.UrbProcessRequest(request);
    }

    /// <summary>
    ///     Delivers a request.
    /// </summary>
    /// <param name="request">The request to deliver.</param>
    private void UrbDeliver(TR request)
    {
        _logger.LogDebug($"Received FIFO Request: {request}");
        var pendingRequestsToDeliver = new List<TR>();
        var broadcasterId = request.BroadcasterId;

        // TODO: Add lock for each broadcasterId (create map of locks)
        lock (this)
        {
            _lastProcessedSequenceNumMap.TryAdd(broadcasterId, -1);

            // TODO: Change this linked list
            if (!_pendingRequestsMap.ContainsKey(broadcasterId))
                _pendingRequestsMap[broadcasterId] = new List<FifoRequest>();

            if ((long)request.SequenceNum > _lastProcessedSequenceNumMap[broadcasterId] + 1)
            {
                _logger.LogDebug($"Adding FIFO request to pending: {request}");
                _pendingRequestsMap[broadcasterId]!.AddSorted(new FifoRequest(request));
                return;
            }

            pendingRequestsToDeliver.Add(request);
        }

        while (true)
        {
            lock (this)
            {
                if (_pendingRequestsMap[broadcasterId]!.Count <= 0 && pendingRequestsToDeliver.Count <= 0)
                    return;

                ProcessPending(broadcasterId, pendingRequestsToDeliver);
            }

            foreach (var req in pendingRequestsToDeliver)
            {
                _logger.LogDebug($"Delivering FIFO request: {req}");
                _fifoUrbDeliver(req);
                lock (this)
                {
                    _lastProcessedSequenceNumMap[broadcasterId]++;
                }
            }

            pendingRequestsToDeliver.Clear();
        }
    }

    private void ProcessPending(ulong broadcasterId, List<TR> requestsToDeliver)
    {
        for (var i = 0; i < _pendingRequestsMap[broadcasterId]!.Count; i++)
        {
            var pendingRequest = _pendingRequestsMap[broadcasterId]![i];
            if (!pendingRequest.Request.SequenceNum.Equals((ulong)_lastProcessedSequenceNumMap[broadcasterId] + 1))
                break;

            requestsToDeliver.Add(pendingRequest.Request);
            _pendingRequestsMap[broadcasterId]!.RemoveAt(i--);
        }
    }

    /// <summary>
    ///     A FIFO request.
    ///     Implements <see cref="IComparable{T}" /> to allow for sorting, using the SequenceNum.
    /// </summary>
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