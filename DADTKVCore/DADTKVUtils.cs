namespace Dadtkv;

/// <summary>
///     Utilities for the Dadtkv project.
/// </summary>
public static class DadtkvUtils
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
                if (!signal) return;
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

    /// <summary>
    ///     Gets the value associated with the specified key, or the default value if the key does not exist.
    /// </summary>
    /// <param name="dict">The dictionary to get the value from.</param>
    /// <param name="key">The key to get the value for.</param>
    /// <param name="defaultValue">The default value to return if the key does not exist.</param>
    /// <typeparam name="TK">The type of the key.</typeparam>
    /// <typeparam name="TV">The type of the value.</typeparam>
    /// <returns>The value associated with the specified key, or the default value if the key does not exist.</returns>
    public static TV? GetValueOrNull<TK, TV>(this IDictionary<TK, TV?> dict, TK key, TV? defaultValue = default)
    {
        return dict.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    ///     Adds an item to the list in sorted order.
    /// </summary>
    /// <param name="list">The list to add the item to.</param>
    /// <param name="item">The item to add.</param>
    /// <typeparam name="T">The type of the list.</typeparam>
    public static void AddSorted<T>(this List<T> list, T item) where T : IComparable<T>
    {
        if (list.Count == 0)
        {
            list.Add(item);
            return;
        }

        if (list[^1].CompareTo(item) <= 0)
        {
            list.Add(item);
            return;
        }

        if (list[0].CompareTo(item) >= 0)
        {
            list.Insert(0, item);
            return;
        }

        var index = list.BinarySearch(item);
        if (index < 0)
            index = ~index;
        list.Insert(index, item);
    }
}