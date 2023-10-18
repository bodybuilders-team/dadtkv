namespace Dadtkv;

/// <summary>
///     Information about a process in the system.
/// </summary>
public interface IProcessInfo
{
    public string Id { get; }
    public string Role { get; }
}