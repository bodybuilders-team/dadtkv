namespace Dadtkv;

/// <summary>
///     Configuration of a lease manager process.
/// </summary>
public class LeaseManagerConfiguration : ServerProcessConfiguration
{
    public LeaseManagerConfiguration(ServerProcessConfiguration serverProcessConfiguration) : base(
        serverProcessConfiguration, serverProcessConfiguration.ProcessInfo.Id)
    {
    }

    public IEnumerable<IProcessInfo> OtherLeaseManagers =>
        LeaseManagers.Where(info => !info.Id.Equals(ProcessInfo.Id)).ToList();

    /// <summary>
    ///     The lease manager with the lowest id is the leader.
    /// </summary>
    /// <returns>The id of the leader.</returns>
    private string GetLeaderId()
    {
        return LeaseManagers
                   .Where(lm => !MyCurrentSuspected.Contains(lm.Id) && !RealSuspected.Contains(lm.Id))
                   .MinBy(info => GetLeaseManagerIdNum(info.Id))?.Id
               ?? throw new Exception("No leader found");
    }

    /// <summary>
    ///     Checks if this process is the leader.
    /// </summary>
    /// <returns>True if this process is the leader, false otherwise.</returns>
    public bool IsLeader()
    {
        return GetLeaderId().Equals(ProcessInfo.Id);
    }
}