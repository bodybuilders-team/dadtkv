using Grpc.Core;
using Grpc.Net.Client;
using DADTKV;

namespace DADTKVT;

public class DADTKVServiceImpl : DADTKVService.DADTKVServiceBase
{
    private readonly ProcessConfiguration _processConfiguration;
    private readonly object _lockObject;
    private ulong _sequenceNumCounter;
    private readonly List<StateUpdateService.StateUpdateServiceClient> _stateUpdateServiceClients = new();

    public DADTKVServiceImpl(object lockObject, ProcessConfiguration processConfiguration)
    {
        _processConfiguration = processConfiguration;
        _lockObject = lockObject;

        _processConfiguration.OtherTransactionManagers
            .Select(tm => GrpcChannel.ForAddress(tm.URL))
            .Select(channel => new StateUpdateService.StateUpdateServiceClient(channel))
            .ForEach((client) => this._stateUpdateServiceClients.Add(client));
    }

    public override Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            var lmChannel = GrpcChannel
                .ForAddress(_processConfiguration.SystemConfiguration.LeaseManagers.Random().URL);
            var lmClient = new LeaseService.LeaseServiceClient(lmChannel);
            var leaseRes = lmClient.RequestLease(new LeaseRequest
            {
                ClientID = _processConfiguration.ProcessInfo.Id,
                Set = { request.WriteSet.Select(x => x.Key) }
            });

            if (!leaseRes.Ok)
                throw new Exception(); //TODO: What to do?

            // Commit transaction
            var asyncUnaryCalls = new List<AsyncUnaryCall<UpdateResponse>>();
            foreach (var susClient in _stateUpdateServiceClients)
            {
                var updateReq = new UpdateRequest
                {
                    ServerId = _processConfiguration.ProcessInfo.Id,
                    SequenceNum = _sequenceNumCounter++,
                    WriteSet = { request.WriteSet }
                };

                var res = susClient.UpdateAsync(updateReq); //TODO: Check if throws exception when timeout
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

            cde.Wait(); // TODO: Check if majority was not reached


            // executeTransLocally()

            // ...
            return null;
        }
    }

    public override Task<StatusResponse> Status(StatusRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            // ...
            return null;
        }
    }
}