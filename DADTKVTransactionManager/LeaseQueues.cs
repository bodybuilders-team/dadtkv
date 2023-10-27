namespace Dadtkv;

public class LeaseQueues : Dictionary<string, Queue<LeaseId>>
{
    /// <summary>
    ///     Checks if the leases of a request are on the top of the queue.
    /// </summary>
    /// <param name="set">The set of keys.</param>
    /// <param name="leaseId">The lease id.</param>
    /// <returns>True if the leases are on the top of the queue, false otherwise.</returns>
    public bool ObtainedLeases(List<string> set, LeaseId leaseId)
    {
        foreach (var key in set)
            if (!ContainsKey(key) || this[key].Count == 0 || !this[key].Peek().Equals(leaseId))
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
        return ObtainedLeases(leaseReq.Keys, leaseReq.LeaseId);
    }

    /// <summary>
    ///     Checks if the leases of a request are on the top of the queue.
    ///     If so, removes the leases from the queue (only from the top).
    /// </summary>
    /// <param name="leaseId">The lease id.</param>
    public void FreeLeases(LeaseId leaseId)
    {
        foreach (var (_, queue) in this)
            if (queue.Count > 0 && queue.Peek().Equals(leaseId))
                queue.Dequeue();
    }

    /// <summary>
    ///     Checks if the leases of a request exist on a queue.
    ///     If so, removes the leases from the queue (from the middle if needed).
    /// </summary>
    /// <param name="leaseId">The lease id.</param>
    public void ForceFreeLeases(LeaseId leaseId)
    {
        foreach (var (_, queue) in this)
        {
            var tempQueue = new Queue<LeaseId>();

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                if (!item.Equals(leaseId)) tempQueue.Enqueue(item);
            }

            // Restore the items back to the original queue
            while (tempQueue.Count > 0) queue.Enqueue(tempQueue.Dequeue());
        }
    }

    public override string ToString()
    {
        var lines = this.Select(kvp => $"{kvp.Key}:{kvp.Value.ToStringRep()}");
        return "{" + string.Join(",", lines) + "}";
    }
}