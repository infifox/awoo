using System.Runtime.InteropServices;

namespace Awoo.Audio;

/// <summary>
/// Follows every audio session on the machine — what each application is
/// playing or recording — and folds them into one row per application.
/// </summary>
/// <remarks>
/// <para>
/// Owned by <see cref="AudioService"/>, which tells it when an endpoint is
/// opened or released: sessions belong to an endpoint, so an app playing to a
/// device that is not the default is still seen, and a device going away takes
/// its sessions with it.
/// </para>
/// <para>
/// Threading is <see cref="AudioService"/>'s: Core Audio calls the
/// notification interfaces back on its own threads, those callbacks do
/// nothing but copy and post, and everything else happens on the one thread
/// the <c>dispatch</c> delegate posts to.
/// </para>
/// <para>
/// Only sessions Core Audio reports as <see cref="AudioSessionState.Active"/>
/// become rows. A Windows audio session outlives the sound it was playing —
/// it goes inactive and lingers for a while before expiring — and the
/// Applications tab is a list of what is playing now, the way the Plasma
/// applet's is, so an app that has stopped drops off rather than sitting
/// there greyed out.
/// </para>
/// <para>
/// A row does wait <see cref="Linger"/> before it goes, though, because
/// "playing" is burstier than it sounds: Windows' system-sounds session goes
/// active and inactive again for every single notification, so a run of them
/// would otherwise insert and remove its row several times a second.
/// </para>
/// </remarks>
internal sealed class AudioSessionIndex : IDisposable
{
    /// <summary>
    /// How long a row stays after its last stream went quiet. Long enough to
    /// bridge the gap between two notification sounds, short enough that
    /// stopping a video still reads as the row going away.
    /// </summary>
    private static readonly TimeSpan Linger = TimeSpan.FromSeconds(2);

    private readonly Action<Action> _dispatch;

    /// <summary>Re-publishes when the oldest lingering row's time is up.</summary>
    private readonly Timer _settle;

    /// <summary>One per endpoint we hold open, keyed by Core Audio's endpoint id.</summary>
    private readonly Dictionary<string, Source> _sources = new(StringComparer.Ordinal);

    /// <summary>Every session we know of, keyed by its instance identifier.</summary>
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    /// <summary>Stamped on every change awoo makes, so its echo can be ignored.</summary>
    private Guid _eventContext = Guid.NewGuid();

    private bool _disposed;

