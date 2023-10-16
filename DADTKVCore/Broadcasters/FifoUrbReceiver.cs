using DADTKV;

namespace DADTKVTransactionManager;

public class FifoUrbReceiver<TR, TA, TC> where TR : IFifoUrbRequest<TR>
{
    private readonly UrbReceiver<TR, TA, TC> _urbReceiver;
    private readonly Action<TR> _tobDeliver;
    private Dictionary<ulong, long> _lastProcessedMessageId = new Dictionary<ulong, long>();
    private readonly Dictionary<ulong, List<TR>> _pendingRequests = new();

    public FifoUrbReceiver(List<TC> clients, Action<TR> tobDeliver,
        Func<TC, TR, Task<TA>> getResponse)
    {
        _tobDeliver = tobDeliver;
        _urbReceiver = new UrbReceiver<TR, TA, TC>(clients, UrbDeliver, getResponse);
    }

    public void FifoUrbProcessRequest(TR request)
    {
        _urbReceiver.UrbProcessRequest(request);
    }

    private void UrbDeliver(TR request)
    {
        lock (this)
        {
            var messageId = request.MessageId;

            var serverId = request.ServerId;
        }
    }
}