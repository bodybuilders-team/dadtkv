namespace Dadtkv;

/// <summary>
///     Utilities for concurrent operations in the Dadtkv project.
/// </summary>
public static class ConcurrentUtils
{
    public static ulong GetAndIncrement(ref ulong location)
    {
        ulong original;
        do
        {
            original = location;
        } while (original != Interlocked.CompareExchange(ref location, original + 1, original));

        return original;
    }

    public static int GetAndIncrement(ref int location)
    {
        int original;
        do
        {
            original = location;
        } while (original != Interlocked.CompareExchange(ref location, original + 1, original));

        return original;
    }
}