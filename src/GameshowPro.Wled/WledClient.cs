using System.Collections.Concurrent;
using GameshowPro.Common;
using WLED_SDK.Core.Models.WledInfo;
using WLED_SDK.Core.Models.WledState;
using WLED_SDK.Core.WledEventArgs;

namespace GameshowPro.Wled;

public class WledClient : ObservableClass, IRemoteService
{
    private record ConnectionRecord(WledWebsocketClient Connection, string Host);

    private readonly PropertyChangeFilters _changeFilters = new(nameof(WledClient));
    private readonly ILogger _logger;
    private readonly ILogger<WledWebsocketClient> _clientLogger;
    private readonly AutoResetEvent _reconnect = new(false);
    private readonly AutoResetEvent _loadPreset = new(false);
    private readonly WaitHandle _cancelling;
    private readonly CancellationToken _cancellationToken;
    private readonly ConcurrentQueue<int> _loadPresetRequestQueue = [];

    public WledClient(Settings settings, IMdnsServiceFinder? serviceFinder, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        Settings = settings;
        if (serviceFinder != null)
        {
            ServicesMonitor = serviceFinder.Services[s_mdnsSearchProfile];
            ServicesMonitor.ServiceWasSelected +=
                (context, s) =>
                {
                    if (context == this)
                    {
                        Settings.Host = s.HostName;
                    }
                };
        }
        _logger = loggerFactory.CreateLogger($"{nameof(WledClient)}/{settings.Name}");
        _clientLogger = loggerFactory.CreateLogger<WledWebsocketClient>();
        Name = settings.Name;
        _cancellationToken = cancellationToken;
        _cancelling = cancellationToken.WaitHandle;
        _changeFilters.AddFilter((s, e) => _reconnect.Set(), new PropertyChangeCondition(Settings, nameof(Settings.Host)));
        ServiceState = new(RemoteServiceName);
        _ = Task.Run(UpdateLoop, cancellationToken);
    }

    public Settings Settings { get; }
    IRemoteServiceSettings? IRemoteService.RemoteServiceSettings => Settings?.RemoteServiceSettings;

    public IMdnsMatchedServicesMonitor? ServicesMonitor { get; }

    public string Name { get; }

    private async Task UpdateLoop()
    {
        WaitHandle[] waitHandles = [_cancelling, _reconnect, _loadPreset];
        int handle;
        WledWebsocketClient? client = null;
        try
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                handle = WaitHandle.WaitAny(waitHandles, s_heartbeatPeriod);
                switch (handle)
                {
                    case WaitHandle.WaitTimeout:
                        client?.SendJsonAsync(new { on = true }); //keep alive
                        break;
                    case 0:
                        //_cancelling
                        AbandonClient();
                        return;
                    case 1:
                        //_reconnect requested
                        AbandonClient();
                        ServiceState.SetAll(new(RemoteServiceStates.Warning, "Reconnecting", null));
                        string host = Settings.Host;
                        if (string.IsNullOrWhiteSpace(host))
                        {
                            ServiceState.SetAll(new(RemoteServiceStates.Disconnected, "Disabled", 0));
                            continue;
                        }
                        client = new(host, _clientLogger);
                        JoinClient();
                        try
                        {
                            await client.ConnectAsync(false, _cancellationToken);
                        }
                        catch
                        {
                            ServiceState.SetAll(new(RemoteServiceStates.Warning, "Connection failed. Awaiting retry.", null));
                            AbandonClient();
                            await Task.Delay(5000, _cancellationToken);
                            _reconnect.Set();
                            continue;
                        }
                        ServiceState.SetAll(new(RemoteServiceStates.Connected, "Connected", 1));
                        break;
                    case 2:
                        //_loadPreset requested
                        while (client != null && _loadPresetRequestQueue.Count > 0)
                        {
                            List<int> presets = [];
                            while (_loadPresetRequestQueue.TryDequeue(out int preset))
                            {
                                presets.Add(preset);
                            }
                            
                            // Process to keep only the last occurrence of each preset
                            Dictionary<int, int> lastOccurrence = [];
                            for (int i = 0; i < presets.Count; i++)
                            {
                                lastOccurrence[presets[i]] = i;
                            }
                            
                            // Apply presets in original order, but only the last occurrence of each
                            foreach (KeyValuePair<int, int> pair in lastOccurrence.OrderBy(x => x.Value))
                            {
                                await client.SendJsonAsync(new { ps = pair.Key });
                            }
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("UpdateLoop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateLoop exception");
        }

        void JoinClient()
        {
            if (client != null)
            {
                client.OnStateChanged += StateChanged;
                client.OnInfoChanged += InfoChanged;
                client.OnDisconnected += Disconnected;
            }
        }

        void AbandonClient()
        {
            if (client != null)
            {
                client.OnStateChanged -= StateChanged;
                client.OnInfoChanged -= InfoChanged;
                client.OnDisconnected -= Disconnected;
                client.Dispose();
                client = null;
            }
        }

        void StateChanged(object? sender, StateChangedEventArgs args)
        {
            WledState = args.State;
        }

        void InfoChanged(object? sender, InfoChangedEventArgs args)
        {
            WledInfo = args.Info;
        }

        void Disconnected(object? sender, Websocket.Client.DisconnectionInfo args)
        {
            _reconnect.Set();
        }
    }
    public ServiceState ServiceState { get; }

    private State? _wledState;
    public State? WledState
    {
        get => _wledState;
        private set => _ = SetProperty(ref _wledState, value);
    }

    private Info? _wledInfo;
    public Info? WledInfo
    {
        get => _wledInfo;
        private set => _ = SetProperty(ref _wledInfo, value);
    }

    public void SetPreset(int preset)
    {
        if (!preset.IsInRange(-1, 250, true))
        {
            throw new ArgumentOutOfRangeException(nameof(preset), "Preset must be between -1 and 250.");
        }
        _loadPresetRequestQueue.Enqueue(preset);
        _loadPreset.Set();
    }
}
