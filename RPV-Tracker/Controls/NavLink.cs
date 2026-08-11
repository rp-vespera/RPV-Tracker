using System;
using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;

namespace RPV_Tracker.Controls
{
    /// <summary>
    /// A link in the Midnight nav bar: Stone at rest, White on hover, Ember when active.
    /// </summary>
    internal class NavLink : Control
    {
        private bool isActive;
        private bool hovered;

        public NavLink()
        {
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = RpvTheme.FontBodyMedium;
            Cursor = Cursors.Hand;
            Height = RpvTheme.NavHeight;
        }

        public bool IsActive
        {
            get { return isActive; }
            set { isActive = value; Invalidate(); }
        }

        /// <summary>Identifies which page this link opens.</summary>
        public string PageKey { get; set; }

        public void AutoWidth()
        {
            Width = TextRenderer.MeasureText(Text, Font).Width + RpvTheme.Space5;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            AutoWidth();
            base.OnTextChanged(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(RpvTheme.BackdropFor(this));
            RpvTheme.EnableSmoothing(g);

            Color color;
            if (isActive)
            {
                color = RpvTheme.Ember;
            }
            else if (hovered)
            {
                color = RpvTheme.OnAccent;
            }
            else
            {
                color = RpvTheme.Stone;
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
