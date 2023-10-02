using Grpc.Core;
using Grpc.Net.Client;
using Google.Protobuf.Collections;

namespace DADTKV;

internal class StateUpdateServiceImpl : StateUpdateService.StateUpdateServiceBase
{
    private readonly object _lockObject;
    private readonly ProcessConfiguration _processConfiguration;
    private readonly Dictionary<string, HashSet<ulong>> _sequenceNumCounterLookup;
    private readonly List<StateUpdateService.StateUpdateServiceClient> _stateUpdateServiceClients;

    public StateUpdateServiceImpl(object lockObject, ProcessConfiguration processConfiguration)
    {
        _lockObject = lockObject;
        _processConfiguration = processConfiguration;
        _sequenceNumCounterLookup = new Dictionary<string, HashSet<ulong>>();
        _sequenceNumCounterLookup = new Dictionary<string, HashSet<ulong>>();

        _processConfiguration = processConfiguration;
        foreach (var tm in processConfiguration.SystemConfiguration.TransactionManagers)
        {
            _sequenceNumCounterLookup[tm.Id] = new HashSet<ulong>();
        }

        _stateUpdateServiceClients = processConfiguration.OtherTransactionManagers
            .Select(tm => GrpcChannel.ForAddress(tm.URL))
            .Select(channel => new StateUpdateService.StateUpdateServiceClient(channel))
            .ToList();
    }

    public override Task<UpdateResponse> Update(UpdateRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            var currSeqNumSet = _sequenceNumCounterLookup[request.ServerId];

            if (currSeqNumSet.Contains(request.SequenceNum))
                return Task.FromResult(new UpdateResponse { Ok = true }); //TODO: Should it be okay?

            _sequenceNumCounterLookup[request.ServerId].Add(request.SequenceNum);

            var asyncUnaryCalls = new List<AsyncUnaryCall<UpdateResponse>>();
            foreach (var stateUpdateServiceClient in _stateUpdateServiceClients)
            {
                var res = stateUpdateServiceClient.UpdateAsync(request);
                asyncUnaryCalls.Add(res);
            }

            var cde = new CountdownEvent(_processConfiguration.SystemConfiguration.TransactionManagers.Count / 2);
            foreach (var asyncUnaryCall in asyncUnaryCalls)
            {
                var thread = new Thread(() =>
                {
                    asyncUnaryCall.ResponseAsync.Wait();
                    var res = asyncUnaryCall.ResponseAsync.Result;
                    if (!res.Ok) //TODO: Is this necessary?
                        throw new Exception();

                    cde.Signal();
                });
                thread.Start();
            }

            cde.Wait();

            //TODO: Needs majority to deliver
            // Deliver
            Deliver(request.WriteSet);

            return Task.FromResult(new UpdateResponse { Ok = true });
        }
    }

    private void Deliver(RepeatedField<DadInt> writeSet)
    {
        ExecuteTrans(writeSet);
    }

    private void ExecuteTrans(RepeatedField<DadInt> writeSet)
    {
    }
}