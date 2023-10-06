namespace DADTKV;

/// <summary>
///     Utilities for the DADTKV project.
/// </summary>
public static class DADTKVUtils
{
    private const int DefaultTimeout = 1000;

    /// <summary>
    ///     Selects a random element from the list.
    /// </summary>
    /// <param name="list">The list to select from.</param>
    /// <typeparam name="T">The type of the list.</typeparam>
    /// <returns>A random element from the list.</returns>
    public static T Random<T>(this List<T> list)
    {
        var random = new Random();
        var randomIdx = random.Next(list.Count);

        return list[randomIdx];
    }

    /// <summary>
    ///     Invokes an action for each element in the IEnumerable.
    /// </summary>
    /// <param name="iEnumerable">The IEnumerable to iterate over.</param>
    /// <param name="action">The action to invoke.</param>
    /// <typeparam name="T">The type of the IEnumerable.</typeparam>
    /// <returns>The IEnumerable.</returns>
    public static void ForEach<T>(this IEnumerable<T> iEnumerable, Action<T> action)
    {
        foreach (var element in iEnumerable)
            action.Invoke(element);
    }

    /// <summary>
    ///     Waits for a majority of the async tasks to complete.
    /// </summary>
    /// <param name="asyncTasks">The async tasks to wait for.</param>
    /// <param name="predicate">The predicate to check for.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <returns>True if a majority of the async tasks completed, false otherwise.</returns>
    public static bool WaitForMajority<TResponse>(
        List<Task<TResponse>> asyncTasks,
        Func<TResponse, bool> predicate,
        int timeout = DefaultTimeout
    )
    {
        // Majority is defined as n/2 + 1
        var cde = new CountdownEvent(asyncTasks.Count / 2 + 1);

        asyncTasks.ForEach(asyncTask =>
        {
            new Thread(() =>
            {
                asyncTask.Wait();
                var res = asyncTask.Result;
                var signal = predicate.Invoke(res);
                if (signal)
                    lock (cde)
                    {
                        if (!cde.IsSet)
                            cde.Signal();
                    }
            }).Start();
        });

        // TODO: use the failure detector for each 
        return cde.Wait(timeout);
    }

    public static TV GetValueOrNull<TK, TV>(this IDictionary<TK, TV> dict, TK key, TV defaultValue = default)
    {
        TV value;
        return dict.TryGetValue(key, out value) ? value : defaultValue;
    }
}