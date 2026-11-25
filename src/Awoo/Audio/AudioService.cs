using System.Runtime.InteropServices;

namespace Awoo.Audio;

/// <summary>
/// Follows every active audio endpoint — outputs and inputs — through Core
/// Audio, and which of them Windows treats as the default.
/// </summary>
/// <remarks>
/// <para>
/// Core Audio calls its notification interfaces back on its own threads, and
/// the documentation is explicit that those threads must not be used to call
/// back into the enumerator. So everything this service does — reading,
/// writing, raising its events — happens on one thread: the one the
/// <c>dispatch</c> delegate posts to, which the app wires to the UI thread.
/// The COM callbacks themselves do nothing but copy their arguments and post.
/// </para>
/// <para>
/// Every endpoint is held open with its own volume callback registered, so a
/// change to any device — not only the default one — arrives rather than
/// being polled for. The tray icon's <see cref="State"/> is then a view of
/// whichever endpoint is the default output, rather than a second
/// subscription to it.
/// </para>
/// <para>
/// Changes awoo itself makes carry an event context GUID, which is how the
/// echo of our own write is told apart from a change made elsewhere.
/// </para>
/// </remarks>
internal sealed class AudioService : IAudioService
{
    private readonly Action<Action> _dispatch;
    private readonly IMMDeviceEnumerator _enumerator;
    private readonly IPolicyConfig? _policyConfig;
    private readonly AppRouting _routing;
    private readonly SpatialSound _spatial;
    private readonly DeviceNotifications _deviceNotifications;
    private readonly AudioSessionIndex _sessions;

    /// <summary>The three Windows roles, in the order Windows applies them.</summary>
    private static readonly Role[] AllRoles = [Role.Console, Role.Multimedia, Role.Communications];

    /// <summary>The pair Windows' own Sound settings moves as one.</summary>
    private static readonly Role[] AudioRoles = [Role.Console, Role.Multimedia];

    private static readonly Role[] CommunicationsRoles = [Role.Communications];

    /// <summary>Every endpoint we hold open, by Core Audio's endpoint id.</summary>
    private readonly Dictionary<string, Endpoint> _endpoints = new(StringComparer.Ordinal);

    /// <summary>Stamped on every change awoo makes, so its echo can be ignored.</summary>
    private Guid _eventContext = Guid.NewGuid();

    private string? _defaultOutputId;
    private string? _defaultInputId;

    /// <summary>
    /// The communications default per flow, which is a default of its own:
    /// Windows lets calls go through one device while everything else goes
    /// through another, and awoo follows both so a row can show either.
    /// </summary>
    private string? _commsOutputId;
    private string? _commsInputId;

    private bool _disposed;

    /// <param name="dispatch">
    /// Posts work to the thread this service runs on. Everything it owns is
    /// touched there and nowhere else.
    /// </param>
    public AudioService(Action<Action> dispatch)
    {
        _dispatch = dispatch;
        _enumerator = CoreAudio.CreateEnumerator();

        // Both undocumented, so their absence is a capability awoo goes
        // without rather than a failure to start.
        _policyConfig = CoreAudio.CreatePolicyConfig();
        _routing = AppRouting.Create();

        // Documented, unlike the two above, but just as optional: a Windows
        // without it is one where no device offers spatial sound.
        _spatial = SpatialSound.Create();

        _deviceNotifications = new DeviceNotifications(this);
        _ = _enumerator.RegisterEndpointNotificationCallback(_deviceNotifications);

        // Sessions hang off endpoints, so the index has to exist before the
        // first Refresh opens any.
        _sessions = new AudioSessionIndex(dispatch, _routing);
        _sessions.AppsChanged += (_, apps) => AppsChanged?.Invoke(this, apps);

        State = VolumeState.NoDevice;
        Devices = [];
        Refresh();
    }

    public event EventHandler<VolumeState>? StateChanged;

    public event EventHandler<IReadOnlyList<AudioDeviceState>>? DevicesChanged;

    public event EventHandler<IReadOnlyList<AudioAppState>>? AppsChanged;

    public VolumeState State { get; private set; }

    public IReadOnlyList<AudioDeviceState> Devices { get; private set; }

    public IReadOnlyList<AudioAppState> Apps => _sessions.Apps;

