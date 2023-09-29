using Grpc.Core;

namespace DADTKV
{
    internal class LeaseConsensusValue
    {
        // TODO: add pointer to lease request
        public Dictionary<string, Queue<string>> LeaseQueue { get; }

        public LeaseConsensusValue()
        {
            LeaseQueue = new Dictionary<string, Queue<string>>();
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

            var lockObject = new Object();

            Server server = new Server
            {
                Services =
                {
                    LeaseService.BindService(new LeaseServiceImpl(lockObject)),
                    PaxosService.BindService(new PaxosServiceImpl(lockObject))
                },
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