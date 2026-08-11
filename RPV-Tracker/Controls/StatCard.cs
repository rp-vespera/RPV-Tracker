using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;

namespace RPV_Tracker.Controls
{
    /// <summary>
    /// Data / stat card: Mist surface, large Midnight figure, uppercase Stone label.
    /// The accent variant renders the figure in Terracotta.
    /// </summary>
    internal class StatCard : Control
    {
        private const float LabelTracking = 0.8f;   // ~0.06em at 11px

        private string label = "Label";
        private string value = "0";
        private bool isAccent;

        public StatCard()
        {
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(240, 96);
        }

        public string Label
        {
            get { return label; }
            set { label = value; Invalidate(); }
        }

        public string Value
        {
            get { return value; }
            set { this.value = value; Invalidate(); }
        }

        public bool IsAccent
        {
            get { return isAccent; }
            set { isAccent = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(RpvTheme.BackdropFor(this));
            RpvTheme.EnableSmoothing(g);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RpvTheme.RoundedRect(bounds, RpvTheme.RadiusLg))
            using (var brush = new SolidBrush(RpvTheme.Mist))
            {
                g.FillPath(brush, path);
            }

            RpvTheme.DrawTracked(g, (label ?? string.Empty).ToUpperInvariant(), RpvTheme.FontMicro,
                RpvTheme.Stone, new Point(RpvTheme.Space5, 20), LabelTracking);

            TextRenderer.DrawText(g, value, RpvTheme.FontStatNumber,
                new Point(RpvTheme.Space5 - 2, 42),
                isAccent ? RpvTheme.Terracotta : RpvTheme.HeadingText,
                TextFormatFlags.NoPadding);
        }
    }
}
