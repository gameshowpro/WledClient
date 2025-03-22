namespace GameshowPro.Wled;

public static class Constants
{
    public const string RemoteServiceName = "WLED";
    public static readonly IMdnsServiceSearchProfile s_mdnsSearchProfile = new ServiceSearchProfile("_wled", "tcp", false);
    public static readonly TimeSpan s_heartbeatPeriod = TimeSpan.FromSeconds(10);
}
