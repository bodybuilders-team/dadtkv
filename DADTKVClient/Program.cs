// ReSharper disable once CheckNamespace

namespace DADTKV
{
    internal class Program
    {
        // Entry point for the client application
        // Arguments: serverPort serverHostname clientID scriptPath
        public static void Main(string[] args)
        {
            // Transaction Managers configuration
            const int serverPort = 1001; // args[0];
            const string serverHostname = "localhost"; // args[1];

            // Client configuration
            const string clientId = "client1"; // args[2];
            var clientLogic = new ClientLogic(clientId, serverHostname, serverPort);

            // Script configuration
            var path = Path.Combine(Environment.CurrentDirectory,
                "/IST/Courses/1st Semester/PADI/ist-meic-dad-g05/DADTKVClient/scripts/DADTKV_client_script_sample.txt"); // args[3];
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
}