namespace GameshowPro.Wled;

public class Settings(string? host, string? name, RemoteServiceSettings? remoteServiceSettings) : ObservableClass
{
    public Settings() : this(null, null, null) { }

    [DataMember]
    public string Name { get; set; } = name ?? "No name";

    private string _host = host ?? string.Empty;
    [DataMember]
    public string Host
    {
        get { return _host; }
        set { SetProperty(ref _host, value); }
    }

    [DataMember]
    public RemoteServiceSettings RemoteServiceSettings { get; } = remoteServiceSettings ?? new();
}