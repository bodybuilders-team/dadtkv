using DADTKV;

namespace DADTKVTransactionManager;

public class TobReceiver<TR, TA, TC>
{
    private readonly UrbReceiver<TR, TA, TC> _urbReceiver;
    private readonly Action<TR> _tobDeliver;
    private readonly Func<TR, ulong> _getMessageId;
    private long _lastProcessedMessageId = -1;
    private readonly List<TobRequest> _pendingRequests = new();

    class TobRequest : IComparable<TobRequest>
    {
        public TobRequest(TR request, long messageId)
        {
            this.Request = request;
            this.MessageId = messageId;
        }

        public TR Request { get; set; }
        public long MessageId { get; set; }

        public int CompareTo(TobRequest? other)
        {
            return MessageId.CompareTo(other?.MessageId);
        }
    }


    public TobReceiver(List<TC> clients, Action<TR> tobDeliver, Func<TR, ulong> getMessageId,
        Func<TC, TR, Task<TA>> getResponse)
    {
        _tobDeliver = tobDeliver;
        _getMessageId = getMessageId;
        _urbReceiver = new UrbReceiver<TR, TA, TC>(clients, UrbDeliver, getMessageId, getResponse);
    }
    
    public void TobProcessRequest(TR request)
    {
        _urbReceiver.UrbProcessRequest(request);
    }

    private void UrbDeliver(TR request)
    {
        lock (this)
        {
            var messageId = (long)_getMessageId(request);

            if (messageId > _lastProcessedMessageId + 1)
            {
                _pendingRequests.AddSorted(new TobRequest(request, messageId));
                return;
            }

            _lastProcessedMessageId++;
            _tobDeliver(request);

            // TODO make this readable
            for (var i = 0; i < _pendingRequests.Count; i++)
            {
                var pendingRequest = _pendingRequests[i];
                if (pendingRequest.MessageId != _lastProcessedMessageId + 1)
                    break;

                _lastProcessedMessageId++;
                _tobDeliver(pendingRequest.Request);
                _pendingRequests.RemoveAt(i--);
            }
        }
    }
}