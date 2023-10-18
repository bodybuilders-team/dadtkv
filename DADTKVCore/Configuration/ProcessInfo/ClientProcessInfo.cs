namespace Dadtkv;

/// <summary>
///     Information about a client process in the system.
/// </summary>
public class ClientProcessInfo : IProcessInfo
{
    public ClientProcessInfo(string id, string role, string script)
    {
        Id = id;
        Role = role;
        Script = script;
    }

    public string Script { get; }
    public string Id { get; }
    public string Role { get; }
}