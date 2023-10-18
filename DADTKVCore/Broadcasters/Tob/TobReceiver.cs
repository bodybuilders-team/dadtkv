namespace Dadtkv;

/// <summary>
///     Receiver for Total Order Broadcast.
/// </summary>
/// <typeparam name="TR">Type of the request.</typeparam>
/// <typeparam name="TA">Type of the response.</typeparam>
/// <typeparam name="TC">Type of the client.</typeparam>
public class TobReceiver<TR, TA, TC> where TR : ITobRequest<TR>
{
    private readonly List<TobRequest> _pendingRequests = new();
    private readonly Action<TR> _tobDeliver;
    private readonly UrbReceiver<TR, TA, TC> _urbReceiver;
    private long _lastProcessedMessageId = -1;

    public TobReceiver(List<TC> clients, Action<TR> tobDeliver, Func<TC, TR, Task<TA>> getResponse,
        ServerProcessConfiguration serverProcessConfiguration)
    {
        _tobDeliver = tobDeliver;
        _urbReceiver = new UrbReceiver<TR, TA, TC>(clients, UrbDeliver, getResponse, serverProcessConfiguration);
    }

    /// <summary>
    ///     Processes a request.
    /// </summary>
    /// <param name="request">The request to process.</param>
    public void TobProcessRequest(TR request)
    {
        _urbReceiver.UrbProcessRequest(request);
    }

    /// <summary>
    ///     Delivers a request.
    /// </summary>
    /// <param name="request">The request to deliver.</param>
    private void UrbDeliver(TR request)
    {
        var requestsToDeliver = new List<TR>();

        lock (this)
        {
            var messageId = request.TobMessageId;

            if ((long)messageId > _lastProcessedMessageId + 1)
            {
                _pendingRequests.AddSorted(new TobRequest(request));
                return;
            }

            _lastProcessedMessageId++;
            requestsToDeliver.Add(request);

            // TODO make this readable
            for (var i = 0; i < _pendingRequests.Count; i++)
            {
                var pendingRequest = _pendingRequests[i];
                if (!pendingRequest.Request.TobMessageId.Equals((ulong)(_lastProcessedMessageId + 1)))
                    break;

                _lastProcessedMessageId++;
                requestsToDeliver.Add(pendingRequest.Request);
                _pendingRequests.RemoveAt(i--);
            }
        }

        requestsToDeliver.ForEach(_tobDeliver);
    }

    /// <summary>
    ///     A Total Order Broadcast request.
    ///     Implements <see cref="IComparable{T}" /> to allow for sorting, using the TobMessageId.
    /// </summary>
    private class TobRequest : IComparable<TobRequest>
    {
        public TobRequest(TR request)
        {
            Request = request;
        }

        public TR Request { get; }

        public int CompareTo(TobRequest? other)
        {
            return Request.TobMessageId.CompareTo(other?.Request.TobMessageId);
        }
    }
}