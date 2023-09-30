namespace DADTKV;

public class ProcessInfo
{
    public string Id { get; set; }
    public string Role { get; set; }
    public string URL { get; set; }
    public Dictionary<int, string> SlotStatus { get; } = new Dictionary<int, string>();
}