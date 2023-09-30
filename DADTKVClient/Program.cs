namespace DADTKV;

internal static class Program
{
    // Entry point for the client application
    // Arguments: serverPort serverHostname clientID scriptPath
    public static void Main(string[] args)
    {
        if (args.Length != 4)
            throw new ArgumentException("Invalid arguments.");
        
        // Transaction Managers configuration
        var serverPort = int.Parse(args[0]);
        var serverHostname = args[1];

        // Client configuration
        var clientId = args[2];
        var clientLogic = new ClientLogic(clientId, serverHostname, serverPort);

        // Script configuration
        var path = Path.Combine(Environment.CurrentDirectory, args[3]);
        var scriptReader = new ScriptReader(File.ReadAllText(path));

        while (scriptReader.HasNextCommand())
        {
            var command = scriptReader.NextCommand();

            lock (clientLogic)
            {
                switch (command)
                {
                    case TransactionCommand transactionCommand:
                        var writeSet = transactionCommand.WriteSet
                            .Select(x => new DadInt
                            {
                                Key = x.Key,
                                Value = x.Value
                            }).ToList();

                        var readSet = clientLogic.TxSubmit(transactionCommand.ReadSet.ToList(), writeSet)
                            .Result;
                        Console.WriteLine("Read set: " + readSet);
                        break;
                    case WaitCommand waitCommand:
                        Console.WriteLine("Waiting " + waitCommand.Milliseconds + " milliseconds");
                        Thread.Sleep(waitCommand.Milliseconds);
                        break;
                    default:
                        Console.WriteLine("Unknown command");
                        break;
                }
            }
        }
    }
}