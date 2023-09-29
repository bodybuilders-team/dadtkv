using Grpc.Core;

namespace DADTKV
{
    public class LeaseServiceImpl : LeaseService.LeaseServiceBase
    {
        private Object lockObject;
        public Dictionary<string, Queue<string>> LeaseQueue { get; }

        public LeaseServiceImpl(Object lockObject)
        {
            this.lockObject = lockObject;
        }

        public override Task<LeaseResponse> RequestLease(LeaseRequest request, ServerCallContext context)
        {
            lock (lockObject)
            {
                foreach (var leaseKey in request.Set)
                {
                    if (!LeaseQueue.ContainsKey(leaseKey))
                    {
                        LeaseQueue.Add(leaseKey, new Queue<string>());
                    }

                    LeaseQueue[leaseKey].Enqueue(request.ClientID);
                }

                return Task.FromResult(new LeaseResponse { Ok = true });
            }
        }
    }
}