using Grpc.Core;

namespace DADTKV;

public static class DADTKVUtils
{
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

    public static void WaitForMajority<TResponse>(
        List<AsyncUnaryCall<TResponse>> asyncTasks,
        Func<TResponse, CountdownEvent, Task> responseHandler
    )
    {
        // Majority is defined as n/2 + 1
        var cde = new CountdownEvent(asyncTasks.Count / 2 + 1);

        asyncTasks.ForEach(asyncTask =>
        {
            var thread = new Thread(() =>
            {
                asyncTask.ResponseAsync.Wait();
                var res = asyncTask.ResponseAsync.Result;
                responseHandler.Invoke(res, cde);
            });
            thread.Start();
        });

        cde.Wait();
    }
}