using System.Runtime.InteropServices;

namespace Awoo.Audio;

/// <summary>
/// Follows the default output device and its volume through Core Audio.
/// </summary>
/// <remarks>
/// <para>
/// Core Audio calls its notification interfaces back on its own threads, and
/// the documentation is explicit that those threads must not be used to call
/// back into the enumerator. So everything this service does — reading,
/// writing, raising <see cref="StateChanged"/> — happens on one thread: the
/// one the <c>dispatch</c> delegate posts to, which the app wires to the UI
/// thread. The COM callbacks themselves do nothing but copy their arguments
/// and post.
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
    private readonly DeviceNotifications _deviceNotifications;
    private readonly VolumeNotifications _volumeNotifications;

    /// <summary>Stamped on every change awoo makes, so its echo can be ignored.</summary>
    private Guid _eventContext = Guid.NewGuid();

    private IMMDevice? _device;
    private IAudioEndpointVolume? _volume;
    private string? _deviceId;
    private bool _disposed;

    /// <param name="dispatch">
    /// Posts work to the thread this service runs on. Everything it owns is
    /// touched there and nowhere else.
    /// </param>
    public AudioService(Action<Action> dispatch)
    {
        _dispatch = dispatch;
        _enumerator = CoreAudio.CreateEnumerator();

        _deviceNotifications = new DeviceNotifications(this);
        _volumeNotifications = new VolumeNotifications(this);

        _ = _enumerator.RegisterEndpointNotificationCallback(_deviceNotifications);

        State = VolumeState.NoDevice;
        Rebind();
    }

    public event EventHandler<VolumeState>? StateChanged;

    public VolumeState State { get; private set; }

    public void SetPercent(int percent)
    {
        if (_volume is null)
        {
            return;
        }

        var context = _eventContext;
        if (_volume.SetMasterVolumeLevelScalar(VolumeState.ToScalar(percent), ref context) != CoreAudio.S_OK)
        {
            return;
        }

        // Read back rather than assume: what the endpoint took is the truth.
        PublishVolume();
    }

    public void SetMute(bool muted)
    {
        if (_volume is null)
        {
            return;
        }

        var context = _eventContext;
        if (_volume.SetMute(muted, ref context) != CoreAudio.S_OK)
        {
            return;
        }

        PublishVolume();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = _enumerator.UnregisterEndpointNotificationCallback(_deviceNotifications);
        Detach();
    }

    /// <summary>
    /// Points the service at whatever the default output device is now. Safe
    /// to call when nothing has changed: the endpoint is only re-opened when
    /// the device id is a different one.
    /// </summary>
    private void Rebind()
    {
        if (_disposed)
        {
            return;
        }

        var hr = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console, out var device);
        if (hr != CoreAudio.S_OK || device is null)
        {
            // E_NOTFOUND is the ordinary answer on a machine with no output.
            Detach();
            Publish(VolumeState.NoDevice);
            return;
        }

        _ = device.GetId(out var id);
        if (id is not null && id == _deviceId && _volume is not null)
        {
            PublishAll();
            return;
        }

        Detach();
        Attach(device, id);
    }

    private void Attach(IMMDevice device, string? id)
    {
        var iid = typeof(IAudioEndpointVolume).GUID;
        var hr = device.Activate(ref iid, CoreAudio.ClsCtxAll, IntPtr.Zero, out var instance);
        if (hr != CoreAudio.S_OK || instance is not IAudioEndpointVolume volume)
        {
            Publish(VolumeState.NoDevice);
            return;
        }

        _device = device;
        _deviceId = id;
        _volume = volume;
        _ = volume.RegisterControlChangeNotify(_volumeNotifications);

        PublishAll();
    }

    private void Detach()
    {
        if (_volume is not null)
        {
            _ = _volume.UnregisterControlChangeNotify(_volumeNotifications);
        }

        _volume = null;
        _device = null;
        _deviceId = null;
    }

    /// <summary>Re-reads name, volume and mute from the endpoint.</summary>
    private void PublishAll()
    {
        if (_volume is null || _device is null)
        {
            Publish(VolumeState.NoDevice);
            return;
        }

        Publish(Read(FriendlyName(_device)));
    }

    /// <summary>
    /// Re-reads volume and mute only, keeping the device name we already have
    /// — this runs on every step of a slider drag.
    /// </summary>
    private void PublishVolume()
    {
        if (_volume is null)
        {
            Publish(VolumeState.NoDevice);
            return;
        }

        Publish(Read(State.DeviceName));
    }

    private VolumeState Read(string deviceName)
    {
        if (_volume is null)
        {
            return VolumeState.NoDevice;
        }

        if (_volume.GetMasterVolumeLevelScalar(out var scalar) != CoreAudio.S_OK ||
            _volume.GetMute(out var muted) != CoreAudio.S_OK)
        {
            return VolumeState.NoDevice;
        }

        return new VolumeState(deviceName, VolumeState.ToPercent(scalar), muted, true);
    }

    private void Publish(VolumeState state)
    {
        if (state == State)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, state);
    }

    /// <summary>Applies a notification the endpoint sent us about its own volume.</summary>
    private void ApplyNotification(AudioVolumeNotificationData data)
    {
        if (_disposed || _volume is null)
        {
            return;
        }

        if (data.EventContext == _eventContext)
        {
            // Our own write, already published by the call that made it.
            return;
        }

        Publish(State with
        {
            Percent = VolumeState.ToPercent(data.MasterVolume),
            IsMuted = data.Muted != 0,
        });
    }

    private void RefreshNameOf(string deviceId)
    {
        if (!_disposed && deviceId == _deviceId)
        {
            PublishAll();
        }
    }

    /// <summary>
    /// The endpoint's friendly name, or a placeholder if the property store
    /// will not give it up.
    /// </summary>
    private static string FriendlyName(IMMDevice device)
    {
        const string Unknown = "Output device";

        if (device.OpenPropertyStore(CoreAudio.StgmRead, out var store) != CoreAudio.S_OK || store is null)
        {
            return Unknown;
        }

        var key = CoreAudio.DeviceFriendlyName;
        if (store.GetValue(ref key, out var value) != CoreAudio.S_OK)
        {
            return Unknown;
        }

        try
        {
            if (value.Type != CoreAudio.VtLpwstr)
            {
                return Unknown;
            }

            var name = Marshal.PtrToStringUni(value.Value);
            return string.IsNullOrWhiteSpace(name) ? Unknown : name;
        }
        finally
        {
            _ = CoreAudio.PropVariantClear(ref value);
        }
    }

    /// <summary>
    /// The endpoint's own volume callback. It runs on a Core Audio thread, so
    /// it copies the notification — the pointer is only good for the length of
    /// the call — and posts it.
    /// </summary>
    private sealed class VolumeNotifications(AudioService owner) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr notificationData)
        {
            if (notificationData == IntPtr.Zero)
            {
                return CoreAudio.S_OK;
            }

            var data = Marshal.PtrToStructure<AudioVolumeNotificationData>(notificationData);
            owner._dispatch(() => owner.ApplyNotification(data));
            return CoreAudio.S_OK;
        }
    }

    /// <summary>
    /// The enumerator's callback: devices coming, going and becoming the
    /// default. Like the volume callback, it only posts.
    /// </summary>
    private sealed class DeviceNotifications(AudioService owner) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, int newState) => Rebind();

        public int OnDeviceAdded(string deviceId) => Rebind();

        public int OnDeviceRemoved(string deviceId) => Rebind();

        public int OnDefaultDeviceChanged(DataFlow flow, Role role, string? deviceId)
        {
            // Only the console render role is the one the flyout follows;
            // communications and multimedia are separate defaults.
            return flow == DataFlow.Render && role == Role.Console ? Rebind() : CoreAudio.S_OK;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key)
        {
            if (key.Matches(CoreAudio.DeviceFriendlyName))
            {
                owner._dispatch(() => owner.RefreshNameOf(deviceId));
            }

            return CoreAudio.S_OK;
        }

        private int Rebind()
        {
            owner._dispatch(owner.Rebind);
            return CoreAudio.S_OK;
        }
    }
}
