namespace DADTKV;

/// <summary>
///     Command to wait for a number of milliseconds.
/// </summary>
internal class WaitCommand : ICommand
{
    public WaitCommand(int milliseconds)
    {
        Milliseconds = milliseconds;
    }

    public int Milliseconds { get; }
}