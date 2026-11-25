using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Awoo.Tray;

/// <summary>
/// Paints one tray speaker into a bitmap: the glyph drawing on its own, with
/// nothing in it that has to ask Windows anything.
/// </summary>
/// <remarks>
/// Separate from <see cref="TrayIconRenderer"/> so the drawing can be run —
/// and its output measured against a screenshot of the real taskbar —
/// without a taskbar, a DPI or a theme to read.
/// </remarks>
internal static class TrayIconPainter
{
    /// <summary>How many times the outline is filled.</summary>
    /// <remarks>
    /// Twice. One fill of the designer's outline measures 1.52px of stem
    /// where the tray's own speaker measures 1.75, so the glyph reads lighter
    /// than the icons beside it however faithfully it is drawn. A second fill
    /// of the same path changes nothing inside the glyph, where the first is
    /// already solid; it strengthens the antialiased edge alone, taking a
    /// pixel at <c>c</c> to <c>1-(1-c)²</c>. That is where the weight is at
    /// this size, and it takes the stem to 1.76 against the tray's 1.75.
    ///
    /// Both of the obvious ways to add weight overshoot instead. A pen drawn
    /// over the fill does, because GDI+ renders any sub-pixel pen as a
    /// hairline and adds about half a pixel whatever width it is told; and
    /// <c>Widen</c>, which puts the stroke into the geometry where there is
    /// no hairline rule to round it up, lands on 2.00px for every width from
    /// 0.15 upward just the same.
    /// </remarks>
    private const int Fills = 2;

    /// <summary>
    /// An icon-sized bitmap holding <paramref name="glyph"/>, with
    /// <paramref name="ghost"/> faintly behind it. The caller owns it.
    /// </summary>
    /// <param name="glyph">The speaker for this volume, drawn at full strength.</param>
    /// <param name="ghost">
    /// The speaker whose waves are all there, drawn faintly underneath so the
    /// waves above the current level show the way Windows shows them: present
    /// but off. Null for mute, which Windows draws with no waves at all.
    /// </param>
    /// <param name="size">The icon's side, in pixels.</param>
    /// <param name="fontFamily">The icon font to take the outlines from.</param>
    /// <param name="colour">What the glyph is drawn in, alpha included.</param>
    /// <param name="ghostColour">The same for the ghost: the same colour, far more transparent.</param>
    internal static Bitmap Paint(
        string glyph,
        string? ghost,
        int size,
        string fontFamily,
        Color colour,
        Color ghostColour)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            using var family = new FontFamily(fontFamily);

            graphics.Clear(Color.Transparent);

            // The glyph is filled as a path rather than drawn as a string,
            // which is what the weight comes from. Drawn as a string, GDI+
            // grid-fits the outline — it snaps the speaker's stems to whole
            // pixels, and at tray size that rounds a stem of just under a
            // pixel down to exactly one, which is the thinness the icon was
            // reported for. The path is the designer's outline unrounded,
            // and it measures within a tenth of a pixel of what the Windows
            // tray draws beside it.
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (ghost is not null)
            {
                Fill(graphics, family, ghost, size, ghostColour);
            }

            Fill(graphics, family, glyph, size, colour);
        }
        catch (Exception)
        {
            bitmap.Dispose();
            throw;
        }

        return bitmap;
    }

    /// <summary>Fills one glyph's outline over the whole bitmap.</summary>
    /// <remarks>
    /// Every glyph of this font is exactly em-square: its advance width, its
    /// line height and its cell ascent are all one em, and the cell is what
    /// the designer drew the icon to fill. So an em of exactly the bitmap's
    /// size, from the origin, puts the glyph's own grid over the bitmap's
    /// with no fractional offset left anywhere, and the speakers of the five
    /// volume glyphs land on each other — which is what lets the ghost be
    /// drawn underneath rather than beside.
    /// </remarks>
    private static void Fill(Graphics graphics, FontFamily family, string glyph, int size, Color colour)
    {
        // Typographic rather than the default format, which pads a string
        // with the font's overhang allowance and would push the glyph off
        // that grid.
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
        format.Alignment = StringAlignment.Near;
        format.LineAlignment = StringAlignment.Near;

        using var path = new GraphicsPath();
        using var brush = new SolidBrush(colour);

        // This font's cell is its em exactly — one em of ascent and no
        // descent — so an em of N covers exactly N pixels from the origin,
        // with no line-spacing allowance to take off it.
        path.AddString(glyph, family, (int)FontStyle.Regular, size, PointF.Empty, format);

        for (var pass = 0; pass < Fills; pass++)
        {
            graphics.FillPath(brush, path);
        }
    }
}
