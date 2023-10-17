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

    protected List<string> MyCurrentSuspicions => CurrentSuspicions
        .Where(tuple => tuple.Item1.Equals(ProcessInfo.Id))
        .Select(tuple => tuple.Item2).ToList();

    public ulong ServerId
    {
        get
        {
            var index = Processes.FindIndex(p => p.Id.Equals(ProcessInfo.Id));

            if (index < 0)
                throw new Exception("Server not found");

            return (ulong)index;
        }
    }
}