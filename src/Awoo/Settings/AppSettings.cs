namespace Awoo.Settings;

/// <summary>
/// Everything awoo remembers between runs. A record rather than a mutable
/// object, so a change is a new value that can be compared with the old one —
/// which is what keeps the file from being rewritten for nothing.
/// </summary>
/// <remarks>
/// "Start with Windows" is deliberately not here: Windows keeps that, under
/// the user's Run key, and a copy of it in this file could only ever disagree
/// with the thing that actually decides. See <see cref="Autostart"/>.
/// </remarks>
internal sealed record AppSettings
{
    /// <summary>What the Plasma applet scrolls by, and so what awoo does.</summary>
    internal const int DefaultVolumeStep = 5;

    /// <summary>A step of nothing is not a step, and a quarter of the range is plenty.</summary>
    internal const int MinVolumeStep = 1;

    internal const int MaxVolumeStep = 25;

    /// <summary>
    /// How far one notch of the wheel over the tray icon moves the volume, in
    /// percentage points.
    /// </summary>
    public int VolumeStep { get; init; } = DefaultVolumeStep;

    /// <summary>
    /// Whether awoo has already had its one go at switching autostart on. On
    /// by default was settled with the human, and a default can only be
    /// applied once: anything else turns the setting back on behind somebody
    /// who turned it off.
    /// </summary>
    public bool AutostartRegistered { get; init; }

    /// <summary>
    /// The same settings with anything out of range brought back into it. The
    /// file is the user's to edit, and an edit does not have to be sensible.
    /// </summary>
    internal AppSettings Normalised() =>
        VolumeStep is >= MinVolumeStep and <= MaxVolumeStep
            ? this
            : this with { VolumeStep = Math.Clamp(VolumeStep, MinVolumeStep, MaxVolumeStep) };
}
