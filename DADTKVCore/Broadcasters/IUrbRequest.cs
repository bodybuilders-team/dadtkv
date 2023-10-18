namespace Dadtkv;

/// <summary>
///     Interface for a request that can be sent through the Urb protocol.
/// </summary>
/// <typeparam name="TR">The type of the request.</typeparam>
public interface IUrbRequest<in TR>
{
    public ulong SenderId { get; }
    public ulong SequenceNum { get; set; }
}