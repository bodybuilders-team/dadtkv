namespace Dadtkv;

/// <summary>
///     A class that reads a script and has methods to obtain the next command in the script.
/// </summary>
internal class ScriptReader
{
    private readonly string[] _lines;
    private int _currentLine;

    public ScriptReader(string script)
    {
        _lines = script.Split("\n");
        _currentLine = 0;
    }

    /// <summary>
    ///     Obtains the next command in the script.
    /// </summary>
    /// <returns>The next command in the script, or null if there is no more command.</returns>
    /// <exception cref="Exception">If the command is not recognized.</exception>
    public ICommand? NextCommand()
    {
        while (true)
        {
            if (!HasNextCommand())
                return null;

            var line = _lines[_currentLine++];
            var args = line.Split(' ');
            switch (args[0])
            {
                case "#":
                    continue;
                case "T":
                    var readSet = args[1]
                        .Split(new[] { "(", ")" }, StringSplitOptions.None)[1]
                        .Split(',')
                        .Where(data => !data.Equals(""))
                        .Select(x => x.Trim('"'))
                        .ToList();

                    var writeSet = args[2]
                        .Split(new[] { "(", ")" }, StringSplitOptions.None)[1]
                        .Split(">,")
                        .Select(x => x.Trim('<', '>').Split(','))
                        .ToDictionary(x => x[0].Trim('"'), x => int.Parse(x[1]));

                    return new TransactionCommand(readSet, writeSet);
                case "W":
                    return new WaitCommand(int.Parse(args[1]));
                case "S":
                    return new StatusCommand();
                default:
                    throw new UnknownCommandException($"Unknown command: {line}");
            }
        }
    }

    /// <summary>
    ///     Checks if there is more command in the script.
    /// </summary>
    /// <returns>True if there is more command, false otherwise.</returns>
    public bool HasNextCommand()
    {
        return _currentLine < _lines.Length;
    }
}