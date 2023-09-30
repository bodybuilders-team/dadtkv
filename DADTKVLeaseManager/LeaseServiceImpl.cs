using Grpc.Core;

namespace DADTKV;

public class LeaseServiceImpl : LeaseService.LeaseServiceBase
{
    private readonly object _lockObject;
    private readonly List<ILeaseRequest> _leaseRequests;

    public LeaseServiceImpl(object lockObject, List<ILeaseRequest> leaseRequests)
    {
        _lockObject = lockObject;
        _leaseRequests = leaseRequests;
    }

    public override Task<LeaseResponse> RequestLease(LeaseRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            _leaseRequests.Add(request);
            return Task.FromResult(new LeaseResponse { Ok = true });
        }
    }

    public override Task<FreeLeaseResponse> FreeLease(FreeLeaseRequest request, ServerCallContext context)
    {
        lock (_lockObject)
        {
            _leaseRequests.Add(request);
            return Task.FromResult(new FreeLeaseResponse() { Ok = true });
        }
    }
}