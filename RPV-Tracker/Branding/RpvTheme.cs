using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace RPV_Tracker.Branding
{
    /// <summary>
    /// Implemented by containers that paint their own surface. Owner-drawn children need
    /// the real colour behind them, which <see cref="Control.BackColor"/> reports as
    /// Transparent on such containers.
    /// </summary>
    internal interface ISurfaceProvider
    {
        Color SurfaceColor { get; }
    }

    /// <summary>
    /// RPV brand design tokens, ported from the rpv-brand skill's CSS custom
    /// properties so the desktop client matches the web app exactly.
    /// Hex values are taken verbatim from the brand system — never approximate them.
    /// </summary>
    internal static class RpvTheme
    {
        // ---------------------------------------------------------------- colors

        // Primary palette — the foundation
        public static readonly Color Midnight = Color.FromArgb(0x0D, 0x1B, 0x2A);
        public static readonly Color Slate = Color.FromArgb(0x1C, 0x3A, 0x52);
        public static readonly Color Steel = Color.FromArgb(0x2E, 0x5F, 0x80);
        public static readonly Color Horizon = Color.FromArgb(0x4A, 0x90, 0xB8);

        // Accent palette — the human warmth
        public static readonly Color Terracotta = Color.FromArgb(0xC4, 0x62, 0x2D);
        public static readonly Color Ember = Color.FromArgb(0xE0, 0x7B, 0x45);
        public static readonly Color Sand = Color.FromArgb(0xF0, 0xC8, 0x9A);

        // Neutral palette — the canvas
        public static readonly Color Cream = Color.FromArgb(0xFA, 0xF7, 0xF2);
        public static readonly Color Mist = Color.FromArgb(0xEE, 0xF2, 0xF5);
        public static readonly Color Stone = Color.FromArgb(0x8F, 0xA3, 0xB1);
        public static readonly Color Charcoal = Color.FromArgb(0x2C, 0x3E, 0x4A);
        public static readonly Color White = Color.FromArgb(0xFF, 0xFF, 0xFF);

        // Semantic / functional
        public static readonly Color ActionHover = Color.FromArgb(0xA8, 0x52, 0x1F);
        public static readonly Color Success = Color.FromArgb(0x2D, 0x7A, 0x4F);
        public static readonly Color Warning = Color.FromArgb(0xB8, 0x73, 0x33);
        public static readonly Color Danger = Color.FromArgb(0xB8, 0x32, 0x32);

        // Borders. GDI+ has no alpha compositing for 1px hairlines that reads well,
        // so rgba(44,62,74,0.12) is pre-flattened over white.
        public static readonly Color Border = Color.FromArgb(0xE6, 0xE8, 0xE9);
        public static readonly Color InputBorder = Color.FromArgb(0xD0, 0xDB, 0xE3);

        // --------------------------------------------------------------- spacing

        // 8pt grid — every spacing value is a multiple of the base unit.
        public const int Space1 = 4;
        public const int Space2 = 8;
        public const int Space3 = 12;
        public const int Space4 = 16;
        public const int Space5 = 24;
        public const int Space6 = 32;
        public const int Space7 = 48;
        public const int Space8 = 64;

        public const int RadiusSm = 4;
        public const int RadiusMd = 8;
        public const int RadiusLg = 12;
        public const int RadiusXl = 16;

        public const int NavHeight = 56;
        public const int ContentMaxWidth = 1200;
        public const int FormMaxWidth = 800;
        public const int InputHeight = 40;

        // ------------------------------------------------------------ typography

        /// <summary>Resolved body family: Inter when installed, else the brand fallback stack.</summary>
        public static readonly string BaseFamily;

        /// <summary>
        /// Family used for headings. The brand caps weight at 600, but GDI+ FontStyle.Bold
        /// maps to 700 — so a real semibold family is preferred when one is installed.
        /// </summary>
        public static readonly string HeadingFamily;

        private static readonly bool HeadingNeedsBoldStyle;

        static RpvTheme()
        {
            string[] installed;
            using (var collection = new InstalledFontCollection())
            {
                installed = collection.Families.Select(f => f.Name).ToArray();
            }

            BaseFamily = FirstInstalled(installed, "Inter", "DM Sans", "Segoe UI", "Tahoma")
                         ?? FontFamily.GenericSansSerif.Name;

            string semibold = FirstInstalled(installed, "Inter SemiBold", "Inter Semi Bold", "Segoe UI Semibold");
            HeadingFamily = semibold ?? BaseFamily;
            HeadingNeedsBoldStyle = semibold == null;
        }

        private static string FirstInstalled(string[] installed, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (installed.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static Font Heading(float points)
        {
            return new Font(HeadingFamily, points,
                HeadingNeedsBoldStyle ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
        }

        private static Font Body(float points, FontStyle style = FontStyle.Regular)
        {
            return new Font(BaseFamily, points, style, GraphicsUnit.Point);
        }

        // The brand type scale is expressed in px; WinForms works in points (px * 0.75 at 96dpi).
        public static readonly Font FontDisplay = Heading(30f);    // 40px
        public static readonly Font FontH1 = Heading(21f);         // 28px
        public static readonly Font FontH2 = Heading(16.5f);       // 22px
        public static readonly Font FontH3 = Heading(13.5f);       // 18px
        public static readonly Font FontBodyLarge = Body(12f);     // 16px
        public static readonly Font FontBody = Body(10.5f);        // 14px
        public static readonly Font FontBodyMedium = Heading(10.5f);
        public static readonly Font FontCaption = Body(9f);        // 12px
        public static readonly Font FontMicro = Heading(8.25f);    // 11px
        public static readonly Font FontStatNumber = Heading(19.5f); // 26px

        // -------------------------------------------------------------- painting

        /// <summary>Rounded-rectangle path, clamped so the radius can never exceed the box.</summary>
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = Math.Max(0, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            var path = new GraphicsPath();

            if (d == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Draws text with manual letter spacing. The brand asks for 0.06–0.12em tracking on
        /// all-caps labels and eyebrows, which GDI+ does not support natively.
        /// </summary>
        public static void DrawTracked(Graphics g, string text, Font font, Color color, Point origin, float tracking)
        {
            float x = origin.X;
            foreach (char c in text)
            {
                string s = c.ToString();
                TextRenderer.DrawText(g, s, font, new Point((int)Math.Round(x), origin.Y), color,
                    TextFormatFlags.NoPadding);
                x += TextRenderer.MeasureText(g, s, font, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding).Width + tracking;
            }
        }

        public static int MeasureTracked(string text, Font font, float tracking)
        {
            float width = 0;
            foreach (char c in text)
            {
                width += TextRenderer.MeasureText(c.ToString(), font, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding).Width + tracking;
            }
            return (int)Math.Ceiling(Math.Max(0, width - tracking));
        }

        /// <summary>
        /// The opaque colour actually behind <paramref name="control"/>. Owner-drawn controls
        /// clear to this before painting; using Parent.BackColor directly would clear to
        /// transparent black inside a card or any other self-painting container.
        /// </summary>
        public static Color BackdropFor(Control control)
        {
            Control parent = control == null ? null : control.Parent;

            while (parent != null)
            {
                var surface = parent as ISurfaceProvider;
                if (surface != null)
                {
                    return surface.SurfaceColor;
                }

                if (parent.BackColor.A == 255)
                {
                    return parent.BackColor;
                }

                parent = parent.Parent;
            }

            return Cream;
        }

        public static void EnableSmoothing(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
    }
}
