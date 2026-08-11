using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;
using RPV_Tracker.Domains.TimeTracking.Services;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Pages
{
    /// <summary>
    /// Log of every finished tracking session — when it started and when it finished — read
    /// from <see cref="TaskHistoryStore"/> so it survives across app restarts.
    /// </summary>
    internal class TaskHistoryPage : UserControl
    {
        private const int TitleHeight = 32;
        private const int SubtitleHeight = 32;
        private const int HeaderHeight = 34;
        private const int RowHeight = 56;
        private const int EmptyHeight = 120;

        private readonly TimeTrackingService service;

        private readonly Panel scrollHost;
        private readonly Panel content;
        private readonly Label titleLabel;
        private readonly RpvButton refreshLink;
        private readonly Label subtitleLabel;
        private readonly CardPanel listCard;

        private int listCardHeight = HeaderHeight + EmptyHeight;

        public TaskHistoryPage(TimeTrackingService trackingService)
        {
            service = trackingService;
            BackColor = RpvTheme.Cream;
            DoubleBuffered = true;

            scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = RpvTheme.Cream };
            content = new Panel { BackColor = RpvTheme.Cream };

            titleLabel = MakeLabel(RpvTheme.FontH1, RpvTheme.HeadingText, "Task history", TitleHeight);
            refreshLink = new RpvButton { Text = "Refresh", Variant = RpvButtonVariant.Tertiary, Size = new Size(120, 28) };
            refreshLink.Click += (s, e) => { RenderList(); LayoutContent(); };
            subtitleLabel = MakeLabel(RpvTheme.FontBody, RpvTheme.Stone,
                "When each tracked session started and finished, newest first.", SubtitleHeight);

            listCard = new CardPanel();

            content.Controls.Add(titleLabel);
            content.Controls.Add(refreshLink);
            content.Controls.Add(subtitleLabel);
            content.Controls.Add(listCard);

            scrollHost.Controls.Add(content);
            Controls.Add(scrollHost);

            scrollHost.Resize += (s, e) => LayoutContent();

            // A session that finishes while this tab is open should show up without the
            // user having to click Refresh — MainForm's own handler writes the entry to
            // disk first, so by the time this fires the store already has it.
            service.SessionEnded += service_SessionEnded;

            RenderList();
            LayoutContent();
        }

        private void service_SessionEnded(object sender, Domains.TimeTracking.Models.TrackingSessionSummary summary)
        {
            RenderList();
            LayoutContent();
        }

        // --------------------------------------------------------------- rendering

        private void RenderList()
        {
            listCard.SuspendLayout();
            listCard.Controls.Clear();

            List<TaskHistoryEntry> entries = TaskHistoryStore.LoadAll();
            entries.Reverse();

            var rows = new List<Control>();
            if (entries.Count == 0)
            {
                rows.Add(MakeEmptyRow());
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    rows.Add(BuildRow(entries[i], i < entries.Count - 1));
                }
            }

            listCardHeight = HeaderHeight + rows.Sum(r => r.Height);

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                rows[i].Dock = DockStyle.Top;
                listCard.Controls.Add(rows[i]);
            }

            Label header = MakeCardHeader("Completed sessions");
            header.Dock = DockStyle.Top;
            listCard.Controls.Add(header);

            listCard.ResumeLayout();
        }

        private static Label MakeEmptyRow()
        {
            return new Label
            {
                Height = EmptyHeight,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Stone,
                Text = "No tracked sessions yet. Start tracking from the Time tracker tab — "
                     + "finished sessions will show up here with when they started and finished.",
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private static Panel BuildRow(TaskHistoryEntry entry, bool withDivider)
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

            string title = entry.TaskId.HasValue
                ? "#" + entry.TaskId.Value + (string.IsNullOrEmpty(entry.TaskTitle) ? string.Empty : "  ·  " + entry.TaskTitle)
                : (string.IsNullOrEmpty(entry.TaskTitle) ? "Untitled session" : entry.TaskTitle);

            int titleLeft = 0;
            if (entry.IsOvertime)
            {
                var otTag = new Label
                {
                    AutoSize = false,
                    BackColor = RpvTheme.Warning,
                    ForeColor = RpvTheme.OnAccent,
                    Font = RpvTheme.FontMicro,
                    Text = "OT",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Bounds = new Rectangle(0, 10, 28, 16)
                };
                row.Controls.Add(otTag);
                titleLeft = 34;
            }

            var titleLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Charcoal,
                Text = title,
                Bounds = new Rectangle(titleLeft, 8, 320 - titleLeft, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                AutoEllipsis = true
            };

            string range = entry.StartedAt.Date == entry.EndedAt.Date
                ? entry.StartedAt.ToString("d MMM yyyy") + "  ·  " + entry.StartedAt.ToString("h:mm tt") + " – " + entry.EndedAt.ToString("h:mm tt")
                : entry.StartedAt.ToString("d MMM, h:mm tt") + " – " + entry.EndedAt.ToString("d MMM, h:mm tt");

            var metaLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Text = range,
                Bounds = new Rectangle(titleLeft, 30, 320 - titleLeft, 18),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var durationLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.HeadingText,
                Text = FormatDuration(entry.EndedAt - entry.StartedAt),
                Bounds = new Rectangle(0, 8, 110, 20),
                TextAlign = ContentAlignment.MiddleRight
            };

            var activityLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Terracotta,
                Text = entry.ActivePercent + "% active",
                Bounds = new Rectangle(0, 30, 110, 18),
                TextAlign = ContentAlignment.MiddleRight
            };

            row.Controls.Add(titleLabel);
            row.Controls.Add(metaLabel);
            row.Controls.Add(durationLabel);
            row.Controls.Add(activityLabel);

            row.Resize += (s, e) =>
            {
                durationLabel.Left = Math.Max(0, row.ClientSize.Width - durationLabel.Width);
                activityLabel.Left = Math.Max(0, row.ClientSize.Width - activityLabel.Width);
                int textWidth = Math.Max(60, durationLabel.Left - RpvTheme.Space4 - titleLeft);
                titleLabel.Width = textWidth;
                metaLabel.Width = textWidth;
            };

            return row;
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalHours >= 1)
            {
                return (int)span.TotalHours + "h " + span.Minutes + "m";
            }
            return Math.Max(0, span.Minutes) + "m";
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
            listCard.SetBounds(0, y, width, listCardHeight);

            content.Height = y + listCardHeight + RpvTheme.Space5;

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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                service.SessionEnded -= service_SessionEnded;
            }
            base.Dispose(disposing);
        }
    }
}
