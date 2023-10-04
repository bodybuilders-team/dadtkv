namespace DADTKV;

public class LeaseManagerConfiguration : ProcessConfiguration
{
    public LeaseManagerConfiguration(ProcessConfiguration processConfiguration) : base(
        processConfiguration, processConfiguration.ProcessInfo.Id)
    {
    }

    public IEnumerable<ProcessInfo> OtherLeaseManagers
    {
        get { return LeaseManagers.Where((info => info.Id != ProcessInfo.Id)).ToList(); }
    }

    //TODO: Check if we should choose minimum id to be leader or a rotating leader based on epoch
    public string GetLeaderId(ulong proposalNumber)
    {
        return LeaseManagers[(int)(proposalNumber % (ulong)LeaseManagers.Count) - 1].Id;
    }
}