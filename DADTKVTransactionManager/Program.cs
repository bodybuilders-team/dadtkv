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
            const int port = 1001; // args[0];
            const string hostname = "localhost"; // args[1];

            Server server = new Server
            {
                Services = { DADTKVService.BindService(new ServerService()) },
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