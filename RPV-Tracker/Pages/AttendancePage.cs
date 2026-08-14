using System;
using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;
using RPV_Tracker.Domains.TimeTracking.Services;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Pages
{
    /// <summary>
    /// Today's TAPS attendance record for the signed-in employee — the same
    /// GET /v1/hr/taps-sync/attendance-check the Time tracker tab uses to gate its
    /// overtime option, surfaced here as its own read-only screen.
    /// </summary>
    internal class AttendancePage : UserControl
    {
        private const int TitleHeight = 32;
        private const int SubtitleHeight = 32;
        private const int HeaderHeight = 34;
        private const int RowHeight = 44;
        private const int MessageHeight = 80;

        private readonly Panel scrollHost;
        private readonly Panel content;
        private readonly Label titleLabel;
        private readonly RpvButton refreshLink;
        private readonly Label subtitleLabel;
        private readonly CardPanel statusCard;

        private int statusCardHeight = HeaderHeight + MessageHeight;
        private bool loading;

        public AttendancePage()
        {
            BackColor = RpvTheme.Cream;
            DoubleBuffered = true;

            scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = RpvTheme.Cream };
            content = new Panel { BackColor = RpvTheme.Cream };

            titleLabel = MakeLabel(RpvTheme.FontH1, RpvTheme.HeadingText, "Attendance", TitleHeight);
            refreshLink = new RpvButton { Text = "Refresh", Variant = RpvButtonVariant.Tertiary, Size = new Size(120, 28) };
            refreshLink.Click += (s, e) => LoadAsync();
            subtitleLabel = MakeLabel(RpvTheme.FontBody, RpvTheme.Stone,
                "Today's TAPS attendance record, per HR.", SubtitleHeight);

            statusCard = new CardPanel();

            content.Controls.Add(titleLabel);
            content.Controls.Add(refreshLink);
            content.Controls.Add(subtitleLabel);
            content.Controls.Add(statusCard);

            scrollHost.Controls.Add(content);
            Controls.Add(scrollHost);

            scrollHost.Resize += (s, e) => LayoutContent();

            RenderMessage("Checking today's attendance…");
            LayoutContent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadAsync();
        }

        private async void LoadAsync()
        {
            if (loading)
            {
                return;
            }

            loading = true;
            refreshLink.Enabled = false;
            RenderMessage("Checking today's attendance…");
            LayoutContent();

            AttendanceCheckResult result = null;
            string error = null;
            try
            {
                result = await AttendanceService.CheckAsync(DateTime.Today);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                DebugLog.Exception("attendance", ex);
            }

            if (IsDisposed)
            {
                return;
            }

            loading = false;
            refreshLink.Enabled = true;

            if (error != null)
            {
                RenderMessage(error);
            }
            else
            {
                RenderStatus(result);
            }

            LayoutContent();
        }

        // --------------------------------------------------------------- rendering

        private void RenderMessage(string text)
        {
            statusCard.SuspendLayout();
            statusCard.Controls.Clear();

            var message = new Label
            {
                Height = MessageHeight,
                Dock = DockStyle.Top,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Stone,
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter
            };
            statusCard.Controls.Add(message);

            Label header = MakeCardHeader("Today — " + DateTime.Today.ToString("d MMM yyyy"));
            header.Dock = DockStyle.Top;
            statusCard.Controls.Add(header);

            statusCardHeight = HeaderHeight + MessageHeight + statusCard.Padding.Vertical;
            statusCard.ResumeLayout();
        }

        private void RenderStatus(AttendanceCheckResult result)
        {
            statusCard.SuspendLayout();
            statusCard.Controls.Clear();

            string[] labels =
            {
                "Attendance on file",
                "Worked morning",
                "Worked afternoon",
                "Eligible for overtime"
            };
            bool[] values =
            {
                result.HasAttendance,
                result.WorkedMorning,
                result.WorkedAfternoon,
                result.CanRequestOvertime
            };

            for (int i = labels.Length - 1; i >= 0; i--)
            {
                Panel row = BuildRow(labels[i], values[i], i < labels.Length - 1);
                row.Dock = DockStyle.Top;
                statusCard.Controls.Add(row);
            }

            Label header = MakeCardHeader("Today — " + DateTime.Today.ToString("d MMM yyyy"));
            header.Dock = DockStyle.Top;
            statusCard.Controls.Add(header);

            statusCardHeight = HeaderHeight + (RowHeight * labels.Length) + statusCard.Padding.Vertical;
            statusCard.ResumeLayout();
        }

        private static Panel BuildRow(string label, bool value, bool withDivider)
        {
            var row = new Panel { Height = RowHeight, BackColor = RpvTheme.CardSurface };
            if (withDivider)
            {
                row.Paint += (s, e) =>
                {
                    using (var pen = new Pen(RpvTheme.Border))
                    {
                        e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
                    }
                };
            }

            var name = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Charcoal,
                Bounds = new Rectangle(0, 0, 260, RowHeight),
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var badge = new Label
            {
                AutoSize = false,
                BackColor = value ? RpvTheme.Success : RpvTheme.Danger,
                ForeColor = RpvTheme.OnAccent,
                Font = RpvTheme.FontMicro,
                Text = value ? "YES" : "NO",
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, (RowHeight - 22) / 2, 56, 22)
            };

            row.Controls.Add(badge);
            row.Controls.Add(name);

            row.Resize += (s, e) =>
            {
                badge.Left = Math.Max(name.Width + RpvTheme.Space3, row.ClientSize.Width - badge.Width - RpvTheme.Space4);
            };

            return row;
        }

        // --------------------------------------------------------------- layout

        private void LayoutContent()
        {
            int available = scrollHost.ClientSize.Width;
            if (available <= 0)
            {
                return;
            }

            int margin = available < 720 ? RpvTheme.Space4 : RpvTheme.Space6;
            int width = Math.Min(RpvTheme.ContentMaxWidth, available - (margin * 2));
            if (width < 320)
            {
                width = 320;
            }

            content.Left = Math.Max(margin, (available - width) / 2);
            content.Top = RpvTheme.Space5;
            content.Width = width;

            titleLabel.SetBounds(0, 0, width - 140, TitleHeight);
            refreshLink.SetBounds(width - 130, 2, 130, 28);
            subtitleLabel.SetBounds(0, TitleHeight + 4, width, SubtitleHeight);

            int y = TitleHeight + 4 + SubtitleHeight + RpvTheme.Space4;
            statusCard.SetBounds(0, y, width, statusCardHeight);

            content.Height = y + statusCardHeight + RpvTheme.Space5;

            scrollHost.AutoScrollMinSize = new Size(0, content.Height);
        }

        // --------------------------------------------------------------- helpers

        private static Label MakeLabel(Font font, Color color, string text, int height)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.Cream,
                Font = font,
                ForeColor = color,
                Text = text,
                Height = height,
                UseMnemonic = false,
                TextAlign = ContentAlignment.TopLeft
            };
        }

        private static Label MakeCardHeader(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontH3,
                ForeColor = RpvTheme.HeadingText,
                Height = HeaderHeight,
                Text = text,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }
    }
}
