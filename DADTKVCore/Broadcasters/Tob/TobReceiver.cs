namespace Dadtkv;

public class TobReceiver<TR, TA, TC> where TR : ITobRequest<TR>
{
    private readonly List<TobRequest> _pendingRequests = new();
    private readonly Action<TR> _tobDeliver;
    private readonly UrbReceiver<TR, TA, TC> _urbReceiver;
    private long _lastProcessedMessageId = -1;
    private readonly ProcessConfiguration _processConfiguration;

    public TobReceiver(List<TC> clients, Action<TR> tobDeliver, Func<TC, TR, Task<TA>> getResponse,
        ProcessConfiguration processConfiguration)
    {
        _tobDeliver = tobDeliver;
        _processConfiguration = processConfiguration;
        _urbReceiver = new UrbReceiver<TR, TA, TC>(clients, UrbDeliver, getResponse, processConfiguration);
    }

    private class TobRequest : IComparable<TobRequest>
    {
        public TR Request { get; }

        public TobRequest(TR request)
        {
            Request = request;
        }

        public int CompareTo(TobRequest? other)
        {
            return Request.TobMessageId.CompareTo(other?.Request.TobMessageId);
        }
    }

    public void TobProcessRequest(TR request)
    {
        _urbReceiver.UrbProcessRequest(request);
    }

    private void UrbDeliver(TR request)
    {
        lock (this)
        {
            var messageId = request.TobMessageId;

            if ((long)messageId > _lastProcessedMessageId + 1)
            {
                _pendingRequests.AddSorted(new TobRequest(request));
                return;
            }

            _lastProcessedMessageId++;
            _tobDeliver(request);

            // TODO make this readable
            for (var i = 0; i < _pendingRequests.Count; i++)
            {
                var pendingRequest = _pendingRequests[i];
                if (pendingRequest.Request.TobMessageId != (ulong)(_lastProcessedMessageId + 1))
                    break;

                _lastProcessedMessageId++;
                _tobDeliver(pendingRequest.Request);
                _pendingRequests.RemoveAt(i--);
            }
        }
    }
}