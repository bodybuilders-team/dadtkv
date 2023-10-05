namespace DADTKV;

// Command to wait for a number of milliseconds
internal class WaitCommand : ICommand
{
    public WaitCommand(int milliseconds)
    {
        Milliseconds = milliseconds;
    }

    public int Milliseconds { get; }
}