    public bool CanSetDefaultDevice => _policyConfig is not null;

    public void SetPercent(int percent)
    {
        if (DefaultOutput() is { } output)
        {
            SetVolume(output, percent);
        }
    }

    public void SetMute(bool muted)
    {
        if (DefaultOutput() is { } output)
        {
            SetMuted(output, muted);
        }
    }

    public void SetDevicePercent(string deviceId, int percent)
    {
        if (_endpoints.TryGetValue(deviceId, out var endpoint))
        {
            SetVolume(endpoint, percent);
        }
    }

    public void SetDeviceMute(string deviceId, bool muted)
    {
        if (_endpoints.TryGetValue(deviceId, out var endpoint))
        {
            SetMuted(endpoint, muted);
        }
    }

    public void SetAppPercent(string appId, int percent) => _sessions.SetPercent(appId, percent);

    public void SetAppMute(string appId, bool muted) => _sessions.SetMute(appId, muted);

    public void SetAppDevice(string appId, string? deviceId) => _sessions.SetDevice(appId, deviceId);

    public void RefreshAppRoutes() => _sessions.RefreshRoutes();

    public Task SetDeviceSpatialFormat(string deviceId, Guid? formatId)
    {
        if (_disposed || !_endpoints.TryGetValue(deviceId, out var endpoint) || !endpoint.Spatial.CanSet)
        {
            return Task.CompletedTask;
        }

        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Settle(endpoint, formatId, settled);
        return settled.Task;
    }

