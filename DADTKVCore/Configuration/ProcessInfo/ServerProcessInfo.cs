namespace Dadtkv;

/// <summary>
///     Information about a server process in the system.
/// </summary>
public class ServerProcessInfo : IProcessInfo
{
    public ServerProcessInfo(string id, string role, string url)
    {
        Id = id;
        Role = role;
        Url = url;
    }

    public string Url { get; }
    public List<TimeSlotStatus> TimeSlotStatusList { get; } = new();
    public string Id { get; }
    public string Role { get; }
}