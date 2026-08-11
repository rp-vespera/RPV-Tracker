using System;
using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;

namespace RPV_Tracker.Controls
{
    /// <summary>
    /// A pill-shaped, single-select segmented control — the settings page's stand-in for a
    /// row of radio buttons. Owner-drawn so it shares the brand's rounded, shadow-free
    /// visual language instead of looking like a native Win32 control.
    /// </summary>
    internal class SegmentedControl : Control
    {
        private string[] options = new string[0];
        private int selectedIndex = -1;
        private int hoverIndex = -1;

        public SegmentedControl()
        {
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = RpvTheme.FontBodyMedium;
            Cursor = Cursors.Hand;
            Height = 40;
        }

        /// <summary>Raised after the user picks a different segment.</summary>
        public event EventHandler SelectedIndexChanged;

        public string[] Options
        {
            get { return options; }
            set
            {
                options = value ?? new string[0];
                if (selectedIndex >= options.Length)
                {
                    selectedIndex = options.Length - 1;
                }
                Invalidate();
            }
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value == selectedIndex || value < 0 || value >= options.Length)
                {
                    return;
                }
                selectedIndex = value;
                Invalidate();
                OnSelectedIndexChanged(EventArgs.Empty);
            }
        }

        /// <summary>Sets the selection without raising <see cref="SelectedIndexChanged"/> — for loading a saved value.</summary>
        public void SetSelectedIndexSilently(int index)
        {
            if (index < 0 || index >= options.Length)
            {
                return;
            }
            selectedIndex = index;
            Invalidate();
        }

        protected virtual void OnSelectedIndexChanged(EventArgs e)
        {
            EventHandler handler = SelectedIndexChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = IndexAt(e.X);
            if (index != hoverIndex)
            {
                hoverIndex = index;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int index = IndexAt(e.X);
            if (index >= 0)
            {
                SelectedIndex = index;
            }
            base.OnMouseClick(e);
        }

        private int IndexAt(int x)
        {
            if (options.Length == 0 || Width <= 0)
            {
                return -1;
            }
            int segmentWidth = Width / options.Length;
            int index = segmentWidth == 0 ? 0 : x / segmentWidth;
            return Math.Max(0, Math.Min(options.Length - 1, index));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(RpvTheme.BackdropFor(this));
            RpvTheme.EnableSmoothing(g);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var trackPath = RpvTheme.RoundedRect(bounds, Height / 2))
            using (var trackBrush = new SolidBrush(RpvTheme.Mist))
            using (var trackPen = new Pen(RpvTheme.Border))
            {
                g.FillPath(trackBrush, trackPath);
                g.DrawPath(trackPen, trackPath);
            }

            if (options.Length == 0)
            {
                return;
            }

            int segmentWidth = Width / options.Length;

            if (selectedIndex >= 0)
            {
                const int inset = 3;
                var pill = new Rectangle(selectedIndex * segmentWidth + inset, inset,
                    segmentWidth - (inset * 2), Height - (inset * 2));
                using (var path = RpvTheme.RoundedRect(pill, pill.Height / 2))
                using (var brush = new SolidBrush(RpvTheme.Terracotta))
                {
                    g.FillPath(brush, path);
                }
            }

            for (int i = 0; i < options.Length; i++)
            {
                int segmentX = i * segmentWidth;
                var segment = new Rectangle(segmentX, 0,
                    i == options.Length - 1 ? Width - segmentX : segmentWidth, Height);
                Color text = i == selectedIndex
                    ? RpvTheme.OnAccent
                    : (i == hoverIndex ? RpvTheme.HeadingText : RpvTheme.Charcoal);

                TextRenderer.DrawText(g, options[i], Font, segment, text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
