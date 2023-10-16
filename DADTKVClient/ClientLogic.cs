using Google.Protobuf.Collections;
using Grpc.Net.Client;

namespace Dadtkv;

/// <summary>
///     Implements the client logic for the Dadtkv service.
/// </summary>
internal class ClientLogic
{
    private readonly DadtkvService.DadtkvServiceClient _client;
    private readonly string _clientId;

    public ClientLogic(string clientId, string serverUrl)
    {
        _clientId = clientId;
        _client = new DadtkvService.DadtkvServiceClient(GrpcChannel.ForAddress(serverUrl));
    }

    /// <summary>
    ///     Submits a transaction to the server.
    /// </summary>
    /// <param name="readSet">The keys to read from the server.</param>
    /// <param name="writeSet">The keys and values to write to the server.</param>
    /// <returns>The values read from the server.</returns>
    public async Task<List<DadInt>> TxSubmit(IEnumerable<string> readSet, IEnumerable<DadInt> writeSet)
    {
        var request = new TxSubmitRequestDto
        {
            ClientID = _clientId,
            ReadSet = { readSet },
            WriteSet = { writeSet }
        };
        
        var start = DateTime.Now;
        var response = await _client.TxSubmitAsync(request);
        var end = DateTime.Now;
        
        Console.WriteLine("Time taken: " + (end - start).TotalMilliseconds + "ms");
        
        return response.ReadSet.ToList();
    }

    /// <summary>
    ///     Gets the status of the system.
    /// </summary>
    public async Task<RepeatedField<string>> Status()
    {
        var request = new StatusRequestDto();
        var response = await _client.StatusAsync(request);
        return response.Status;
    }
}