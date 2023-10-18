using System.Text;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

/// <summary>
///     Utilities for the Dadtkv project.
/// </summary>
public static class DadtkvUtils
{
    private static readonly ILogger _logger = DadtkvLogger.Factory.CreateLogger("DadtkvUtils");

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
    public static async Task<bool> WaitForMajority<TResponse>(
        List<Task<TResponse>> asyncTasks,
        Func<TResponse, bool> predicate,
        int timeout = 10000
    )
    {
        var tasksWithTimeout =
            asyncTasks.Select(task => task.TimeoutAfter(TimeSpan.FromMilliseconds(timeout))).ToList();


        var majority = asyncTasks.Count / 2 + 1;

        var countSatisfyingPredicate = 0;
        var completedTaskCount = 0;

        var remainingTasks = new HashSet<Task<TResponse>>(tasksWithTimeout);

        while (remainingTasks.Count > 0)
        {
            // Wait for the next task to complete
            var completedTask = await Task.WhenAny(remainingTasks);

            remainingTasks.Remove(completedTask);
            completedTaskCount++;

            if (!completedTask.IsFaulted && predicate(completedTask.Result))
            {
                countSatisfyingPredicate++;
            }

            if (countSatisfyingPredicate >= majority)
            {
                return true;
            }

            // If it's impossible to reach the majority given the remaining tasks, return false
            if (completedTaskCount - countSatisfyingPredicate >= majority)
            {
                return false;
            }
        }

        return false;
    }

    public static async Task<bool> WaitForMajority<TResponse>(
        List<Task<TResponse>> asyncTasks
    )
    {
        return await WaitForMajority(asyncTasks, _ => true);
    }

    private static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource();

        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));

        if (completedTask == task)
        {
            timeoutCancellationTokenSource.Cancel();
            return await task; // Very important in order to propagate exceptions
        }
        else
        {
            throw new TimeoutException("The operation has timed out.");
        }
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

    public static string ToStringRep<T>(this IList<T> list)
    {
        var sb = new StringBuilder();
        sb.Append("[");
        // Do the same as above but take into account empty list and list with only one element
        foreach (var ele in list)
        {
            sb.Append(ele);
            sb.Append(", ");
        }

        if (list.Count > 0)
            sb.Length -= 2; // Remove the trailing comma and space

        sb.Append(']');

        return sb.ToString();
    }

    public static string ToStringRep<TK, TV>(this IDictionary<TK, TV> dictionary) where TK : notnull
    {
        var lines = dictionary.Select(kvp => $"{kvp.Key}:{kvp.Value}");
        return "{" + string.Join(",", lines) + "}";
    }

    public static string ToStringRep<TV>(this ISet<TV> set)
    {
        var lines = set.Select(ele => $"{ele}");
        return "{" + string.Join(",", lines) + "}";
    }

    public static string ToStringRep<T>(this Queue<T> queue)
    {
        var sb = new StringBuilder();
        sb.Append("[");

        foreach (var ele in queue)
        {
            sb.Append(ele);
            sb.Append(", ");
        }

        if (queue.Count > 0)
            sb.Length -= 2; // Remove the trailing comma and space

        sb.Append(']');
        return sb.ToString();
    }
}