﻿using DADTKVT;
using Grpc.Core;

namespace DADTKV;

internal static class Program
{
    // Entry point for the server application
    // Arguments: port, hostname, serverId
    public static void Main(string[] args)
    {
        if (args.Length != 1)
            throw new ArgumentException("Invalid arguments.");

        var serverId = args[0];


        var configurationFile = Path.Combine(Environment.CurrentDirectory, args[2]);
        var systemConfiguration = SystemConfiguration.ReadSystemConfiguration(configurationFile)!;

        var processConfiguration = new ProcessConfiguration(systemConfiguration, serverId);
        var serverProcessPort = new Uri(processConfiguration.ProcessInfo.URL).Port;
        var hostname = new Uri(processConfiguration.ProcessInfo.URL).Host;

        var lockObject = new object();
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        
        var server = new Server
        {
            Services =
            {
                DADTKVService.BindService(
                    new DADTKVServiceImpl(lockObject, processConfiguration) // TODO: Add lease manager URL
                ),
                StateUpdateService.BindService(
                    new StateUpdateServiceImpl(lockObject, processConfiguration)
                )
            },
            Ports = { new ServerPort(hostname, serverProcessPort, ServerCredentials.Insecure) }
        };

        server.Start();

        Console.WriteLine($"Transaction Manager server listening on port {serverProcessPort}");
        Console.WriteLine("Press Enter to stop the server.");
        Console.ReadLine();

        server.ShutdownAsync().Wait();
    }
}