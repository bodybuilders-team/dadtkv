namespace DADTKV;

internal static class Program
{
    /// <summary>
    /// Entry point for the client application.
    /// </summary>
    /// <param name="args">Arguments: serverUrl clientID scriptFilePath</param>
    /// <exception cref="ArgumentException">Invalid arguments.</exception>
    public static void Main(string[] args)
    {
        if (args.Length != 3)
            throw new ArgumentException("Invalid arguments.");

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        // Client configuration
        var serverUrl = args[0];
        var clientId = args[1];
        var clientLogic = new ClientLogic(clientId, serverUrl);

        // Script configuration
        var scriptFilePath = Path.Combine(Environment.CurrentDirectory, args[2]);
        var scriptReader = new ScriptReader(File.ReadAllText(scriptFilePath));

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

                        Console.WriteLine("Read set:");
                        foreach (var dadInt in readSet)
                            Console.WriteLine(dadInt.Key + " " + dadInt.Value);

                        break;

                    case WaitCommand waitCommand:
                        Console.WriteLine("Waiting " + waitCommand.Milliseconds + " milliseconds");
                        Thread.Sleep(waitCommand.Milliseconds);
                        break;

                    case StatusCommand:
                        Console.WriteLine("Status: " + clientLogic.Status().Result);
                        break;

                    default:
                        Console.WriteLine("Unknown command");
                        break;
                }
            }
        }
    }
}