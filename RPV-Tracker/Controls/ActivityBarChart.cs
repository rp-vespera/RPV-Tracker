using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;

namespace RPV_Tracker.Controls
{
    /// <summary>Owner-drawn bar chart: activity percentage per bucket (typically one hour).</summary>
    internal class ActivityBarChart : Control
    {
        public class Bucket
        {
            public string Label;
            public int Percent;
        }

        private const int LeftAxis = 36;
        private const int BottomAxis = 22;
        private const int TopPad = 14;

        private List<Bucket> buckets = new List<Bucket>();
        private string emptyText = "Activity per hour will appear here once an interval completes.";

        public ActivityBarChart()
        {
            SetStyle(ControlStyles.UserPaint
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
        }

        public string EmptyText
        {
            get { return emptyText; }
            set { emptyText = value ?? string.Empty; Invalidate(); }
        }

        public void SetData(IEnumerable<Bucket> data)
        {
            buckets = new List<Bucket>(data ?? new Bucket[0]);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(RpvTheme.BackdropFor(this));
            RpvTheme.EnableSmoothing(g);

            if (buckets.Count == 0)
            {
                TextRenderer.DrawText(g, emptyText, RpvTheme.FontBody, ClientRectangle, RpvTheme.Stone,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }

            int plotWidth = Width - LeftAxis - RpvTheme.Space2;
            int plotHeight = Height - BottomAxis - TopPad;
            if (plotWidth <= 0 || plotHeight <= 0)
            {
                return;
            }

            int plotLeft = LeftAxis;
            int plotBottom = TopPad + plotHeight;

            for (int pct = 0; pct <= 100; pct += 25)
            {
                int y = plotBottom - (int)Math.Round(plotHeight * (pct / 100.0));
                using (var pen = new Pen(RpvTheme.Border))
                {
                    g.DrawLine(pen, plotLeft, y, Width, y);
                }
                TextRenderer.DrawText(g, pct + "%", RpvTheme.FontMicro,
                    new Rectangle(0, y - 8, LeftAxis - 6, 16), RpvTheme.Stone,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            int count = buckets.Count;

            // A day of 5-minute intervals is ~100 bars where an hourly chart has 8. The gap
            // closes up as bars multiply, otherwise the gaps eat the plot and every bar
            // collapses to the 6px floor.
            int gap = count > 48 ? 2 : count > 24 ? 4 : count > 12 ? 6 : 10;
            double barWidth = Math.Max(2, (plotWidth - (gap * (count + 1))) / (double)count);

            // Labels are drawn on a stride so they thin out instead of overprinting each
            // other — every bar still draws, only its label is periodic.
            int labelStride = 1;
            int labelRoom = Math.Max(1, plotWidth / 48);
            if (count > labelRoom)
            {
                labelStride = (int)Math.Ceiling(count / (double)labelRoom);
            }

            for (int i = 0; i < count; i++)
            {
                Bucket bucket = buckets[i];
                int percent = Math.Max(0, Math.Min(100, bucket.Percent));
                int barHeight = (int)Math.Round(plotHeight * (percent / 100.0));
                int x = plotLeft + gap + (int)Math.Round(i * (barWidth + gap));
                int width = (int)Math.Round(barWidth);
                var barRect = new Rectangle(x, plotBottom - barHeight, width, barHeight);

                Color barColor = percent >= 60 ? RpvTheme.Success
                    : percent >= 30 ? RpvTheme.Warning
                    : RpvTheme.Danger;

                if (barRect.Height > 1)
                {
                    using (var path = RpvTheme.RoundedRect(barRect, 4))
                    using (var brush = new SolidBrush(barColor))
                    {
                        g.FillPath(brush, path);
                    }
                }

                if (i % labelStride == 0)
                {
                    // Labels are centred on their bar but allowed to overhang it, so a wide
                    // "10:45 AM" still reads when the bar under it is only a few pixels.
                    const int labelWidth = 64;
                    TextRenderer.DrawText(g, bucket.Label, RpvTheme.FontMicro,
                        new Rectangle(x + (width / 2) - (labelWidth / 2), plotBottom + 4, labelWidth, BottomAxis - 4),
                        RpvTheme.Stone, TextFormatFlags.HorizontalCenter);
                }
            }
        }
    }
}
