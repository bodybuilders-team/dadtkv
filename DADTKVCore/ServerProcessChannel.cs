using Grpc.Net.Client;

namespace DADTKV;

// TODO: Useless class? ProcessInfo is not used anywhere.

/// <summary>
/// Store the gRPC channel and the process info of a server.
/// </summary>
public class ServerProcessChannel
{
    public GrpcChannel GrpcChannel;
    public ProcessInfo ProcessInfo;
}