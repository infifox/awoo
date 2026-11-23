namespace Awoo.Audio;

/// <summary>
/// How loud something is, in the bands an icon has to distinguish rather than
/// the percentage it came from.
/// </summary>
internal enum VolumeLevel
{
    /// <summary>Muted, whatever the volume behind it.</summary>
    Muted,

    /// <summary>Not muted, but at zero.</summary>
    Silent,

    Low,
    Medium,
    High,
}

/// <summary>
/// Everything awoo shows about one output: a snapshot rather than a live
/// object, so a reader never sees half of a change.
/// </summary>
/// <param name="DeviceName">The endpoint's friendly name, as Windows shows it.</param>
/// <param name="Percent">Volume, 0–100. Capped at 100: over-amplification is out of scope.</param>
/// <param name="IsMuted">Whether the endpoint is muted.</param>
/// <param name="HasDevice">False when the machine has no active output device at all.</param>
internal readonly record struct VolumeState(string DeviceName, int Percent, bool IsMuted, bool HasDevice)
{
    /// <summary>The lowest and highest a volume can be, as the UI and the service both see it.</summary>
    internal const int MinPercent = 0;

    internal const int MaxPercent = 100;

    /// <summary>What there is to show when no output device is available.</summary>
    internal static VolumeState NoDevice { get; } = new("No output device", 0, true, false);

    internal VolumeLevel Level => this switch
    {
        { HasDevice: false } => VolumeLevel.Muted,
        { IsMuted: true } => VolumeLevel.Muted,
        { Percent: <= 0 } => VolumeLevel.Silent,
        { Percent: <= 33 } => VolumeLevel.Low,
        { Percent: <= 66 } => VolumeLevel.Medium,
        _ => VolumeLevel.High,
    };

    /// <summary>A scalar 0.0–1.0 is what Core Audio takes; a percentage is what people read.</summary>
    internal static int ToPercent(float scalar) =>
        Math.Clamp((int)Math.Round(scalar * 100f), MinPercent, MaxPercent);

    internal static float ToScalar(int percent) =>
        Math.Clamp(percent, MinPercent, MaxPercent) / 100f;
}
