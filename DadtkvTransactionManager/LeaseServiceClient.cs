namespace Dadtkv;

public class LeaseServiceClient
{
    public readonly LeaseService.LeaseServiceClient Client;
    public readonly ServerProcessInfo ServerProcessInfo;

    public LeaseServiceClient(LeaseService.LeaseServiceClient client, ServerProcessInfo serverProcessInfo)
    {
        Client = client;
        ServerProcessInfo = serverProcessInfo;
    }
}