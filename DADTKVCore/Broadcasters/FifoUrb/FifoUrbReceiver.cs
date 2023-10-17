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
    private readonly ProcessConfiguration _processConfiguration;
    private readonly UrbReceiver<TR, TA, TC> _urbReceiver;

    public FifoUrbReceiver(List<TC> clients, Action<TR> fifoUrbDeliver, Func<TC, TR, Task<TA>> getResponse,
        ProcessConfiguration processConfiguration)
    {
        _fifoUrbDeliver = fifoUrbDeliver;
        _processConfiguration = processConfiguration;
        _urbReceiver = new UrbReceiver<TR, TA, TC>(clients, UrbDeliver, getResponse, processConfiguration);
    }

    public void FifoUrbProcessRequest(TR request)
    {
        _urbReceiver.UrbProcessRequest(request);
    }

    private void UrbDeliver(TR request)
    {
        lock (this)
        {
            var messageId = request.ServerId + request.SequenceNum * (ulong)_processConfiguration.ServerProcesses.Count;
            var serverId = request.ServerId;

            if (!_lastProcessedMessageIdMap.ContainsKey(serverId))
                _lastProcessedMessageIdMap[serverId] = -1;

            if (!_pendingRequestsMap.ContainsKey(serverId))
                _pendingRequestsMap[serverId] = new List<FifoRequest>();

            if ((long)messageId > _lastProcessedMessageIdMap[serverId] + 1)
            {
                _pendingRequestsMap[serverId].AddSorted(new FifoRequest(request, messageId));
                return;
            }

            _lastProcessedMessageIdMap[serverId]++;
            _fifoUrbDeliver(request);

            // TODO make this readable
            for (var i = 0; i < _pendingRequestsMap[serverId].Count; i++)
            {
                var pendingRequest = _pendingRequestsMap[serverId][i];
                if (pendingRequest.FifoMessageId != (ulong)(_lastProcessedMessageIdMap[serverId] + 1))
                    break;

                _lastProcessedMessageIdMap[serverId]++;
                _fifoUrbDeliver(pendingRequest.Request);
                _pendingRequestsMap[serverId].RemoveAt(i--);
            }
        }
    }

    private class FifoRequest : IComparable<FifoRequest>
    {
        public readonly ulong FifoMessageId;

        public FifoRequest(TR request, ulong fifoMessageId)
        {
            Request = request;
            FifoMessageId = fifoMessageId;
        }

        public TR Request { get; }

        public int CompareTo(FifoRequest? other)
        {
            return FifoMessageId.CompareTo(other?.FifoMessageId);
        }
    }
}