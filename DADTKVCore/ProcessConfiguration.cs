namespace DADTKV;

public class ProcessConfiguration
{
    public readonly SystemConfiguration SystemConfiguration;
    public readonly ProcessInfo ProcessInfo;

    public ProcessConfiguration(SystemConfiguration systemConfiguration, string serverId)
    {
        SystemConfiguration = systemConfiguration;
        ProcessInfo = systemConfiguration.Processes.Find((info) => info.Id == serverId)!;
    }

    public IEnumerable<ProcessInfo> ServerProcesses
    {
        get { return SystemConfiguration.ServerProcesses.Where((info => info.Id != ProcessInfo.Id)).ToList(); }
    }

    public IEnumerable<Tuple<string, string>> MyCurrentSuspicions
    {
        get { return SystemConfiguration.CurrentSuspicions.Where((tuple) => tuple.Item1 == ProcessInfo.Id).ToList(); }
    }
}