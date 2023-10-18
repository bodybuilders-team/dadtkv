namespace Dadtkv;

/// <summary>
///     Configuration of a server process.
/// </summary>
public class ServerProcessConfiguration : SystemConfiguration
{
    public readonly ServerProcessInfo ProcessInfo;

    public ServerProcessConfiguration(SystemConfiguration systemConfiguration, string serverId) : base(
        systemConfiguration)
    {
        ProcessInfo = ServerProcesses.Find(info => info.Id.Equals(serverId))!;
    }

    public List<ServerProcessInfo> OtherServerProcesses =>
        ServerProcesses.Where(info => !info.Id.Equals(ProcessInfo.Id)).ToList();

    public List<ServerProcessInfo> OtherTransactionManagers =>
        TransactionManagers.Where(info => !info.Id.Equals(ProcessInfo.Id)).ToList();

    public List<string> MyCurrentSuspected => CurrentSuspicions
        .Where(suspicion => suspicion.Suspect.Equals(ProcessInfo.Id))
        .Select(suspicion => suspicion.Suspected).ToList();

    public ulong ServerId
    {
        get
        {
            var index = FindServerProcessIndex(ProcessInfo.Id);

            if (index < 0)
                throw new Exception("Server not found");

            return (ulong)index;
        }
    }
}