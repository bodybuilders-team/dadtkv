using Grpc.Core;
using Grpc.Net.Client;
using DADTKV;

namespace DADTKVT;

public class DADTKVServiceImpl : DADTKVService.DADTKVServiceBase
{
    private readonly ProcessConfiguration _processConfiguration;
    private readonly object _lockObject;
    private ulong _sequenceNumCounter = 0; 
    private readonly List<StateUpdateService.StateUpdateServiceClient> _stateUpdateServiceClients = new();

    public DADTKVServiceImpl(object lockObject, ProcessConfiguration processConfiguration)
    {
        this._processConfiguration = processConfiguration;
        this._lockObject = lockObject;

        _processConfiguration.OtherTransactionManagers
            .Select(tm => GrpcChannel.ForAddress(tm.URL))
            .Select(channel => new StateUpdateService.StateUpdateServiceClient(channel))
            .ForEach((client) => this._stateUpdateServiceClients.Add(client));
    }

    public override Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            var lmChannel =
                GrpcChannel.ForAddress(_processConfiguration.SystemConfiguration.LeaseManagers.Random().URL);
            var lmClient = new LeaseService.LeaseServiceClient(lmChannel);
            var leaseRes = lmClient.RequestLease(new LeaseRequest
            {
                ClientID = _processConfiguration.ProcessInfo.Id,
                Set = { request.WriteSet.Select(x => x.Key) }
            });

            if (!leaseRes.Ok)
                throw new Exception(); //TODO: What to do?

            // Commit transaction
            foreach (var susClient in _stateUpdateServiceClients)
            {
                var updateReq = new UpdateRequest
                {
                    ServerId = _processConfiguration.ProcessInfo.Id,
                    SequenceNum = _sequenceNumCounter++,
                    WriteSet = { request.WriteSet }
                };

               var res= susClient.UpdateBroadcast(updateReq); //TODO: Check if throws exception when timeout
               //TODO: Needs majority
            }
            


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