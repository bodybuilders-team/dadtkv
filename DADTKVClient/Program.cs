namespace DADTKVClient
{
    internal class Program
    {
        public static void Main()
        {
            // Transaction Managers configuration -> TODO: change later for multiple TM
            const int serverPort = 1001;
            const string serverHostname = "localhost";

            // Client configuration
            const string clientID = "client1"; // TODO: change later for multiple clients
            ClientLogic clientLogic = new ClientLogic(clientID, serverHostname, serverPort);

            // Script configuration
            var path = Path.Combine(Environment.CurrentDirectory, "/IST/Courses/1st Semester/PADI/ist-meic-dad-g05/DADTKVClient/scripts/DADTKV_client_script_sample.txt");
            ScriptReader scriptReader = new ScriptReader(File.ReadAllText(path));

            while (scriptReader.HasNextCommand())
            {
                Command command = scriptReader.NextCommand();

                switch (command)
                {
                    case TransactionCommand:
                        Console.WriteLine("Transaction command");
                        Console.WriteLine("Read set: ");
                        foreach (string read in ((TransactionCommand)command).ReadSet)
                        {
                            Console.WriteLine(read);
                        }
                        Console.WriteLine("Write set: ");
                        foreach (KeyValuePair<string, int> entry in ((TransactionCommand)command).WriteSet)
                        {
                            Console.WriteLine(entry.Key + " " + entry.Value);
                        }
                        break;
                    case WaitCommand:
                        Console.WriteLine("Wait command");
                        Console.WriteLine("Milliseconds: " + ((WaitCommand)command).Milliseconds);
                        break;
                }

                lock (clientLogic)
                {
                    switch (command)
                    {
                        case TransactionCommand:
                            List<DadInt> writeSet = ((TransactionCommand)command).WriteSet 
                                .Select(x => new DadInt
                                {
                                    Key = x.Key,
                                    Value = x.Value
                                }).ToList(); 

                            clientLogic.TxSubmit(((TransactionCommand)command).ReadSet.ToList(), writeSet);
                            break;
                        case WaitCommand:
                            Thread.Sleep(((WaitCommand)command).Milliseconds);
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