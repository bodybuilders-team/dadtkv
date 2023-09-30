namespace DADTKV;

public class LeaseManagerConfiguration
{
    public readonly ProcessConfiguration ProcessConfiguration;

    public LeaseManagerConfiguration(ProcessConfiguration processConfiguration)
    {
        ProcessConfiguration = processConfiguration;
    }

    public string GetLeaderId()
    {
        return ProcessConfiguration.SystemConfiguration.LeaseManagers
            .Where(leaseManager =>
                ProcessConfiguration.MyCurrentSuspicions.All(suspicion => suspicion.Item2 != leaseManager.Id))
            .Min()
            ?.Id!;
    }
}