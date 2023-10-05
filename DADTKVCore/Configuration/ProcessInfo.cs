namespace DADTKV;

public class ProcessInfo
{
    public string Id { get; set; }
    public string Role { get; set; }
    // TODO: Create ServerProcessInfo and ClientProcessInfo to abstract this? Avoid nullability issues
    public string? Url { get; set; } 
    public Dictionary<int, string> SlotStatus { get; } = new();
}