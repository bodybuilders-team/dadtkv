namespace DADTKVTransactionServer
{
    // TODO: Needs to be server and client fo the state updade service
    // TODO: Needs to be Client of the lease service


    public class ServerService : DADTKVService.DADTKVServiceBase
    {
        
        public ServerService()
        {
        }

        public override Task<TxSubmitReply> TxSubmit(TxSubmitRequest request, ServerCallContext context)
        {
            // ...
        }

        public override Task<StatusReply> Status(StatusRequest request, ServerCallContext context)
        {
            // ...
        }
    }

    internal class Program
    {
        public static void Main()
        {
            // Set up the gRPC server
            const int port = 1001;
            const string hostname = "localhost";
            ServerPort serverPort;

            serverPort = new ServerPort(hostname, port, ServerCredentials.Insecure);

            Server server = new Server
            {
                Services = { ChatServerService.BindService(new ServerService()) },
                Ports = { serverPort }
            };

            // Start the gRPC server
            server.Start();
            Console.WriteLine($"Server listening on port {port}");

            // Configuring HTTP for client connections in Register method
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            // Keep the server running indefinitely
            while (true) ;
        }
    }
}