namespace DADTKV
{
    // Class to read the script file for a client
    // A script file has the following format:
    // 1. Each line is a command
    // 2. Each line starting with # is a comment
    // 3. a T command includes a read set (list of string keys) and a
    // write set (keys and values); e.g.: T ("a-key-name","another-key-name") (<"name1",10>,<"name2",20>)
    // 4. a W command is used to wait for a number of milliseconds; e.g.: W 1000

    internal interface ICommand
    {
    }

    // Command to execute a transaction
    // A transaction has a read set and a write set
    internal class TransactionCommand : ICommand
    {
        public List<string> ReadSet { get; }
        public Dictionary<string, int> WriteSet { get; }

        public TransactionCommand(List<string> readSet, Dictionary<string, int> writeSet)
        {
            ReadSet = readSet;
            WriteSet = writeSet;
        }
    }

    // Command to wait for a number of milliseconds
    internal class WaitCommand : ICommand
    {
        public int Milliseconds { get; }

        public WaitCommand(int milliseconds)
        {
            Milliseconds = milliseconds;
        }
    }


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
                {
                    return null;
                }

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
}