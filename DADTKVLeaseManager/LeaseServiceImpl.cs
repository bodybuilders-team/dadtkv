using Grpc.Core;

namespace DADTKV;

public class LeaseServiceImpl : LeaseService.LeaseServiceBase
{
    private readonly object _lockObject;
    private readonly List<ILeaseRequest> _leaseRequests;

    public LeaseServiceImpl(object lockObject, List<ILeaseRequest> leaseRequests)
    {
        this._lockObject = lockObject;
        this._leaseRequests = leaseRequests;
    }

    public override Task<LeaseResponse> RequestLease(LeaseRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            // foreach (var leaseKey in request.Set)
            // {
            //     if (!LeaseQueue.ContainsKey(leaseKey))
            //     {
            //         LeaseQueue.Add(leaseKey, new Queue<string>());
            //     }
            //
            //
            //     if (!LeaseQueue[leaseKey].Contains(request.ClientID))
            //         LeaseQueue[leaseKey].Enqueue(request.ClientID);
            // }
            this._leaseRequests.Add(request);

            return Task.FromResult(new LeaseResponse { Ok = true });
        }
    }

    public override Task<FreeLeaseResponse> FreeLease(FreeLeaseRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            this._leaseRequests.Add(request);

            return Task.FromResult(new FreeLeaseResponse() { Ok = true });
        }
    }
}