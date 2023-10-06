namespace DADTKV;

/// <summary>
/// Configuration of a process.
/// </summary>
public class ProcessConfiguration : SystemConfiguration
{
    public readonly ProcessInfo ProcessInfo;

    public ProcessConfiguration(SystemConfiguration systemConfiguration, string serverId) : base(systemConfiguration)
    {
        ProcessInfo = Processes.Find(info => info.Id == serverId)!;
    }

    public List<ProcessInfo> OtherServerProcesses =>
        ServerProcesses.Where(info => info.Id != ProcessInfo.Id).ToList();

    public List<ProcessInfo> OtherTransactionManagers =>
        TransactionManagers.Where(info => info.Id != ProcessInfo.Id).ToList();

    protected List<string> MyCurrentSuspicions => CurrentSuspicions
        .Where(tuple => tuple.Item1 == ProcessInfo.Id)
        .Select(tuple => tuple.Item2).ToList();
}