using Grpc.Core;

namespace DADTKV
{
    public class LeaseServiceImpl : LeaseService.LeaseServiceBase
    {
        
        public override Task<LeaseResponse> RequestLease(LeaseRequest request, ServerCallContext context)
        {
            // Implement your lease request logic here
            bool leaseGranted = GrantLease(request.ClientID, request.Set);

            
            return Task.FromResult(new LeaseResponse { Ok = leaseGranted });
        }

        private bool GrantLease(string clientID, IEnumerable<string> dataSet)
        {
            // Implement logic to grant leases based on clientID and dataSet
            // Return true if the lease is granted, false otherwise
            // You can keep track of granted leases in memory or a data store
            // Check for conflicts and manage lease expiration as needed
            return true; // Replace with actual implementation
        }
    }

    class PaxosServiceImpl : PaxosService.PaxosServiceBase
    {
        public override Task<Promise> Prepare(PrepareRequest request, ServerCallContext context)
        {
            return base.Prepare(request, context);
        }

        public override Task<AcceptResponse> Accept(AcceptRequest request, ServerCallContext context)
        {
            return base.Accept(request, context);
        }
    }

    class Program
    {
        // Entry point for the server application
        // Arguments: port, hostname
        static void Main(string[] args)
        {
            const int port = 50051; // args[0];
            const string hostname = "localhost"; // args[1];

            Server server = new Server
            {
                Services = { LeaseService.BindService(new LeaseServiceImpl()) },
                Ports = { new ServerPort(hostname, port, ServerCredentials.Insecure) }
            };

            server.Start();

            Console.WriteLine($"Lease Manager server listening on port {port}");
            Console.WriteLine("Press Enter to stop the server.");
            Console.ReadLine();

            server.ShutdownAsync().Wait();
        }
        
    }
}