using Google.Protobuf.Collections;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

/// <summary>
///     Implements the client logic for the Dadtkv service.
/// </summary>
internal class ClientLogic
{
    private readonly DadtkvService.DadtkvServiceClient _client;
    private readonly string _clientId;
    private readonly ILogger<ClientLogic> _logger = DadtkvLogger.Factory.CreateLogger<ClientLogic>();

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
    public async Task<List<DadIntDto>> TxSubmit(IEnumerable<string> readSet, IEnumerable<DadIntDto> writeSet)
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

        _logger.LogDebug("Time taken: {timeTaken} ms", (end - start).TotalMilliseconds);

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