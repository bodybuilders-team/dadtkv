namespace DADTKV;

/// <summary>
///     Configuration of a server process.
/// </summary>
public class ServerProcessConfiguration : SystemConfiguration
{
    public readonly ServerProcessInfo ProcessInfo;

    public ServerProcessConfiguration(SystemConfiguration systemConfiguration, string serverId) : base(
        systemConfiguration)
    {
        ProcessInfo = ServerProcesses.Find(info => info.Id == serverId)!;
    }

    public List<ServerProcessInfo> OtherServerProcesses =>
        ServerProcesses.Where(info => info.Id != ProcessInfo.Id).ToList();

    public List<ServerProcessInfo> OtherTransactionManagers =>
        TransactionManagers.Where(info => info.Id != ProcessInfo.Id).ToList();

    protected List<string> MyCurrentSuspicions => CurrentSuspicions
        .Where(tuple => tuple.Item1 == ProcessInfo.Id)
        .Select(tuple => tuple.Item2).ToList();
}