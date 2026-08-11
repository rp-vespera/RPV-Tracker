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
        // ---------------------------------------------------------------- theme mode

        /// <summary>
        /// Raised after <see cref="ApplyMode"/> reassigns every mutable token below.
        /// Windows already on screen don't repaint themselves automatically — a listener
        /// must re-skin its own literal colour assignments and, for pages built once in a
        /// constructor, rebuild the page. See MainForm's subscription for the pattern.
        /// </summary>
        public static event EventHandler ThemeChanged;

        public static bool IsDarkMode { get; private set; }

        // ---------------------------------------------------------- fixed brand colors
        //
        // These never change with theme. They're either the brand's dark chrome surface
        // (nav bar, the login screen's brand panel) or a mark/text that always sits on
        // that fixed dark surface or on the Terracotta accent — flipping them with the
        // page theme would make them illegible rather than adaptive.

        /// <summary>The brand's dark chrome surface — nav bar, login brand panel. Was "Midnight".</summary>
        public static readonly Color BrandSurface = Color.FromArgb(0x0D, 0x1B, 0x2A);

        /// <summary>Text/marks that sit on BrandSurface or an accent fill and must stay light. Was "White".</summary>
        public static readonly Color OnAccent = Color.FromArgb(0xFF, 0xFF, 0xFF);

        public static readonly Color Terracotta = Color.FromArgb(0xC4, 0x62, 0x2D);
        public static readonly Color Ember = Color.FromArgb(0xE0, 0x7B, 0x45);
        public static readonly Color Sand = Color.FromArgb(0xF0, 0xC8, 0x9A);
        public static readonly Color ActionHover = Color.FromArgb(0xA8, 0x52, 0x1F);
        public static readonly Color Horizon = Color.FromArgb(0x4A, 0x90, 0xB8);

        /// <summary>Muted secondary text. Reads clearly on light content, dark content, and BrandSurface alike.</summary>
        public static readonly Color Stone = Color.FromArgb(0x8F, 0xA3, 0xB1);

        // ------------------------------------------------------- theme-mutable colors
        //
        // Everything below flips between the light and dark palette in ApplyMode. Pages
        // and controls read these fields directly, the same way they always have — only
        // the value behind the name now depends on the active mode.

        /// <summary>Page background.</summary>
        public static Color Cream;

        /// <summary>Card, input, and row fill. Literal white in light mode, elevated dark slate in dark mode. Was "White" used as a surface.</summary>
        public static Color CardSurface;

        /// <summary>Secondary fill — stat tiles, placeholder art.</summary>
        public static Color Mist;

        /// <summary>Body text.</summary>
        public static Color Charcoal;

        /// <summary>Heading and headline-figure text on a page or card surface. Was "Midnight" used as text.</summary>
        public static Color HeadingText;

        /// <summary>Hairline divider, 1px.</summary>
        public static Color Border;

        /// <summary>Input field resting border.</summary>
        public static Color InputBorder;

        /// <summary>Secondary-button text/border and similar mid-emphasis content accents.</summary>
        public static Color Slate;

        /// <summary>Tertiary-button text, focus rings, low-emphasis content accents.</summary>
        public static Color Steel;

        public static Color Success;
        public static Color Warning;
        public static Color Danger;

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

            ApplyMode(false);
        }

        /// <summary>
        /// Reassigns every theme-mutable token above to the light or dark palette and
        /// raises <see cref="ThemeChanged"/>. Controls already on screen keep whatever
        /// colour they captured at construction time — callers must re-skin or rebuild.
        /// </summary>
        public static void ApplyMode(bool dark)
        {
            IsDarkMode = dark;

            if (dark)
            {
                Cream = Color.FromArgb(0x11, 0x18, 0x1F);
                CardSurface = Color.FromArgb(0x1B, 0x24, 0x2D);
                Mist = Color.FromArgb(0x23, 0x2E, 0x39);
                Charcoal = Color.FromArgb(0xD3, 0xDA, 0xE0);
                HeadingText = Color.FromArgb(0xF3, 0xF6, 0xF8);
                Border = Color.FromArgb(0x2B, 0x36, 0x42);
                InputBorder = Color.FromArgb(0x3A, 0x4A, 0x59);
                Slate = Color.FromArgb(0xA9, 0xC7, 0xDE);
                Steel = Color.FromArgb(0x7F, 0xB3, 0xD9);
                Success = Color.FromArgb(0x4F, 0xBE, 0x82);
                Warning = Color.FromArgb(0xE0, 0xA4, 0x58);
                Danger = Color.FromArgb(0xE5, 0x6B, 0x6B);
            }
            else
            {
                Cream = Color.FromArgb(0xFA, 0xF7, 0xF2);
                CardSurface = Color.FromArgb(0xFF, 0xFF, 0xFF);
                Mist = Color.FromArgb(0xEE, 0xF2, 0xF5);
                Charcoal = Color.FromArgb(0x2C, 0x3E, 0x4A);
                HeadingText = Color.FromArgb(0x0D, 0x1B, 0x2A);
                Border = Color.FromArgb(0xE6, 0xE8, 0xE9);
                InputBorder = Color.FromArgb(0xD0, 0xDB, 0xE3);
                Slate = Color.FromArgb(0x1C, 0x3A, 0x52);
                Steel = Color.FromArgb(0x2E, 0x5F, 0x80);
                Success = Color.FromArgb(0x2D, 0x7A, 0x4F);
                Warning = Color.FromArgb(0xB8, 0x73, 0x33);
                Danger = Color.FromArgb(0xB8, 0x32, 0x32);
            }

            EventHandler handler = ThemeChanged;
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
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
