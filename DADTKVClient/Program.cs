using System.Text;
using Microsoft.Extensions.Logging;

namespace Dadtkv;

internal static class Program
{
    /// <summary>
    ///     Entry point for the client application.
    /// </summary>
    /// <param name="args">Arguments: serverUrl clientID scriptFilePath (relative to solution)</param>
    /// <exception cref="ArgumentException">Invalid arguments.</exception>
    public static void Main(string[] args)
    {
        if (args.Length != 3)
            throw new ArgumentException("Invalid arguments. Usage: DadtkvClient.exe serverUrl clientID scriptFilePath");

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        // Client configuration
        var serverUrl = args[0];
        var clientId = args[1];
        DadtkvLogger.InitializeLogger(clientId);
        var logger = DadtkvLogger.Factory.CreateLogger<ClientLogic>();

        var clientLogic = new ClientLogic(clientId, serverUrl);

        // Script configuration
        var scriptFilePath = Path.Combine(Environment.CurrentDirectory, args[2]);
        var scriptReader = new ScriptReader(File.ReadAllText(scriptFilePath));

        logger.LogInformation("Client started");

        try
        {
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

                            logger.LogInformation($"Executing transaction {transactionCommand}");
                            var readSet = clientLogic.TxSubmit(transactionCommand.ReadSet.ToList(), writeSet)
                                .Result;
                            
                            var strBuilder = new StringBuilder();
                            strBuilder.AppendLine("Transaction {transactionCommand} executed successfully");
                            if (readSet.Count == 0)
                            {
                                strBuilder.AppendLine("Read set is empty");
                                logger.LogInformation(strBuilder.ToString());
                                break;
                            }

                            strBuilder.AppendLine("Read set:");
                            foreach (var dadInt in readSet)
                                strBuilder.AppendLine(dadInt.Key + " " + dadInt.Value);
                            logger.LogInformation(strBuilder.ToString());
                            break;

                        case WaitCommand waitCommand:
                            logger.LogInformation($"Waiting {waitCommand.Milliseconds} milliseconds");
                            Thread.Sleep(waitCommand.Milliseconds);
                            break;

                        case StatusCommand:
                            var status = clientLogic.Status().Result;
                            
                            var strBuilder2 = new StringBuilder();
                            strBuilder2.AppendLine("Status:");
                            foreach (var statusEntry in status)
                                strBuilder2.AppendLine(statusEntry);
                            logger.LogInformation(strBuilder2.ToString());
                            break;
                    }
                }
            }
        }
        catch (Exception)
        {
            logger.LogInformation($"Client {clientId} connection with TM ended abruptly.");
        }

        logger.LogInformation($"Client {clientId} stopped");
    }
}