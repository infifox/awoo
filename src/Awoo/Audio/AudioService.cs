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
/// being polled for. The header's <see cref="State"/> is then a view of
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
    private readonly DeviceNotifications _deviceNotifications;
    private readonly AudioSessionIndex _sessions;

    /// <summary>Every endpoint we hold open, by Core Audio's endpoint id.</summary>
    private readonly Dictionary<string, Endpoint> _endpoints = new(StringComparer.Ordinal);

    /// <summary>Stamped on every change awoo makes, so its echo can be ignored.</summary>
    private Guid _eventContext = Guid.NewGuid();

    private string? _defaultOutputId;
    private string? _defaultInputId;
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

    public void SetDefaultDevice(string deviceId)
    {
        if (_disposed || _policyConfig is null || !_endpoints.TryGetValue(deviceId, out var endpoint))
        {
            return;
        }

        if (deviceId == DefaultIdFor(endpoint.Kind))
        {
            return;
        }

        // All three roles together: Windows keeps a default per role, and a
        // device that is the default for playback but not for calls is not
        // what "set as default" means to anybody looking at one radio button.
        foreach (var role in Roles)
        {
            try
            {
                if (_policyConfig.SetDefaultEndpoint(deviceId, role) != CoreAudio.S_OK)
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

    /// <summary>The roles a "set as default" covers, in the order Windows applies them.</summary>
    private static ReadOnlySpan<Role> Roles => [Role.Console, Role.Multimedia, Role.Communications];

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

        _defaultOutputId = DefaultId(DataFlow.Render);
        _defaultInputId = DefaultId(DataFlow.Capture);

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
                endpoint.Id == DefaultIdFor(endpoint.Kind)))
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

    private Endpoint? DefaultOutput() =>
        _defaultOutputId is { } id && _endpoints.TryGetValue(id, out var endpoint) ? endpoint : null;

    private string? DefaultIdFor(DeviceKind kind) =>
        kind == DeviceKind.Output ? _defaultOutputId : _defaultInputId;

    /// <summary>
    /// The default endpoint's id for one flow, or null when the machine has
    /// none. E_NOTFOUND is the ordinary answer on a machine with no device of
    /// that flow at all.
    /// </summary>
    private string? DefaultId(DataFlow flow)
    {
        if (_enumerator.GetDefaultAudioEndpoint(flow, Role.Console, out var device) != CoreAudio.S_OK ||
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
            // The console role is the one awoo follows for both flows;
            // multimedia and communications are separate defaults that
            // SetDefaultDevice keeps in step with it.
            return role == Role.Console ? Refresh() : CoreAudio.S_OK;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key)
        {
            if (key.Matches(CoreAudio.DeviceFriendlyName))
            {
                owner._dispatch(() => owner.RefreshNameOf(deviceId));
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
