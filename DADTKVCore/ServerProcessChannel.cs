using Grpc.Net.Client;

namespace DADTKV;

public class ServerProcessChannel
{
    public GrpcChannel GrpcChannel;
    public ProcessInfo ProcessInfo;
}