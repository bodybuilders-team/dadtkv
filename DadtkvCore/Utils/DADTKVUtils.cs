using System.Text;

namespace Dadtkv;

/// <summary>
///     Utilities for the Dadtkv project.
/// </summary>
public static class DadtkvUtils
{
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
    ///     Waits for a majority of the async tasks to complete, checking for a predicate.
    /// </summary>
    /// <param name="asyncTasks">The async tasks to wait for.</param>
    /// <param name="predicate">The predicate to check for.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <param name="onTimeout">The action to invoke on timeout.</param>
    /// <param name="onSuccess">The action to invoke on success.</param>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <returns>True if a majority of the async tasks completed, false otherwise.</returns>
    public static async Task<bool> WaitForMajority<TRequest, TResponse>(
        List<TaskWithRequest<TRequest, TResponse>> asyncTasks,
        bool countSelf,
        Func<TResponse, bool> predicate,
        int timeout = 1000,
        Action<TRequest>? onTimeout = null,
        Action<TRequest>? onSuccess = null
    )
    {
        var tasksWithTimeout = asyncTasks
            .Select(task => task.TimeoutAfter(timeout, onTimeout, onSuccess))
            .ToList();

        var majority = asyncTasks.Count / 2 + 1;

        var countSatisfyingPredicate = 0;
        var completedTaskCount = 0;

        var remainingTasks = new HashSet<Task<TResponse>>(tasksWithTimeout);

        if (countSelf)
            countSatisfyingPredicate++;

        while (remainingTasks.Count > 0)
        {
            // Wait for the next task to complete
            var completedTask = await Task.WhenAny(remainingTasks);

            remainingTasks.Remove(completedTask);
            completedTaskCount++;

            if (!completedTask.IsFaulted && predicate(completedTask.Result))
                countSatisfyingPredicate++;

            if (countSatisfyingPredicate >= majority)
                return true;

            // If it's impossible to reach the majority given the remaining tasks, return false
            if (completedTaskCount - countSatisfyingPredicate >= majority) return false;
        }

        return false;
    }

    /// <summary>
    ///     Waits for a majority of the async tasks to complete.
    /// </summary>
    /// <param name="asyncTasks">The async tasks to wait for.</param>
    /// <returns>True if a majority of the async tasks completed, false otherwise.</returns>
    public static async Task<bool> WaitForMajority<TRequest, TResponse>(
        List<TaskWithRequest<TRequest, TResponse>> asyncTasks, int timeout = 1000)
    {
        return await WaitForMajority(asyncTasks, false, _ => true, timeout);
    }

    /// <summary>
    ///     Times out a task after a specified timeout.
    /// </summary>
    /// <param name="task">The task to time out.</param>
    /// <param name="timeout">The timeout.</param>
    /// <param name="onTimeout">The action to invoke on timeout.</param>
    /// <param name="onSuccess">The action to invoke on success.</param>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <returns>The task.</returns>
    private static async Task<TResult> TimeoutAfter<TRequest, TResult>(
        this TaskWithRequest<TRequest, TResult> task,
        int timeout,
        Action<TRequest>? onTimeout = null,
        Action<TRequest>? onSuccess = null
    )
    {
        if (timeout <= 0)
        {
            var result2 = await task.Task;
            if (!task.Task.IsFaulted)
                onSuccess?.Invoke(task.Request);

            return result2;
        }

        using var timeoutCancellationTokenSource = new CancellationTokenSource();

        var completedTask = await Task.WhenAny(task.Task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));

        if (completedTask != task.Task)
        {
            onTimeout?.Invoke(task.Request);
            throw new TimeoutException("The operation has timed out.");
        }

        timeoutCancellationTokenSource.Cancel();

        var result = await task.Task;
        if (!task.Task.IsFaulted)
            onSuccess?.Invoke(task.Request);

        return result; // Very important in order to propagate exceptions
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

    /// <summary>
    ///     Gets the string representation of the list.
    /// </summary>
    /// <param name="list">The list to get the string representation of.</param>
    /// <typeparam name="T">The type of the list.</typeparam>
    /// <returns>The string representation of the list.</returns>
    public static string ToStringRep<T>(this IList<T> list)
    {
        var sb = new StringBuilder();
        sb.Append('[');
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

    /// <summary>
    ///     Gets the string representation of the dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary to get the string representation of.</param>
    /// <typeparam name="TK">The type of the key.</typeparam>
    /// <typeparam name="TV">The type of the value.</typeparam>
    /// <returns>The string representation of the dictionary.</returns>
    public static string ToStringRep<TK, TV>(this IDictionary<TK, TV> dictionary) where TK : notnull
    {
        var lines = dictionary.Select(kvp => $"{kvp.Key}:{kvp.Value}");
        return "{" + string.Join(",", lines) + "}";
    }

    /// <summary>
    ///     Gets the string representation of the set.
    /// </summary>
    /// <param name="set">The set to get the string representation of.</param>
    /// <typeparam name="TV">The type of the set.</typeparam>
    /// <returns>The string representation of the set.</returns>
    public static string ToStringRep<TV>(this ISet<TV> set)
    {
        var lines = set.Select(ele => $"{ele}");
        return "{" + string.Join(",", lines) + "}";
    }

    /// <summary>
    ///     Gets the string representation of the queue.
    /// </summary>
    /// <param name="queue">The queue to get the string representation of.</param>
    /// <typeparam name="T">The type of the queue.</typeparam>
    /// <returns>The string representation of the queue.</returns>
    public static string ToStringRep<T>(this Queue<T> queue)
    {
        var sb = new StringBuilder();
        sb.Append('[');

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

    public class TaskWithRequest<TRequest, TResponse>
    {
        public TaskWithRequest(Task<TResponse> task, TRequest req)
        {
            Task = task;
            Request = req;
        }

        public Task<TResponse> Task { get; }
        public TRequest Request { get; }
    }
}