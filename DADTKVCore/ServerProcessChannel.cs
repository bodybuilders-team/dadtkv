using Grpc.Net.Client;

namespace DADTKV;

public class ServerProcessChannel
{
    public ProcessInfo ProcessInfo;
    public GrpcChannel GrpcChannel;
}