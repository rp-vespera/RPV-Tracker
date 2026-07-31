using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;

namespace RPV_Tracker.Controls
{
    /// <summary>
    /// The RPV symbol: the wordmark set tight, followed by an Ember dot.
    /// The dot signals energy and continuity and is never recoloured.
    /// </summary>
    internal class RpvLogo : Control
    {
        private float pointSize = 15f;
        private Color wordmarkColor = RpvTheme.White;

        public RpvLogo()
        {
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(70, 28);
        }

        public float PointSize
        {
            get { return pointSize; }
            set { pointSize = value; Invalidate(); }
        }

        /// <summary>White on Midnight surfaces, Midnight on Cream or White ones.</summary>
        public Color WordmarkColor
        {
            get { return wordmarkColor; }
            set { wordmarkColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(RpvTheme.BackdropFor(this));
            RpvTheme.EnableSmoothing(g);

            using (var font = new Font(RpvTheme.HeadingFamily, pointSize, FontStyle.Bold, GraphicsUnit.Point))
            {
                // Tight tracking, so the letters are drawn as one run and only the dot is offset.
                Size mark = TextRenderer.MeasureText(g, "RPV", font, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding);
                int top = (Height - mark.Height) / 2;

                TextRenderer.DrawText(g, "RPV", font, new Point(0, top), wordmarkColor, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, "·", font, new Point(mark.Width + 1, top), RpvTheme.Ember,
                    TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>
    /// The monogram mark: initials in a filled circle. Used for avatars and app icons —
    /// Terracotta fill with White text per the logo rules.
    /// </summary>
    internal class Monogram : Control
    {
        private string initials = "RPV";

        public Monogram()
        {
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(32, 32);
        }

        public string Initials
        {
            get { return initials; }
            set { initials = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(RpvTheme.BackdropFor(this));
            RpvTheme.EnableSmoothing(g);

            var circle = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var brush = new SolidBrush(RpvTheme.Terracotta))
            {
                g.FillEllipse(brush, circle);
            }

            TextRenderer.DrawText(g, initials, RpvTheme.FontMicro, circle, RpvTheme.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
