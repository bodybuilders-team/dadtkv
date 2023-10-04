using Grpc.Core;

namespace DADTKV;

public static class DADTKVUtils
{
    private const int DefaultTimeout = 1000;

    public static T Random<T>(this List<T> list)
    {
        var random = new Random();
        var randomIdx = random.Next(list.Count);

        return list[randomIdx];
    }

    public static void ForEach<T>(this IEnumerable<T> iEnumerable, Action<T> action)
    {
        foreach (var element in iEnumerable)
        {
            action.Invoke(element);
        }
    }

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
            var thread = new Thread(() =>
            {
                asyncTask.Wait();
                var res = asyncTask.Result;
                var signal = predicate.Invoke(res);
                if (signal)
                    cde.Signal();
            });
            thread.Start();
        });

        // TODO: use the failure detector for each 
        return cde.Wait(timeout);
    }
}