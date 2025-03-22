namespace GameshowPro.Wled;

public class WledClients(IEnumerable<Settings> settings, IMdnsServiceFinder? serviceFinder, ILoggerFactory loggerFactory, CancellationToken cancellationToken) : NotifyingClass, IRemoteServiceCollection
{
    public ObservableCollection<WledClient> Items { get; } = [.. settings.Select((s, i) => new WledClient(s, serviceFinder, loggerFactory, cancellationToken))];

    event NotifyCollectionChangedEventHandler? IRemoteServiceCollection.RemoteServiceCollectionChanged
    {
        add => Items.CollectionChanged += value;
        remove => Items.CollectionChanged -= value;
    }
    public IEnumerable<IRemoteService> Services => Items.Cast<IRemoteService>();
}
