using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Domains.Dashboard.Models;

namespace RPV_Tracker.Controls
{
    /// <summary>Status badge using the brand's pill palette. Sizes itself to its text.</summary>
    internal class StatusPill : Control
    {
        private ItemStatus status = ItemStatus.Pending;

        public StatusPill()
        {
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Height = 22;
        }

        public ItemStatus Status
        {
            get { return status; }
            set
            {
                status = value;
                ResizeToText();
                Invalidate();
            }
        }

        private string Caption
        {
            get
            {
                switch (status)
                {
                    case ItemStatus.Approved: return "Approved";
                    case ItemStatus.Rejected: return "Rejected";
                    case ItemStatus.Draft: return "Draft";
                    case ItemStatus.Info: return "For review";
                    default: return "Pending";
                }
            }
        }

        private Color Background
        {
            get
            {
                switch (status)
                {
                    case ItemStatus.Approved: return Color.FromArgb(0xD4, 0xED, 0xDF);
                    case ItemStatus.Rejected: return Color.FromArgb(0xF5, 0xD4, 0xD4);
                    case ItemStatus.Draft: return Color.FromArgb(0xEE, 0xF2, 0xF5);
                    case ItemStatus.Info: return Color.FromArgb(0xD4, 0xE5, 0xF5);
                    default: return Color.FromArgb(0xF5, 0xE6, 0xD0);
                }
            }
        }

        private Color Foreground
        {
            get
            {
                switch (status)
                {
                    case ItemStatus.Approved: return Color.FromArgb(0x2D, 0x7A, 0x4F);
                    case ItemStatus.Rejected: return Color.FromArgb(0x8B, 0x20, 0x20);
                    case ItemStatus.Draft: return Color.FromArgb(0x5A, 0x70, 0x80);
                    case ItemStatus.Info: return Color.FromArgb(0x1C, 0x4A, 0x72);
                    default: return Color.FromArgb(0x8B, 0x5A, 0x1A);
                }
            }
        }

        private void ResizeToText()
        {
            Width = TextRenderer.MeasureText(Caption, RpvTheme.FontMicro).Width + RpvTheme.Space5;
        }

        protected override void OnHandleCreated(System.EventArgs e)
        {
            ResizeToText();
            base.OnHandleCreated(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(RpvTheme.BackdropFor(this));
            RpvTheme.EnableSmoothing(g);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RpvTheme.RoundedRect(bounds, Height / 2))
            using (var brush = new SolidBrush(Background))
            {
                g.FillPath(brush, path);
            }

            TextRenderer.DrawText(g, Caption, RpvTheme.FontMicro, bounds, Foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
