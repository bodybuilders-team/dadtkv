using DADTKVT;
using Grpc.Core;

namespace DADTKV;

internal static class Program
{
    // Entry point for the server application
    // Arguments: port, hostname, serverId
    public static void Main(string[] args)
    {
        if (args.Length != 3)
            throw new ArgumentException("Invalid arguments.");

        var port = int.Parse(args[0]);
        var hostname = args[1];
        var serverId = args[2];

        var transactionManagersLookup = new Dictionary<string, string>
        {
            { "TM1", "http://localhost:1001" },
            { "TM2", "http://localhost:1002" },
            { "TM3", "http://localhost:1003" }
        };

        transactionManagersLookup.Remove(serverId);

        var server = new Server
        {
            Services =
            {
                DADTKVService.BindService(
                    new DADTKVServiceImpl(transactionManagersLookup, serverId, "TODO") // TODO: Add lease manager URL
                ),
                StateUpdateService.BindService(
                    new StateUpdateServiceImpl(transactionManagersLookup)
                )
            },
            Ports = { new ServerPort(hostname, port, ServerCredentials.Insecure) }
        };

        server.Start();

        Console.WriteLine($"Transaction Manager server listening on port {port}");
        Console.WriteLine("Press Enter to stop the server.");
        Console.ReadLine();

        server.ShutdownAsync().Wait();
    }
}