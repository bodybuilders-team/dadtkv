using Microsoft.Extensions.Logging;

namespace Dadtkv;

internal static class Program
{
    private static readonly ILogger<ClientLogic> Logger = DadtkvLogger.Factory.CreateLogger<ClientLogic>();

    /// <summary>
    ///     Entry point for the client application.
    /// </summary>
    /// <param name="args">Arguments: serverUrl clientID scriptFilePath</param>
    /// <exception cref="ArgumentException">Invalid arguments.</exception>
    public static void Main(string[] args)
    {
        if (args.Length != 3)
            throw new ArgumentException("Invalid arguments. Usage: DadtkvClient.exe serverUrl clientID scriptFilePath");

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        // Client configuration
        var serverUrl = args[0];
        var clientId = args[1];
        var clientLogic = new ClientLogic(clientId, serverUrl);

        // Script configuration
        var scriptFilePath = Path.Combine(Environment.CurrentDirectory, args[2]);
        var scriptReader = new ScriptReader(File.ReadAllText(scriptFilePath));

        Logger.LogInformation("Client started");

        while (scriptReader.HasNextCommand())
        {
            var command = scriptReader.NextCommand();
            lock (clientLogic)
            {
                switch (command)
                {
                    case TransactionCommand transactionCommand:
                        var writeSet = transactionCommand.WriteSet
                            .Select(x => new DadIntDto
                            {
                                Key = x.Key,
                                Value = x.Value
                            }).ToList();

                        Logger.LogInformation($"Executing transaction {transactionCommand}");
                        var readSet = clientLogic.TxSubmit(transactionCommand.ReadSet.ToList(), writeSet)
                            .Result;

                        Logger.LogInformation($"Transaction {transactionCommand} executed successfully");
                        if (readSet.Count == 0)
                        {
                            Logger.LogInformation("No read set");
                            break;
                        }

                        Logger.LogInformation("Read set:");
                        foreach (var dadInt in readSet)
                            Logger.LogInformation(dadInt.Key + " " + dadInt.Value);
                        break;

                    case WaitCommand waitCommand:
                        Logger.LogInformation($"Waiting {waitCommand.Milliseconds} milliseconds");
                        Thread.Sleep(waitCommand.Milliseconds);
                        break;

                    case StatusCommand:
                        var status = clientLogic.Status().Result;
                        Logger.LogInformation("Status:");
                        foreach (var statusEntry in status)
                            Logger.LogInformation(statusEntry);
                        break;
                }
            }
        }
    }
}