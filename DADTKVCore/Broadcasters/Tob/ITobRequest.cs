namespace Dadtkv;

/// <summary>
///     A request that is sent using the Total Order Broadcast protocol.
/// </summary>
/// <typeparam name="TR">The type of the request.</typeparam>
public interface ITobRequest<in TR> : IUrbRequest<TR>
{
    public ulong TobMessageId { get; }
}