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
        var pendingRequestsToDeliver = new List<TR>();
        var messageId = request.TobMessageId;

        lock (this)
        {
            if ((long)messageId > _lastProcessedMessageId + 1)
            {
                _pendingRequests.AddSorted(new TobRequest(request));
                return;
            }

            pendingRequestsToDeliver.Add(request);
        }


        while (true)
        {
            lock (this)
            {
                if (_pendingRequests.Count <= 0 && pendingRequestsToDeliver.Count <= 0)
                    return;

                ProcessPending(pendingRequestsToDeliver);
            }

            foreach (var tobRequest in pendingRequestsToDeliver)
            {
                _tobDeliver(tobRequest);
                lock (this)
                {
                    _lastProcessedMessageId++;
                }
            }

            pendingRequestsToDeliver.Clear();
        }
    }

    private void ProcessPending(List<TR> pendingRequestsToDeliver)
    {
        for (var i = 0; i < _pendingRequests.Count; i++)
        {
            var pendingRequest = _pendingRequests[i];
            if (!pendingRequest.Request.TobMessageId.Equals(pendingRequestsToDeliver.Last().SequenceNum + 1))
                break;

            pendingRequestsToDeliver.Add(pendingRequest.Request);
            _pendingRequests.RemoveAt(i--);
        }
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