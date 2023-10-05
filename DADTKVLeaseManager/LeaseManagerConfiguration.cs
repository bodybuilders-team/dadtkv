namespace DADTKV;

public class LeaseManagerConfiguration : ProcessConfiguration
{
    public LeaseManagerConfiguration(ProcessConfiguration processConfiguration) : base(
        processConfiguration, processConfiguration.ProcessInfo.Id)
    {
    }

    public IEnumerable<ProcessInfo> OtherLeaseManagers
    {
        get { return LeaseManagers.Where(info => info.Id != ProcessInfo.Id).ToList(); }
    }

    //TODO: Check if we should choose minimum id to be leader or a rotating leader based on epoch
    public string? GetLeaderId()
    {
        return LeaseManagers
            .Where(lm => !MyCurrentSuspicions.Contains(lm.Id))
            .Min()?.Id;
    }

    public bool IsLeader() => GetLeaderId() == ProcessInfo.Id;
}