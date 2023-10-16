namespace Dadtkv;

public class LeaseQueues : Dictionary<string, Queue<LeaseId>>
{
    /// <summary>
    ///     Checks if the leases of a request are on the top of the queue.
    /// </summary>
    /// <param name="consensusStateValue">The consensus value.</param>
    /// <param name="leaseReq">The lease request.</param>
    /// <returns>True if the leases are on the top of the queue, false otherwise.</returns>
    public bool ObtainedLeases(List<string> set, LeaseId leaseId)
    {
        foreach (var key in set)
            if (!ContainsKey(key) ||
                this[key].Count == 0 ||
                !this[key].Peek().Equals(leaseId)
               )
                return false;

        return true;
    }

    /// <summary>
    ///     Checks if the leases of a request are on the top of the queue.
    /// </summary>
    /// <param name="leaseReq">The lease request.</param>
    /// <returns>True if the leases are on the top of the queue, false otherwise.</returns>
    public bool ObtainedLeases(LeaseRequest leaseReq)
    {
        return ObtainedLeases(leaseReq.Set, leaseReq.LeaseId);
    }

    public void FreeLeases(LeaseId leaseId)
    {
        foreach (var (key, queue) in this)
            if (queue.Count > 0 && queue.Peek().Equals(leaseId))
                queue.Dequeue();
    }
}