using Grpc.Net.Client;

namespace DADTKV;

public class ProcessConfiguration
{
    public readonly SystemConfiguration SystemConfiguration;
    public readonly ProcessInfo ProcessInfo;

    public IEnumerable<ProcessInfo> ServerProcesses
    {
        get { return SystemConfiguration.ServerProcesses.Where((info => info.Id != ProcessInfo.Id)).ToList(); }
    }
    
    public 
    public ProcessConfiguration(SystemConfiguration systemConfiguration, string serverId)
    {
        this.SystemConfiguration = systemConfiguration;
        this.ProcessInfo = systemConfiguration.Processes.Find((info) => info.Id == serverId)!;
    }
  
}