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
        

        logger.LogInformation("Client {clientId} started", clientId);

        try
        {
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
                                .Select(x => new DadIntDto
                                {
                                    Key = x.Key,
                                    Value = x.Value
                                }).ToList();

                            logger.LogInformation("Executing transaction: {transactionCommand}",
                                transactionCommand.ToString());
                            var readSet = clientLogic.TxSubmit(transactionCommand.ReadSet.ToList(), writeSet)
                                .Result;

                            var strBuilder = new StringBuilder();
                            strBuilder.Append("Transaction successfully executed. Read set: {");
                            foreach (var dadInt in readSet)
                                strBuilder.Append($"{dadInt.Key}:{dadInt.Value},");
                            if (readSet.Count > 0)
                                strBuilder.Remove(strBuilder.Length - 1, 1);
                            strBuilder.Append('}');

                            logger.LogInformation(strBuilder.ToString());
                            break;

                        case WaitCommand waitCommand:
                            logger.LogInformation("Waiting {time} milliseconds", waitCommand.Milliseconds);
                            Thread.Sleep(waitCommand.Milliseconds);
                            break;

                        case StatusCommand:
                            var status = clientLogic.Status().Result;

                            var strBuilder2 = new StringBuilder();
                            strBuilder2.Append("Status: ");
                            foreach (var statusEntry in status)
                                strBuilder2.Append(statusEntry);
                            logger.LogInformation(strBuilder2.ToString());
                            break;
                    }
                }
            }
        }
        catch (Exception)
        {
            logger.LogInformation("Client {clientId} connection with TM ended abruptly.", clientId);
        }

        logger.LogInformation("Client {clientId} stopped", clientId);
    }
}