namespace Dadtkv;

/// <summary>
///     Information about a process in the system.
/// </summary>
public interface IProcessInfo
{
    public string Id { get; }
    public string Role { get; }
}

/// <summary>
///     Information about a server process in the system.
/// </summary>
public class ServerProcessInfo : IProcessInfo
{
    public string Id { get; }
    public string Role { get; }
    public string Url { get; }
    public List<TimeSlotStatus> TimeSlotStatusList { get; } = new();

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
    public string Id { get; }
    public string Role { get; }
    public string Script { get; }

    public ClientProcessInfo(string id, string role, string script)
    {
        Id = id;
        Role = role;
        Script = script;
    }
}

public class TimeSlotStatus: IComparable<TimeSlotStatus>
{
    public int TimeSlot { get; set; }
    public string Status { get; set; }
        
    public TimeSlotStatus(int timeSlot, string status)
    {
        TimeSlot = timeSlot;
        Status = status;
    }

    public int CompareTo(TimeSlotStatus? other)
    {
        return TimeSlot.CompareTo(other?.TimeSlot);
    }
}