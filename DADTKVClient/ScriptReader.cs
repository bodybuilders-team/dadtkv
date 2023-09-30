namespace DADTKV;

internal class ScriptReader
{
    private readonly string[] _lines;
    private int _currentLine;

    public ScriptReader(string script)
    {
        _lines = script.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        _currentLine = 0;
    }

    // Returns the next command in the script
    // Returns null if there is no more command
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
                default:
                    throw new Exception("Invalid command");
            }
        }
    }

    // Returns true if there is more command in the script
    public bool HasNextCommand()
    {
        return _currentLine < _lines.Length;
    }
}