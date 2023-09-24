using Google.Protobuf.Collections;
using Grpc.Net.Client;

namespace DADTKV
{
    internal class ClientLogic
    {
        private readonly string _clientId;
        private readonly DADTKVService.DADTKVServiceClient _client;

        public ClientLogic(string clientId, string serverHostname, int serverPort)
        {
            this._clientId = clientId;

            // Set up the gRPC client
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var channel = GrpcChannel.ForAddress($"http://{serverHostname}:{serverPort}");
            _client = new DADTKVService.DADTKVServiceClient(channel);
        }

        public async Task<List<DadInt>> TxSubmit(IEnumerable<string> readSet, IEnumerable<DadInt> writeSet)
        {
            var request = new TxSubmitRequest
            {
                ClientID = _clientId,
                ReadSet = { readSet },
                WriteSet = { writeSet }
            };

            var response = await _client.TxSubmitAsync(request);
            return response.ReadSet.ToList();
        }

        public async Task<RepeatedField<string>> Status()
        {
            var request = new StatusRequest();
            var response = await _client.StatusAsync(request);
            return response.Status;
        }
    }
}