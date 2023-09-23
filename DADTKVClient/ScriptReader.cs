namespace DADTKVClient
{
    // Class to read the script file for a client
    // A script file has the following format:
    // 1. Each line is a command
    // 2. Each line starting with # is a comment
    // 3. a T command includes a read set (list of string keys) and a
    // write set (keys and values); e.g.: T ("a-key-name","another-key-name") (<"name1",10>,<"name2",20>)
    // 4. a W command is used to wait for a number of milliseconds; e.g.: W 1000

    internal interface Command
    {
    }

    internal class TransactionCommand : Command
    {
        private List<string> readSet;
        public List<string> ReadSet { get => readSet; }
        
        private Dictionary<string, int> writeSet;
        public Dictionary<string, int> WriteSet { get => writeSet; }

        public TransactionCommand(List<string> readSet, Dictionary<string, int> writeSet)
        {
            this.readSet = readSet;
            this.writeSet = writeSet;
        }
    }

    internal class WaitCommand : Command
    {
        private int milliseconds;
        public int Milliseconds { get => milliseconds; set => milliseconds = value; }

        public WaitCommand(int milliseconds)
        {
            this.milliseconds = milliseconds;
        }
    }

    
    internal class ScriptReader
    {
        private string[] lines;
        private int currentLine;

        public ScriptReader(string script)
        {
            lines = script.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            currentLine = 0;
        }

        // Returns the next command in the script
        // Returns null if there is no more command
        public Command NextCommand()
        {
            if (!HasNextCommand())
            {
                return null;
            }

            string line = lines[currentLine++];
            string[] args = line.Split(' ');
            switch (args[0])
            {
                case "#":
                    return NextCommand();
                case "T":
                    List<string> readSet = args[1].Split(new string[] { "(", ")" }, StringSplitOptions.None)[1].Split(',').Select(x => x.Trim('"')).ToList();

                    Dictionary<string, int> writeSet = args[2].Split(new string[] { "(", ")" }, StringSplitOptions.None)[1].Split(">,")
                        .Select(x => x.Trim('<', '>').Split(','))
                        .ToDictionary(x => x[0].Trim('"'), x => int.Parse(x[1]));

                    return new TransactionCommand(readSet, writeSet);                  
                case "W":
                    return new WaitCommand(int.Parse(args[1]));
                default:
                    throw new Exception("Invalid command");
            };
        }

        // Returns true if there is more command in the script
        public bool HasNextCommand()
        {
            return currentLine < lines.Length;
        }
    }
}
