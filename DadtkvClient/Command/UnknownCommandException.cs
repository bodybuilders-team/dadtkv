namespace Dadtkv;

/// <summary>
///     Exception thrown when an unknown command is found in a script.
/// </summary>
internal class UnknownCommandException : Exception
{
    public UnknownCommandException(string message) : base(message)
    {
    }
}