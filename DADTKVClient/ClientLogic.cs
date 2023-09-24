using Grpc.Net.Client;

namespace DADTKV
{
    internal class ClientLogic
    {
        private string clientID;
        private readonly GrpcChannel channel;
        private readonly DADTKVService.DADTKVServiceClient client;

        public ClientLogic(string clientID, string serverHostname, int serverPort)
        {
            this.clientID = clientID;

            // Set up the gRPC client
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            channel = GrpcChannel.ForAddress($"http://{serverHostname}:{serverPort}");
            client = new DADTKVService.DADTKVServiceClient(channel);
        }

        public async Task<List<DadInt>> TxSubmit(List<string> readSet, List<DadInt> writeSet)
        {
            TxSubmitRequest request = new TxSubmitRequest
            {
                ClientID = clientID,
                ReadSet = { readSet },
                WriteSet = { writeSet }
            };

            TxSubmitResponse response = await client.TxSubmitAsync(request);
            return response.ReadSet.ToList();
        }

        public async Task Status()
        {
            StatusRequest request = new StatusRequest();
            StatusResponse response = await client.StatusAsync(request);
            // TODO: Implement statues
        }
    }
}