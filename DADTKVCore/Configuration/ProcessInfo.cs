namespace Dadtkv;

/// <summary>
///     Information about a process in the system.
/// </summary>
public interface IProcessInfo
{
    public string Id { get; }
    public string Role { get; }

    public Dictionary<int, string> SlotStatus { get; }
}

/// <summary>
///     Information about a server process in the system.
/// </summary>
public class ServerProcessInfo : IProcessInfo
{
    public string Id { get; init; }
    public string Role { get; init; }
    public string Url { get; init; }
    public Dictionary<int, string> SlotStatus { get; } = new();

    public ServerProcessInfo(string id, string role, string url)
    {
        Id = id;
        Role = role;
        Url = url;
    }
}

/// <summary>
///     Information about a client process in the system.
/// </summary>
public class ClientProcessInfo : IProcessInfo
{
    public string Id { get; init; }
    public string Role { get; init; }
    public string Script { get; init; }
    public Dictionary<int, string> SlotStatus { get; } = new();

    public ClientProcessInfo(string id, string role, string script)
    {
        Id = id;
        Role = role;
        Script = script;
    }
}