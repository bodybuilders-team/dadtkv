namespace DADTKV;

public class LeaseManagerConfiguration
{
    public ProcessConfiguration ProcessConfiguration;

    public LeaseManagerConfiguration(ProcessConfiguration processConfiguration)
    {
        this.ProcessConfiguration = processConfiguration;
    }

    public string getLeader()
    {
        return ProcessConfiguration.SystemConfiguration.Suspicions
    }
}