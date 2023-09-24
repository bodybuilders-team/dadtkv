using DADTKVTransactionManagerServer;
using Grpc.Core;

namespace DADTKV
{
    // TODO: Needs to be server and client fo the state updade service
    // TODO: Needs to be Client of the lease service

    public class ServerService : DADTKVService.DADTKVServiceBase
    {
        public ServerService()
        {
        }

        public override Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request, ServerCallContext context)
        {
            return Task.FromResult(new TxSubmitResponse());
        }

        public override Task<StatusResponse> Status(StatusRequest request, ServerCallContext context)
        {
            return Task.FromResult(new StatusResponse());
        }
    }

    internal class Program
    {
        // Entry point for the server application
        // Arguments: port, hostname
        public static void Main(string[] args)
        {
            Console.WriteLine("Server Identifier: ");
            var serverId = Convert.ToUInt64(Console.ReadLine());

            // Set up the gRPC server
            var port = (int)(1000 + serverId);
            const string hostname = "localhost"; // args[1];

            var transactionManagersLookup = new Dictionary<ulong, string>
            {
                {1, "http://localhost:1001"},
                {2, "http://localhost:1002"},
                {3, "http://localhost:1003"}
            };

            transactionManagersLookup.Remove(serverId);

            var server = new Server
            {
                Services =
                {
                    DADTKVService.BindService(
                        new DADTKVServiceImpl(transactionManagersLookup, serverId, "TODO") //TODO Add lease manager url
                    ),
                    StateUpdateService.BindService(
                        new StateUpdateServiceImpl(transactionManagersLookup)
                    )
                },
                Ports = { new ServerPort(hostname, port, ServerCredentials.Insecure) }
            };

            server.Start();

            Console.WriteLine($"Transaction Manager server listening on port {port}");
            Console.WriteLine("Press Enter to stop the server.");
            Console.ReadLine();

            server.ShutdownAsync().Wait();
        }
    }
}