namespace DADTKV;

public class ProcessConfiguration : SystemConfiguration
{
    public readonly ProcessInfo ProcessInfo;

    public ProcessConfiguration(SystemConfiguration systemConfiguration, string serverId): base(systemConfiguration)
    {
        ProcessInfo = this.Processes.Find((info) => info.Id == serverId)!;
    }

    public List<ProcessInfo> OtherServerProcesses =>
        this.ServerProcesses.Where((info => info.Id != ProcessInfo.Id)).ToList();

    public List<ProcessInfo> OtherTransactionManagers =>
        this.TransactionManagers.Where((info => info.Id != ProcessInfo.Id)).ToList();

    public List<string> MyCurrentSuspicions
    {
        get
        {
            return this.CurrentSuspicions
                .Where((tuple) => tuple.Item1 == ProcessInfo.Id)
                .Select((tuple) => tuple.Item2).ToList();
        }
    }
}