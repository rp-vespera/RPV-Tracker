using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;

namespace RPV_Tracker.Controls
{
    /// <summary>
    /// The standard app surface: white, 12px radius, hairline border, subtle shadow.
    /// The brand forbids coloured card backgrounds — use badges and accents inside instead.
    /// </summary>
    internal class CardPanel : Panel, ISurfaceProvider
    {
        /// <summary>Pixels reserved at the bottom-right for the shadow.</summary>
        private const int ShadowInset = 2;

        private Color surface = RpvTheme.CardSurface;
        private int cornerRadius = RpvTheme.RadiusLg;
        private bool drawBorder = true;

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Padding = new Padding(RpvTheme.Space5, 20, RpvTheme.Space5, 20);
        }

        /// <summary>Card fill. Stat surfaces use Mist; everything else stays White.</summary>
        public Color Surface
        {
            get { return surface; }
            set { surface = value; Invalidate(); }
        }

        public int CornerRadius
        {
            get { return cornerRadius; }
            set { cornerRadius = value; Invalidate(); }
        }

        public bool DrawBorder
        {
            get { return drawBorder; }
            set { drawBorder = value; Invalidate(); }
        }

        /// <summary>The colour children sit on — the card fill, not the transparent BackColor.</summary>
        public Color SurfaceColor
        {
            get { return surface; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(RpvTheme.BackdropFor(this));
            RpvTheme.EnableSmoothing(g);

            var card = new Rectangle(0, 0, Width - ShadowInset - 1, Height - ShadowInset - 1);

            // --rpv-shadow-sm, approximated with two low-alpha passes.
            for (int i = ShadowInset; i >= 1; i--)
            {
                var shadowRect = new Rectangle(card.X + 1, card.Y + i, card.Width, card.Height);
                using (var path = RpvTheme.RoundedRect(shadowRect, cornerRadius))
                using (var brush = new SolidBrush(Color.FromArgb(10, RpvTheme.BrandSurface)))
                {
                    g.FillPath(brush, path);
                }
            }

            using (var path = RpvTheme.RoundedRect(card, cornerRadius))
            {
                using (var brush = new SolidBrush(surface))
                {
                    g.FillPath(brush, path);
                }

                if (drawBorder)
                {
                    using (var pen = new Pen(RpvTheme.Border))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            base.OnPaint(e);
        }
    }
}
