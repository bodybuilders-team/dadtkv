namespace Dadtkv;

/// <summary>
///     Information about a process in the system.
/// </summary>
public class ProcessInfo
{
    public string Id { get; set; }

    public string Role { get; set; }

    // TODO: Create ServerProcessInfo and ClientProcessInfo to abstract this? Avoid nullability issues
    public string? Url { get; set; } // TODO remove nullable
    public Dictionary<int, string> SlotStatus { get; } = new();
}