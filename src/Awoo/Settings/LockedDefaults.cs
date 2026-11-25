using Awoo.Audio;

namespace Awoo.Settings;

/// <summary>
/// The devices awoo holds Windows to while the default-device lock is on: one
/// per kind and per role, by Core Audio endpoint id.
/// </summary>
/// <remarks>
/// <para>
/// Four slots rather than two, because the roles are guarded apart whether or
/// not the split setting is drawing them apart: a user who had calls going
/// through one device and everything else through another when the lock went
/// on gets both of those held, and turning the split on later does not change
/// what is being held.
/// </para>
/// <para>
/// A slot is null where nothing is remembered for it — a machine with no
/// input at all, a settings file written before this setting existed, or an id
/// a hand-edit blanked. Nothing is guarded there.
/// </para>
/// </remarks>
/// <param name="Output">The output everything plays through.</param>
/// <param name="Input">The input everything records from.</param>
/// <param name="CommunicationsOutput">The output calls go through.</param>
/// <param name="CommunicationsInput">The input calls are spoken into.</param>
internal readonly record struct LockedDefaults(
    string? Output,
    string? Input,
    string? CommunicationsOutput,
    string? CommunicationsInput)
{
    /// <summary>
    /// The id remembered for one kind and role, or null where none is.
    /// <see cref="DefaultRole.All"/> answers with the audio slot: the two
    /// hold the same device wherever one marker set them both, and the guard
    /// asks per role rather than for all three at once.
    /// </summary>
    internal string? For(DeviceKind kind, DefaultRole role) => (kind, role) switch
    {
        (DeviceKind.Output, DefaultRole.Communications) => CommunicationsOutput,
        (DeviceKind.Input, DefaultRole.Communications) => CommunicationsInput,
        (DeviceKind.Output, _) => Output,
        _ => Input,
    };

    /// <summary>
    /// The same selection with one device remembered for every role
    /// <paramref name="role"/> covers — so a click on the single marker,
    /// which sets all three of Windows' roles, is remembered for both of
    /// them.
    /// </summary>
    internal LockedDefaults With(DeviceKind kind, DefaultRole role, string deviceId)
    {
        var audio = role is DefaultRole.All or DefaultRole.Audio;
        var communications = role is DefaultRole.All or DefaultRole.Communications;

        return kind == DeviceKind.Output
            ? this with
            {
                Output = audio ? deviceId : Output,
                CommunicationsOutput = communications ? deviceId : CommunicationsOutput,
            }
            : this with
            {
                Input = audio ? deviceId : Input,
                CommunicationsInput = communications ? deviceId : CommunicationsInput,
            };
    }
}
