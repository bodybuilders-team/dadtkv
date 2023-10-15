using Grpc.Core;

namespace DADTKV;

/// <summary>
///     Implementation of the StateUpdateService.
/// </summary>
internal class StateUpdateServiceImpl : StateUpdateService.StateUpdateServiceBase
{
    private readonly DataStore _dataStore;

    public StateUpdateServiceImpl(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    /// <summary>
    ///     Executes a transaction associated with an update.
    /// </summary>
    /// <param name="request">The update request.</param>
    /// <param name="context">The server call context.</param>
    /// <returns>The update response.</returns>
    public override Task<UpdateResponse> Update(UpdateRequest request, ServerCallContext context)
    {
        lock (_dataStore)
        {
            _dataStore.ExecuteTransaction(request.WriteSet);
            return Task.FromResult(new UpdateResponse
            {
                Ok = true
            });
        }
    }
}