    public void RefreshSpatialFormats()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var endpoint in _endpoints.Values)
        {
            ReadSpatial(endpoint);
        }

        Publish();
    }

    public void SetDefaultDevice(string deviceId, DefaultRole role)
    {
        if (_disposed || _policyConfig is null || !_endpoints.TryGetValue(deviceId, out var endpoint))
        {
            return;
        }

        if (AlreadyHolds(deviceId, endpoint.Kind, role))
        {
            return;
        }

        foreach (var windowsRole in RolesOf(role))
        {
            try
            {
                if (_policyConfig.SetDefaultEndpoint(deviceId, windowsRole) != CoreAudio.S_OK)
                {
                    break;
                }
            }
            catch (COMException)
            {
                // A future Windows that moved the vtable. Nothing to do but
                // leave the default where it was.
                break;
            }
        }

        // OnDefaultDeviceChanged will land as well, but not before this
        // returns, and the row that was clicked should not wait for it.
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = _enumerator.UnregisterEndpointNotificationCallback(_deviceNotifications);

        foreach (var endpoint in _endpoints.Values)
        {
            Release(endpoint);
        }

        _endpoints.Clear();
        _sessions.Dispose();
    }

    /// <summary>
    /// The Windows roles one of awoo's covers, in the order Windows applies
    /// them.
    /// </summary>
    /// <remarks>
    /// <see cref="DefaultRole.All"/> is all three together: a device that is
    /// the default for playback but not for calls is not what "set as
    /// default" means to anybody looking at one radio button.
    /// <see cref="DefaultRole.Audio"/> is the pair Windows' own Sound
    /// settings moves as one, leaving calls where they are.
    /// </remarks>
    private static ReadOnlySpan<Role> RolesOf(DefaultRole role) => role switch
    {
        DefaultRole.Audio => AudioRoles,
        DefaultRole.Communications => CommunicationsRoles,
        _ => AllRoles,
    };

    /// <summary>
    /// Brings the endpoint cache back in line with the machine: new devices
    /// opened, gone ones released, the rest re-read, and the two defaults
    /// looked up again. Cheap enough to run on any notification — a handful
    /// of endpoints, read once each.
    /// </summary>
    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var present = new HashSet<string>(StringComparer.Ordinal);
        Collect(DataFlow.Render, DeviceKind.Output, present);
        Collect(DataFlow.Capture, DeviceKind.Input, present);

        foreach (var id in _endpoints.Keys.Where(id => !present.Contains(id)).ToArray())
        {
            Release(_endpoints[id]);
            _endpoints.Remove(id);
        }

        _defaultOutputId = DefaultId(DataFlow.Render, Role.Console);
        _defaultInputId = DefaultId(DataFlow.Capture, Role.Console);
        _commsOutputId = DefaultId(DataFlow.Render, Role.Communications);
        _commsInputId = DefaultId(DataFlow.Capture, Role.Communications);

        Publish();
    }

    /// <summary>Opens, or re-reads, every active endpoint of one flow.</summary>
    private void Collect(DataFlow flow, DeviceKind kind, HashSet<string> present)
    {
        if (_enumerator.EnumAudioEndpoints(flow, CoreAudio.DeviceStateActive, out var collection) != CoreAudio.S_OK ||
            collection is null ||
            collection.GetCount(out var count) != CoreAudio.S_OK)
        {
            return;
        }

        for (var index = 0; index < count; index++)
        {
            if (collection.Item(index, out var device) != CoreAudio.S_OK || device is null)
            {
                continue;
            }

            if (device.GetId(out var id) != CoreAudio.S_OK || string.IsNullOrEmpty(id))
            {
                continue;
            }

            present.Add(id);

            if (_endpoints.TryGetValue(id, out var known))
            {
                Read(known);
                continue;
            }

            if (Attach(device, id, kind) is { } attached)
            {
                _endpoints[id] = attached;
            }
        }
    }

    /// <summary>
    /// Opens an endpoint's volume interface and registers for its changes.
    /// Null when the endpoint will not give the interface up — a device that
    /// is going away as we look at it.
    /// </summary>
    private Endpoint? Attach(IMMDevice device, string id, DeviceKind kind)
    {
        var iid = typeof(IAudioEndpointVolume).GUID;
        if (device.Activate(ref iid, CoreAudio.ClsCtxAll, IntPtr.Zero, out var instance) != CoreAudio.S_OK ||
            instance is not IAudioEndpointVolume volume)
        {
            return null;
        }

        var endpoint = new Endpoint(this, device, volume, id, kind, FriendlyName(device, kind));
        _ = volume.RegisterControlChangeNotify(endpoint);
        Read(endpoint);
        ReadSpatial(endpoint);

        // What this endpoint is playing or recording, for the Applications tab.
        _sessions.Attach(device, id, kind);
        return endpoint;
    }

    private void Release(Endpoint endpoint)
    {
        _ = endpoint.Volume.UnregisterControlChangeNotify(endpoint);
        _sessions.Detach(endpoint.Id);
    }

    /// <summary>Re-reads volume and mute from the endpoint itself.</summary>
    private static void Read(Endpoint endpoint)
    {
        if (endpoint.Volume.GetMasterVolumeLevelScalar(out var scalar) == CoreAudio.S_OK)
        {
            endpoint.Percent = VolumeState.ToPercent(scalar);
        }

        if (endpoint.Volume.GetMute(out var muted) == CoreAudio.S_OK)
        {
            endpoint.IsMuted = muted;
        }
    }

    /// <summary>Re-reads what spatial sound an endpoint offers and is in.</summary>
    private void ReadSpatial(Endpoint endpoint) =>
        endpoint.Spatial = _spatial.Read(endpoint.Device, endpoint.Id, endpoint.Kind);

    /// <summary>
    /// Puts one endpoint into a spatial format and publishes what it took.
    /// </summary>
    /// <remarks>
    /// The one call in this service that cannot be made and read back in a
    /// breath: Windows answers this one asynchronously, and this service's
    /// thread is the UI thread, so waiting for it here would stop the app
    /// rather than the call. The wait happens off the thread and the answer
    /// is posted back onto it, which is also what keeps the endpoint cache
    /// touched from one thread like everything else.
    /// </remarks>
    private async Task Settle(Endpoint endpoint, Guid? formatId, TaskCompletionSource settled)
    {
        try
        {
            // The answer is deliberately dropped: what the endpoint reads back
            // as is the truth, and a format Windows declined leaves it where
            // it was.
            _ = await _spatial.SetAsync(endpoint.Id, formatId).ConfigureAwait(false);
        }
        finally
        {
            // A finally because the menu's snap-back waits on this task: .NET
            // maps some Windows failures to exception types SetAsync's filter
            // does not cover — a device unplugged mid-write, say — and a throw
            // that skipped the read-back would leave the UI showing a format
            // that was never applied.
            _dispatch(() =>
            {
                if (!_disposed)
                {
                    ReadSpatial(endpoint);
                    Publish();
                }

                settled.SetResult();
            });
        }
    }

    private void SetVolume(Endpoint endpoint, int percent)
    {
        var context = _eventContext;
        if (endpoint.Volume.SetMasterVolumeLevelScalar(VolumeState.ToScalar(percent), ref context) != CoreAudio.S_OK)
        {
            return;
        }

        // Read back rather than assume: what the endpoint took is the truth.
        Read(endpoint);
        Publish();
    }

    private void SetMuted(Endpoint endpoint, bool muted)
    {
        var context = _eventContext;
        if (endpoint.Volume.SetMute(muted, ref context) != CoreAudio.S_OK)
        {
            return;
        }

        Read(endpoint);
        Publish();
    }

    /// <summary>
    /// Rebuilds both published views from the endpoint cache and raises
    /// whichever of them actually changed.
    /// </summary>
    private void Publish()
    {
        var devices = _endpoints.Values
            .OrderBy(endpoint => endpoint.Kind)
            .ThenBy(endpoint => endpoint.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(endpoint => new AudioDeviceState(
                endpoint.Id,
                endpoint.Name,
                endpoint.Kind,
                endpoint.Percent,
                endpoint.IsMuted,
                endpoint.Id == DefaultIdFor(endpoint.Kind),
                endpoint.Id == CommunicationsIdFor(endpoint.Kind),
                endpoint.Spatial))
            .ToArray();

        var header = DefaultOutput() is { } output
            ? new VolumeState(output.Name, output.Percent, output.IsMuted, true)
            : VolumeState.NoDevice;

        if (header != State)
        {
            State = header;
            StateChanged?.Invoke(this, header);
        }

        if (!devices.SequenceEqual(Devices))
        {
            Devices = devices;
            DevicesChanged?.Invoke(this, devices);
        }
    }

    /// <summary>Applies a notification an endpoint sent us about its own volume.</summary>
    private void ApplyNotification(string deviceId, AudioVolumeNotificationData data)
    {
        if (_disposed || !_endpoints.TryGetValue(deviceId, out var endpoint))
        {
            return;
        }

        if (data.EventContext == _eventContext)
        {
            // Our own write, already published by the call that made it.
            return;
        }

        endpoint.Percent = VolumeState.ToPercent(data.MasterVolume);
        endpoint.IsMuted = data.Muted != 0;
        Publish();
    }

    private void RefreshNameOf(string deviceId)
    {
        if (_disposed || !_endpoints.TryGetValue(deviceId, out var endpoint))
        {
            return;
        }

        endpoint.Name = FriendlyName(endpoint.Device, endpoint.Kind);
        Publish();
    }

    /// <summary>
    /// Takes one endpoint's spatial sound again, because Windows said it
    /// moved — awoo's own write, Settings', or a provider being installed or
    /// taken away.
    /// </summary>
    private void RefreshSpatialOf(string deviceId)
    {
        if (_disposed || !_endpoints.TryGetValue(deviceId, out var endpoint))
        {
            return;
        }

        ReadSpatial(endpoint);
        Publish();
    }

    private Endpoint? DefaultOutput() =>
        _defaultOutputId is { } id && _endpoints.TryGetValue(id, out var endpoint) ? endpoint : null;

    private string? DefaultIdFor(DeviceKind kind) =>
        kind == DeviceKind.Output ? _defaultOutputId : _defaultInputId;

    private string? CommunicationsIdFor(DeviceKind kind) =>
        kind == DeviceKind.Output ? _commsOutputId : _commsInputId;

    /// <summary>
    /// Whether this endpoint already holds every role a switch would set,
    /// which makes the switch nothing to do. Asked per role-set rather than
    /// of the default alone: a device can be what everything but calls goes
    /// through, and a click on its communications radio has work to do.
    /// </summary>
    private bool AlreadyHolds(string deviceId, DeviceKind kind, DefaultRole role) => role switch
    {
        DefaultRole.Communications => deviceId == CommunicationsIdFor(kind),
        DefaultRole.Audio => deviceId == DefaultIdFor(kind),
        _ => deviceId == DefaultIdFor(kind) && deviceId == CommunicationsIdFor(kind),
    };

    /// <summary>
    /// The default endpoint's id for one flow and role, or null when the
    /// machine has none. E_NOTFOUND is the ordinary answer on a machine with
    /// no device of that flow at all.
    /// </summary>
    private string? DefaultId(DataFlow flow, Role role)
    {
        if (_enumerator.GetDefaultAudioEndpoint(flow, role, out var device) != CoreAudio.S_OK ||
            device is null)
        {
            return null;
        }

        return device.GetId(out var id) == CoreAudio.S_OK ? id : null;
    }

    /// <summary>
    /// The endpoint's friendly name, or a placeholder if the property store
    /// will not give it up.
    /// </summary>
    private static string FriendlyName(IMMDevice device, DeviceKind kind)
    {
        var unknown = kind == DeviceKind.Output ? "Output device" : "Input device";

        if (device.OpenPropertyStore(CoreAudio.StgmRead, out var store) != CoreAudio.S_OK || store is null)
        {
            return unknown;
        }

        var key = CoreAudio.DeviceFriendlyName;
        if (store.GetValue(ref key, out var value) != CoreAudio.S_OK)
        {
            return unknown;
        }

        try
        {
            if (value.Type != CoreAudio.VtLpwstr)
            {
                return unknown;
            }

            var name = Marshal.PtrToStringUni(value.Value);
            return string.IsNullOrWhiteSpace(name) ? unknown : name;
        }
        finally
        {
            _ = CoreAudio.PropVariantClear(ref value);
        }
    }

    /// <summary>
    /// One endpoint awoo holds open, and its own volume callback. The
    /// callback runs on a Core Audio thread, so it copies the notification —
    /// the pointer is only good for the length of the call — and posts it.
    /// </summary>
    private sealed class Endpoint(
        AudioService owner,
        IMMDevice device,
        IAudioEndpointVolume volume,
        string id,
        DeviceKind kind,
        string name) : IAudioEndpointVolumeCallback
    {
        internal IMMDevice Device { get; } = device;

        internal IAudioEndpointVolume Volume { get; } = volume;

        internal string Id { get; } = id;

        internal DeviceKind Kind { get; } = kind;

        internal string Name { get; set; } = name;

        internal int Percent { get; set; }

        internal bool IsMuted { get; set; }

        /// <summary>
        /// What spatial sound this endpoint offers and is in. Cached rather
        /// than read on every publish: a slider drag publishes dozens of
        /// times a second, and this costs a property store each time.
        /// </summary>
        internal SpatialState Spatial { get; set; } = SpatialState.None;

        public int OnNotify(IntPtr notificationData)
        {
            if (notificationData == IntPtr.Zero)
            {
                return CoreAudio.S_OK;
            }

            var data = Marshal.PtrToStructure<AudioVolumeNotificationData>(notificationData);
            var deviceId = Id;
            owner._dispatch(() => owner.ApplyNotification(deviceId, data));
            return CoreAudio.S_OK;
        }
    }

    /// <summary>
    /// The enumerator's callback: devices coming, going and becoming the
    /// default. Like the volume callbacks, it only posts.
    /// </summary>
    private sealed class DeviceNotifications(AudioService owner) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, int newState) => Refresh();

        public int OnDeviceAdded(string deviceId) => Refresh();

        public int OnDeviceRemoved(string deviceId) => Refresh();

        public int OnDefaultDeviceChanged(DataFlow flow, Role role, string? deviceId)
        {
            // Console and communications are the two defaults awoo shows, so
            // either moving is news. Multimedia is not: it rides with console
            // everywhere awoo or Windows' own settings set a default, and a
            // refresh for it would only re-read what the console
            // notification already brought.
            return role is Role.Console or Role.Communications ? Refresh() : CoreAudio.S_OK;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key)
        {
            if (key.Matches(CoreAudio.DeviceFriendlyName))
            {
                owner._dispatch(() => owner.RefreshNameOf(deviceId));
            }
            else if (SpatialSound.Announces(key))
            {
                owner._dispatch(() => owner.RefreshSpatialOf(deviceId));
            }

            return CoreAudio.S_OK;
        }

        private int Refresh()
        {
            owner._dispatch(owner.Refresh);
            return CoreAudio.S_OK;
        }
    }
}
