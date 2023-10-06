namespace DADTKV;

/// <summary>
///     Information about a process in the system.
/// </summary>
public class ProcessInfo
{
    public string Id { get; set; }
    public string Role { get; set; }
    public Dictionary<int, string> SlotStatus { get; } = new();
}

/// <summary>
///     Information about a server process in the system.
/// </summary>
public class ServerProcessInfo : ProcessInfo
{
    public string Url { get; set; }
}

/// <summary>
///     Information about a client process in the system.
/// </summary>
public class ClientProcessInfo : ProcessInfo
{
}