namespace DADTKV;

// Command to wait for a number of milliseconds
internal class WaitCommand : ICommand
{
    public int Milliseconds { get; }

    public WaitCommand(int milliseconds)
    {
        Milliseconds = milliseconds;
    }
}