using System.Runtime.InteropServices;

namespace Awoo.Audio;

/// <summary>
/// Where one application's sound goes: reads and writes the per-application
/// device route Windows' own "App volume and device preferences" page sets.
/// </summary>
/// <remarks>
/// <para>
/// The interface behind this is undocumented, so its absence is a capability
/// awoo goes without rather than a failure to start:
/// <see cref="IsAvailable"/> is false, every read answers "no route" and
/// every write answers false, and the UI hides the selector. Nothing here
/// throws.
/// </para>
/// <para>
/// Windows persists the route against the <em>application</em>, not the
/// process — it resolves the process id it is given to the application's
/// identity and remembers that — which is what makes a route outlive the
/// application being closed and reopened. It also means the process id only
/// has to be one of the application's: routing any process of a browser
/// routes the browser.
/// </para>
/// <para>
/// Instances are not thread-safe and are not meant to be: like everything
/// else in the audio layer, this is touched only on the thread
/// <see cref="AudioService"/> runs on.
/// </para>
/// </remarks>
internal sealed class AppRouting
{
    /// <summary>The Windows Runtime class whose activation factory this is.</summary>
    private const string ActivatableClassId = "Windows.Media.Internal.AudioPolicyConfig";

    /// <summary>
    /// What a Core Audio endpoint id has to be wrapped in before the policy
    /// interface will take it: the device interface path of the MMDEVAPI
    /// software device, which is what Windows persists.
    /// </summary>
    private const string EndpointPrefix = @"\\?\SWD#MMDEVAPI#";

    private const string RenderSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    private const string CaptureSuffix = "#{2eef81be-33fa-4800-9670-1cd474972c3f}";

    private readonly IAudioPolicyConfig? _policy;

    private AppRouting(IAudioPolicyConfig? policy) => _policy = policy;

    /// <summary>
    /// Whether this build of Windows let us at the policy interface. False
    /// turns every other method here into a no-op.
    /// </summary>
    internal bool IsAvailable => _policy is not null;

    /// <summary>
    /// Asks Windows for the policy interface, or returns an instance that can
    /// do nothing where it will not hand it over. Call it on the thread that
    /// will use it, like the rest of the audio layer.
    /// </summary>
    internal static AppRouting Create()
    {
        var classId = IntPtr.Zero;

        try
        {
            if (WindowsRuntime.WindowsCreateString(
                    ActivatableClassId, (uint)ActivatableClassId.Length, out classId) != CoreAudio.S_OK)
            {
                return new AppRouting(null);
            }

            var iid = typeof(IAudioPolicyConfig).GUID;
            if (WindowsRuntime.RoGetActivationFactory(classId, ref iid, out var factory) != CoreAudio.S_OK)
            {
                return new AppRouting(null);
            }

            return new AppRouting(factory as IAudioPolicyConfig);
        }
        catch (Exception e) when (e is COMException or InvalidCastException or NotSupportedException
                                      or DllNotFoundException or EntryPointNotFoundException)
        {
            // A Windows that moved, renamed or withdrew the class. Nothing to
            // do but go without per-application routing.
            return new AppRouting(null);
        }
        finally
        {
            _ = WindowsRuntime.WindowsDeleteString(classId);
        }
    }

    /// <summary>
    /// Whether to offer moving one session's sound at all. A process id of
    /// its own is what the policy interface is asked about, and Windows'
    /// system-sounds session has none that means anything — it belongs to the
    /// audio service rather than to a program, so routing "it" would move
    /// everything that service is playing.
    /// </summary>
    internal bool CanRoute(int processId, bool isSystemSounds) =>
        IsAvailable && processId > 0 && !isSystemSounds;

    /// <summary>
    /// The endpoint id one process's sound has been sent to, or null where it
    /// follows the default device — which is the ordinary answer.
    /// </summary>
    /// <remarks>
    /// Windows answers for a process it knows nothing about with
    /// <c>E_INVALIDARG</c> rather than with "none", so a process that has
    /// never played reads as no route either way. That is the right answer
    /// for a caller that only asks about what is playing.
    /// </remarks>
    internal string? RouteOf(int processId, SessionKind kind)
    {
        if (_policy is null || processId <= 0)
        {
            return null;
        }

        var packed = IntPtr.Zero;

        try
        {
            // One role is enough to read: SetRoute keeps console and
            // multimedia on the same endpoint, so either answers for both.
            return _policy.GetPersistedDefaultAudioEndpoint(
                       (uint)processId, FlowOf(kind), Role.Multimedia, out packed) == CoreAudio.S_OK
                ? Unpack(WindowsRuntime.ReadString(packed))
                : null;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            // The HSTRING came back to us, so it is ours to free.
            _ = WindowsRuntime.WindowsDeleteString(packed);
        }
    }

    /// <summary>
    /// Sends one process's sound to one endpoint, or back to following the
    /// default device when <paramref name="deviceId"/> is null. False where
    /// the interface is missing or would not take it — Windows wants a device
    /// that exists and a process it knows to be playing, and says
    /// <c>E_INVALIDARG</c> to anything else.
    /// </summary>
    internal bool SetRoute(int processId, SessionKind kind, string? deviceId)
    {
        if (_policy is null || processId <= 0)
        {
            return false;
        }

        var packed = IntPtr.Zero;

        try
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                var path = Pack(deviceId, kind);
                if (WindowsRuntime.WindowsCreateString(path, (uint)path.Length, out packed) != CoreAudio.S_OK)
                {
                    return false;
                }
            }

            // A null HSTRING is the empty string, which is how the interface
            // is told to forget the route rather than to set one.
            var flow = FlowOf(kind);

            // Both roles together: Windows keeps a route per role, and an
            // application routed for multimedia but not for console is not
            // what picking one device from one menu means. Communications is
            // left alone — Windows' own page does not touch it either.
            return _policy.SetPersistedDefaultAudioEndpoint((uint)processId, flow, Role.Multimedia, packed)
                       == CoreAudio.S_OK &&
                   _policy.SetPersistedDefaultAudioEndpoint((uint)processId, flow, Role.Console, packed)
                       == CoreAudio.S_OK;
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            _ = WindowsRuntime.WindowsDeleteString(packed);
        }
    }

    /// <summary>Which flow a session's streams are, as the policy interface names them.</summary>
    private static DataFlow FlowOf(SessionKind kind) =>
        kind == SessionKind.Playback ? DataFlow.Render : DataFlow.Capture;

    /// <summary>Wraps a Core Audio endpoint id as the device interface path Windows persists.</summary>
    private static string Pack(string deviceId, SessionKind kind) =>
        EndpointPrefix + deviceId + (kind == SessionKind.Playback ? RenderSuffix : CaptureSuffix);

    /// <summary>
    /// Pulls the Core Audio endpoint id back out of a device interface path,
    /// so it can be compared with the ids the Devices list is keyed on. Null
    /// for the empty string, which is what "no route" reads as.
    /// </summary>
    /// <remarks>
    /// Both suffixes are tried rather than the one this flow would have
    /// written: what comes back is whatever is persisted, and awoo did not
    /// necessarily write it.
    /// </remarks>
    private static string? Unpack(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var id = path.Trim();

        if (id.StartsWith(EndpointPrefix, StringComparison.OrdinalIgnoreCase))
        {
            id = id[EndpointPrefix.Length..];
        }

        foreach (var suffix in (string[])[RenderSuffix, CaptureSuffix])
        {
            if (id.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                id = id[..^suffix.Length];
                break;
            }
        }

        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
