using DADTKV;

namespace DADTKVTransactionManager;

public class TobReceiver<TR, TA, TC> where TR : IFifoUrbRequest<TR>
{
    private readonly UrbReceiver<TR, TA, TC> _urbReceiver;
    private readonly Action<TR> _tobDeliver;
    private readonly Func<TR, ulong> _getMessageId;
    private long _lastProcessedMessageId = -1;
    private readonly List<TR> _pendingRequests = new();

    public TobReceiver(List<TC> clients, Action<TR> tobDeliver, Func<TR, ulong> getMessageId,
        Func<TC, TR, Task<TA>> getResponse)
    {
        _tobDeliver = tobDeliver;
        _getMessageId = getMessageId;
        _urbReceiver = new UrbReceiver<TR, TA, TC>(clients, UrbDeliver, getResponse);
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
                _pendingRequests.AddSorted(request);
                return;
            }

            _lastProcessedMessageId++;
            _tobDeliver(request);

            // TODO make this readable
            for (var i = 0; i < _pendingRequests.Count; i++)
            {
                var pendingRequest = _pendingRequests[i];
                if (pendingRequest.MessageId != (ulong)(_lastProcessedMessageId + 1))
                    break;

                _lastProcessedMessageId++;
                _tobDeliver(pendingRequest);
                _pendingRequests.RemoveAt(i--);
            }
        }
    }
}