    internal AudioSessionIndex(Action<Action> dispatch)
    {
        _dispatch = dispatch;
        Apps = [];

        // Idle until Publish has a linger to wait out, and firing onto the
        // service's own thread like every other callback here.
        _settle = new Timer(
            _ => _dispatch(() =>
            {
                if (!_disposed)
                {
                    Publish();
                }
            }),
            null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    internal event EventHandler<IReadOnlyList<AudioAppState>>? AppsChanged;

    /// <summary>Every application playing or recording right now, playback first.</summary>
    internal IReadOnlyList<AudioAppState> Apps { get; private set; }

    /// <summary>
    /// Starts watching one endpoint's sessions. Called for every endpoint
    /// <see cref="AudioService"/> opens, inputs as well as outputs.
    /// </summary>
    internal void Attach(IMMDevice device, string endpointId, DeviceKind kind)
    {
        if (_disposed || _sources.ContainsKey(endpointId))
        {
            return;
        }

        var iid = typeof(IAudioSessionManager2).GUID;
        if (device.Activate(ref iid, CoreAudio.ClsCtxAll, IntPtr.Zero, out var instance) != CoreAudio.S_OK ||
            instance is not IAudioSessionManager2 manager)
        {
            return;
        }

        var source = new Source(
            this,
            manager,
            endpointId,
            kind == DeviceKind.Output ? SessionKind.Playback : SessionKind.Recording);

        _sources[endpointId] = source;

        // Enumerated before registering, and in this order: a session manager
        // does not report new sessions until its enumerator has been asked
        // for once, so registering first would miss everything until the
        // first app happened to start.
        foreach (var control in Existing(manager))
        {
            Track(source, control);
        }

        _ = manager.RegisterSessionNotification(source);
        Publish();
    }

    /// <summary>
    /// Stops watching one endpoint, and drops the sessions that were on it —
    /// a device being unplugged ends every stream playing to it.
    /// </summary>
    internal void Detach(string endpointId)
    {
        if (!_sources.Remove(endpointId, out var source))
        {
            return;
        }

        _ = source.Manager.UnregisterSessionNotification(source);

        foreach (var session in _sessions.Values.Where(s => s.EndpointId == endpointId).ToArray())
        {
            Forget(session);
        }

        Publish();
    }

    /// <summary>Sets one application's volume, 0–100, across every stream it has.</summary>
    internal void SetPercent(string appId, int percent)
    {
        var scalar = VolumeState.ToScalar(percent);
        var context = _eventContext;

        foreach (var session in StreamsOf(appId))
        {
            if (session.Volume.SetMasterVolume(scalar, ref context) == CoreAudio.S_OK)
            {
                Read(session);
            }
        }

        Publish();
    }

    /// <summary>Mutes or unmutes every stream of one application.</summary>
    internal void SetMute(string appId, bool muted)
    {
        var context = _eventContext;

        foreach (var session in StreamsOf(appId))
        {
            if (session.Volume.SetMute(muted, ref context) == CoreAudio.S_OK)
            {
                Read(session);
            }
        }

        Publish();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Detached before the timer goes, because letting an endpoint go
        // publishes, and publishing re-arms the timer.
        foreach (var endpointId in _sources.Keys.ToArray())
        {
            Detach(endpointId);
        }

        _settle.Dispose();
    }

    /// <summary>
    /// The sessions a manager already has. Materialised rather than yielded:
    /// the enumerator is a snapshot, and nothing should be holding it open
    /// while we call back into Core Audio about each session.
    /// </summary>
    private static IReadOnlyList<IAudioSessionControl> Existing(IAudioSessionManager2 manager)
    {
        if (manager.GetSessionEnumerator(out var sessions) != CoreAudio.S_OK ||
            sessions is null ||
            sessions.GetCount(out var count) != CoreAudio.S_OK)
        {
            return [];
        }

        var found = new List<IAudioSessionControl>(count);

        for (var index = 0; index < count; index++)
        {
            if (sessions.GetSession(index, out var control) == CoreAudio.S_OK && control is not null)
            {
                found.Add(control);
            }
        }

        return found;
    }

    /// <summary>
    /// Takes one session on, registering for its changes. Quietly does
    /// nothing for a session that is already known, which is what makes a
    /// notification arriving for a session we enumerated harmless.
    /// </summary>
    private void Track(Source source, IAudioSessionControl control)
    {
        if (_disposed ||
            control is not IAudioSessionControl2 identified ||
            control is not ISimpleAudioVolume volume ||
            identified.GetSessionInstanceIdentifier(out var instanceId) != CoreAudio.S_OK ||
            string.IsNullOrEmpty(instanceId) ||
            _sessions.ContainsKey(instanceId))
        {
            return;
        }

        var session = new Session(
            this,
            identified,
            volume,
            instanceId,
            source.EndpointId,
            source.Kind,
            SessionIdentity.For(identified, source.Kind));

        Read(session);
        _sessions[instanceId] = session;
        _ = identified.RegisterAudioSessionNotification(session);
    }

    /// <summary>A session arriving after we were already watching its endpoint.</summary>
    private void Adopt(Source source, IAudioSessionControl control)
    {
        if (_disposed || !_sources.ContainsKey(source.EndpointId))
        {
            return;
        }

        Track(source, control);
        Publish();
    }

    /// <summary>Lets a session go: the stream has expired or its device has gone.</summary>
    private void Forget(Session session)
    {
        if (!_sessions.Remove(session.InstanceId))
        {
            return;
        }

        _ = session.Control.UnregisterAudioSessionNotification(session);
    }

    /// <summary>Re-reads one session's state, volume and mute from Core Audio.</summary>
    private static void Read(Session session)
    {
        if (session.Control.GetState(out var state) == CoreAudio.S_OK)
        {
            Settle(session, state);
        }

        if (session.Volume.GetMasterVolume(out var scalar) == CoreAudio.S_OK)
        {
            session.Percent = VolumeState.ToPercent(scalar);
        }

        if (session.Volume.GetMute(out var muted) == CoreAudio.S_OK)
        {
            session.IsMuted = muted;
        }
    }

    /// <summary>
    /// Moves a session to a new state, noting the moment it stopped playing
    /// so <see cref="Shown"/> can let its row linger. A session that was
    /// never active has nothing to linger over — that is what keeps the
    /// already-idle sessions a fresh endpoint enumerates off the list.
    /// </summary>
    private static void Settle(Session session, AudioSessionState state)
    {
        if (state == AudioSessionState.Active)
        {
            session.QuietSince = null;
        }
        else if (session.State == AudioSessionState.Active)
        {
            session.QuietSince = DateTime.UtcNow;
        }

        session.State = state;
    }

    /// <summary>Whether a session is one the Applications tab should be showing.</summary>
    private static bool Shown(Session session, DateTime now) =>
        session.State == AudioSessionState.Active ||
        (session.QuietSince is { } quiet && now - quiet < Linger);

    /// <summary>Every stream of one application that its row stands for.</summary>
    private Session[] StreamsOf(string appId)
    {
        var now = DateTime.UtcNow;

        return _sessions.Values
            .Where(session => session.Identity.Key == appId && Shown(session, now))
            .ToArray();
    }

    /// <summary>
    /// Rebuilds the published list from the sessions we hold, one row per
    /// application, and raises it if anything moved.
    /// </summary>
    private void Publish()
    {
        var now = DateTime.UtcNow;
        ScheduleSettle(now);

        var apps = _sessions.Values
            .Where(session => Shown(session, now))
            .GroupBy(session => session.Identity.Key, StringComparer.Ordinal)
            // Ordered inside the group as well as outside it: which stream a
            // row takes its name and icon from must not depend on what order
            // a dictionary happened to hand them over in.
            .Select(group => group.OrderBy(session => session.InstanceId, StringComparer.Ordinal).ToArray())
            .Select(streams => new AudioAppState(
                streams[0].Identity.Key,
                streams[0].Identity.Name,
                streams.Select(session => session.Identity.IconFile).FirstOrDefault(file => file is not null),
                streams[0].Kind,
                // One slider for several streams: the loudest is what the app
                // is actually playing at, and setting it levels them all.
                streams.Max(session => session.Percent),
                // Muted only when nothing can be heard: one unmuted stream
                // makes the app audible, so the button has to offer Mute.
                streams.All(session => session.IsMuted),
                streams[0].Identity.IsSystemSounds))
            .OrderBy(app => app.Kind)
            .ThenBy(app => app.IsSystemSounds)
            .ThenBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (apps.SequenceEqual(Apps))
        {
            return;
        }

        Apps = apps;
        AppsChanged?.Invoke(this, apps);
    }

    /// <summary>
    /// Arranges for another publish when the soonest lingering row's time
    /// runs out — nothing else would raise one, because a row going quiet is
    /// a moment passing rather than an event arriving. Only lingers that have
    /// time left are counted, which is what stops the timer re-arming itself
    /// for ever once they are all behind us.
    /// </summary>
    private void ScheduleSettle(DateTime now)
    {
        TimeSpan? soonest = null;

        foreach (var session in _sessions.Values)
        {
            if (session.State == AudioSessionState.Active || session.QuietSince is not { } quiet)
            {
                continue;
            }

            var left = Linger - (now - quiet);
            if (left > TimeSpan.Zero && (soonest is null || left < soonest))
            {
                soonest = left;
            }
        }

        _ = _settle.Change(soonest ?? Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void ApplyVolume(string instanceId, int percent, bool muted)
    {
        if (_disposed || !_sessions.TryGetValue(instanceId, out var session))
        {
            return;
        }

        session.Percent = percent;
        session.IsMuted = muted;
        Publish();
    }

    private void ApplyState(string instanceId, AudioSessionState state)
    {
        if (_disposed || !_sessions.TryGetValue(instanceId, out var session))
        {
            return;
        }

        if (state == AudioSessionState.Expired)
        {
            Forget(session);
        }
        else
        {
            Settle(session, state);
        }

        Publish();
    }

    private void ApplyIdentity(string instanceId)
    {
        if (_disposed || !_sessions.TryGetValue(instanceId, out var session))
        {
            return;
        }

        session.Identity = SessionIdentity.For(session.Control, session.Kind);
        Publish();
    }

    private void Drop(string instanceId)
    {
        if (_disposed || !_sessions.TryGetValue(instanceId, out var session))
        {
            return;
        }

        Forget(session);
        Publish();
    }

    /// <summary>
    /// An event context as Core Audio passes it: an LPCGUID that may be null
    /// when the change came from something that did not identify itself.
    /// </summary>
    private static Guid ContextOf(IntPtr pointer) =>
        pointer == IntPtr.Zero ? Guid.Empty : Marshal.PtrToStructure<Guid>(pointer);

    /// <summary>
    /// One endpoint's session manager, and the callback that reports new
    /// sessions on it. Like every Core Audio callback here, it only posts.
    /// </summary>
    private sealed class Source(
        AudioSessionIndex owner,
        IAudioSessionManager2 manager,
        string endpointId,
        SessionKind kind) : IAudioSessionNotification
    {
        internal IAudioSessionManager2 Manager { get; } = manager;

        internal string EndpointId { get; } = endpointId;

        internal SessionKind Kind { get; } = kind;

        public int OnSessionCreated(IAudioSessionControl? session)
        {
            if (session is not null)
            {
                owner._dispatch(() => owner.Adopt(this, session));
            }

            return CoreAudio.S_OK;
        }
    }

    /// <summary>
    /// One session awoo holds open, and its own event callback. The callback
    /// runs on a Core Audio thread, so it copies what it was given — the
    /// event-context pointer is only good for the length of the call — and
    /// posts the rest.
    /// </summary>
    private sealed class Session(
        AudioSessionIndex owner,
        IAudioSessionControl2 control,
        ISimpleAudioVolume volume,
        string instanceId,
        string endpointId,
        SessionKind kind,
        SessionIdentity identity) : IAudioSessionEvents
    {
        internal IAudioSessionControl2 Control { get; } = control;

        internal ISimpleAudioVolume Volume { get; } = volume;

        /// <summary>Core Audio's instance identifier: unique to this one stream.</summary>
        internal string InstanceId { get; } = instanceId;

        /// <summary>The endpoint this stream plays to or records from.</summary>
        internal string EndpointId { get; } = endpointId;

        internal SessionKind Kind { get; } = kind;

        internal SessionIdentity Identity { get; set; } = identity;

        internal AudioSessionState State { get; set; } = AudioSessionState.Inactive;

        /// <summary>
        /// When this stream last stopped playing, or null if it has not
        /// played yet or is playing now. What <see cref="Linger"/> is
        /// measured from.
        /// </summary>
        internal DateTime? QuietSince { get; set; }

        internal int Percent { get; set; }

        internal bool IsMuted { get; set; }

        public int OnDisplayNameChanged(string? name, IntPtr eventContext) => Reidentify();

        public int OnIconPathChanged(string? path, IntPtr eventContext) => Reidentify();

        public int OnSimpleVolumeChanged(float level, bool muted, IntPtr eventContext)
        {
            if (ContextOf(eventContext) == owner._eventContext)
            {
                // Our own write, already published by the call that made it.
                return CoreAudio.S_OK;
            }

            var percent = VolumeState.ToPercent(level);
            var id = InstanceId;
            owner._dispatch(() => owner.ApplyVolume(id, percent, muted));
            return CoreAudio.S_OK;
        }

        public int OnChannelVolumeChanged(int channelCount, IntPtr volumes, int changedChannel, IntPtr eventContext) =>
            CoreAudio.S_OK;

        public int OnGroupingParamChanged(IntPtr groupingParam, IntPtr eventContext) => CoreAudio.S_OK;

        public int OnStateChanged(AudioSessionState state)
        {
            var id = InstanceId;
            owner._dispatch(() => owner.ApplyState(id, state));
            return CoreAudio.S_OK;
        }

        public int OnSessionDisconnected(AudioSessionDisconnectReason reason)
        {
            var id = InstanceId;
            owner._dispatch(() => owner.Drop(id));
            return CoreAudio.S_OK;
        }

        private int Reidentify()
        {
            var id = InstanceId;
            owner._dispatch(() => owner.ApplyIdentity(id));
            return CoreAudio.S_OK;
        }
    }
}
