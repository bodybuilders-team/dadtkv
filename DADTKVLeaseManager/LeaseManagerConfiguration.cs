namespace DADTKV;

public class LeaseManagerConfiguration
{
    public readonly ProcessConfiguration ProcessConfiguration;

    public LeaseManagerConfiguration(ProcessConfiguration processConfiguration)
    {
        ProcessConfiguration = processConfiguration;
    }

    //TODO: Check if we should choose minimum id to be leader or a rotating leader based on epoch
    public string GetLeaderId()
    {
        return ProcessConfiguration.SystemConfiguration.LeaseManagers
            .Where(leaseManager =>
                !ProcessConfiguration.MyCurrentSuspicions.Contains(leaseManager.Id)
            )
            .Min()!.Id;
    }
}