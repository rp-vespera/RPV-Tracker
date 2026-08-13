using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;
using RPV_Tracker.Domains.TimeTracking.Models;
using RPV_Tracker.Domains.TimeTracking.Services;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Pages
{
    /// <summary>
    /// A day's tracking, read back at interval resolution: pick a date, see every captured
    /// interval as a bar across the day, and the sessions those intervals belong to.
    /// </summary>
    /// <remarks>
    /// Sessions come from <see cref="TaskHistoryStore"/> and the bars from
    /// <see cref="IntervalHistoryStore"/>, which are written independently — so a day that
    /// predates interval logging still lists its sessions, it just has no bars to draw.
    /// </remarks>
    internal class ReportPage : UserControl
    {
        private const int TitleHeight = 32;
        private const int SubtitleHeight = 32;
        private const int ControlsHeight = 40;
        private const int StatsHeight = 96;
        private const int ChartHeight = 300;
        private const int SessionRowHeight = 62;
        private const int CardHeaderHeight = 34;
        private const int EmptyRowHeight = 110;

        /// <summary>Chart grouping, in minutes. 0 means one bar per captured interval.</summary>
        private static readonly int[] GroupingMinutes = { 0, 15, 30, 60 };
        private static readonly string[] GroupingLabels = { "Per interval", "15 min", "30 min", "1 hour" };

        private readonly TimeTrackingService service;

        private readonly Panel scrollHost;
        private readonly Panel content;
        private readonly Label titleLabel;
        private readonly RpvButton refreshLink;
        private readonly Label subtitleLabel;

        private readonly ThemedComboBox dayPicker;
        private readonly RpvButton prevDayButton;
        private readonly RpvButton nextDayButton;
        private readonly SegmentedControl groupingControl;

        private readonly StatCard trackedStat;
        private readonly StatCard sessionsStat;
        private readonly StatCard activityStat;
        private readonly StatCard shotsStat;

        private readonly CardPanel chartCard;
        private readonly ActivityBarChart chart;
        private readonly Label chartCaption;

        private readonly CardPanel sessionsCard;

        private DateTime selectedDay = DateTime.Today;
        private int sessionsCardHeight = CardHeaderHeight + EmptyRowHeight;
        private bool suppressPickerEvents;

        public ReportPage(TimeTrackingService trackingService)
        {
            service = trackingService;
            BackColor = RpvTheme.Cream;
            DoubleBuffered = true;

            scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = RpvTheme.Cream };
            content = new Panel { BackColor = RpvTheme.Cream };

            titleLabel = MakeLabel(RpvTheme.FontH1, RpvTheme.HeadingText, "Report", TitleHeight);
            refreshLink = new RpvButton { Text = "Refresh", Variant = RpvButtonVariant.Tertiary, Size = new Size(120, 28) };
            refreshLink.Click += (s, e) => { ReloadDays(); Render(); };
            subtitleLabel = MakeLabel(RpvTheme.FontBody, RpvTheme.Stone,
                "Every interval captured on a day, at whatever screenshot cadence each session used.",
                SubtitleHeight);

            // ---- day controls ----
            prevDayButton = new RpvButton { Text = "◀", Variant = RpvButtonVariant.Tertiary };
            prevDayButton.Click += (s, e) => StepDay(-1);

            nextDayButton = new RpvButton { Text = "▶", Variant = RpvButtonVariant.Tertiary };
            nextDayButton.Click += (s, e) => StepDay(1);

            dayPicker = new ThemedComboBox();
            dayPicker.SelectedIndexChanged += dayPicker_SelectedIndexChanged;

            groupingControl = new SegmentedControl { Options = GroupingLabels };
            groupingControl.SetSelectedIndexSilently(0);
            groupingControl.SelectedIndexChanged += (s, e) => Render();

            // ---- day summary ----
            trackedStat = new StatCard { Label = "Tracked", Value = "0m", IsAccent = true };
            sessionsStat = new StatCard { Label = "Sessions", Value = "0" };
            activityStat = new StatCard { Label = "Avg activity", Value = "—" };
            shotsStat = new StatCard { Label = "Screenshots", Value = "0" };

            // ---- chart ----
            chartCard = new CardPanel();
            Label chartHeader = MakeCardHeader("Activity through the day");
            chartHeader.Dock = DockStyle.Top;
            chartCaption = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 18,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Text = string.Empty,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
            chart = new ActivityBarChart
            {
                Dock = DockStyle.Fill,
                EmptyText = "No intervals recorded for this day."
            };
            chartCard.Controls.Add(chart);
            chartCard.Controls.Add(chartCaption);
            chartCard.Controls.Add(chartHeader);

            // ---- sessions ----
            sessionsCard = new CardPanel();

            content.Controls.Add(titleLabel);
            content.Controls.Add(refreshLink);
            content.Controls.Add(subtitleLabel);
            content.Controls.Add(prevDayButton);
            content.Controls.Add(dayPicker);
            content.Controls.Add(nextDayButton);
            content.Controls.Add(groupingControl);
            content.Controls.Add(trackedStat);
            content.Controls.Add(sessionsStat);
            content.Controls.Add(activityStat);
            content.Controls.Add(shotsStat);
            content.Controls.Add(chartCard);
            content.Controls.Add(sessionsCard);

            scrollHost.Controls.Add(content);
            Controls.Add(scrollHost);

            scrollHost.Resize += (s, e) => LayoutContent();

            // A session finishing while this tab is open should land in the report without a
            // manual refresh — MainForm writes both stores before this fires.
            service.SessionEnded += service_SessionEnded;

            ReloadDays();
            Render();
            LayoutContent();
        }

        private void service_SessionEnded(object sender, TrackingSessionSummary summary)
        {
            ReloadDays();
            Render();
            LayoutContent();
        }

        // --------------------------------------------------------------- day selection

        /// <summary>
        /// Rebuilds the day list from every day that has either sessions or intervals on
        /// record, newest first, with today always present so a fresh install has something
        /// to show.
        /// </summary>
        private void ReloadDays()
        {
            var days = new List<DateTime>();
            foreach (TaskHistoryEntry entry in TaskHistoryStore.LoadAll())
            {
                if (!days.Contains(entry.StartedAt.Date))
                {
                    days.Add(entry.StartedAt.Date);
                }
            }
            foreach (IntervalHistoryEntry entry in IntervalHistoryStore.LoadAll())
            {
                if (!days.Contains(entry.StartedAt.Date))
                {
                    days.Add(entry.StartedAt.Date);
                }
            }
            if (!days.Contains(DateTime.Today))
            {
                days.Add(DateTime.Today);
            }
            if (!days.Contains(selectedDay.Date))
            {
                days.Add(selectedDay.Date);
            }

            days.Sort();
            days.Reverse();

            suppressPickerEvents = true;
            dayPicker.Items.Clear();
            int selectIndex = 0;
            for (int i = 0; i < days.Count; i++)
            {
                dayPicker.Items.Add(new DayChoice(days[i]));
                if (days[i] == selectedDay.Date)
                {
                    selectIndex = i;
                }
            }
            if (dayPicker.Items.Count > 0)
            {
                dayPicker.SelectedIndex = selectIndex;
                selectedDay = ((DayChoice)dayPicker.SelectedItem).Date;
            }
            suppressPickerEvents = false;

            UpdateStepButtons();
        }

        private void dayPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressPickerEvents)
            {
                return;
            }

            var choice = dayPicker.SelectedItem as DayChoice;
            if (choice == null)
            {
                return;
            }

            selectedDay = choice.Date;
            UpdateStepButtons();
            Render();
            LayoutContent();
        }

        /// <summary>Moves through the listed days — older at -1, newer at +1.</summary>
        private void StepDay(int direction)
        {
            int index = dayPicker.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            // The list runs newest-first, so stepping to an older day means moving down it.
            int target = index - direction;
            if (target < 0 || target >= dayPicker.Items.Count)
            {
                return;
            }

            dayPicker.SelectedIndex = target;
        }

        private void UpdateStepButtons()
        {
            int index = dayPicker.SelectedIndex;
            int count = dayPicker.Items.Count;
            prevDayButton.Enabled = index >= 0 && index + 1 < count;
            nextDayButton.Enabled = index > 0;
        }

        // --------------------------------------------------------------- rendering

        private void Render()
        {
            List<IntervalHistoryEntry> intervals = IntervalHistoryStore.LoadForDay(selectedDay);
            List<TaskHistoryEntry> sessions = TaskHistoryStore.LoadAll()
                .Where(s => s.StartedAt.Date == selectedDay.Date)
                .OrderBy(s => s.StartedAt)
                .ToList();

            RenderStats(intervals, sessions);
            RenderChart(intervals);
            RenderSessions(sessions, intervals);
        }

        private void RenderStats(List<IntervalHistoryEntry> intervals, List<TaskHistoryEntry> sessions)
        {
            double trackedHours = sessions.Sum(s => (s.EndedAt - s.StartedAt).TotalHours);
            int shots = sessions.Sum(s => s.ScreenshotCount);

            trackedStat.Value = FormatHours(trackedHours);
            sessionsStat.Value = sessions.Count.ToString();
            shotsStat.Value = shots.ToString();

            if (intervals.Count > 0)
            {
                activityStat.Value = WeightedActivity(intervals) + "%";
            }
            else if (sessions.Count > 0)
            {
                // No interval rows for this day — fall back to the per-session averages,
                // weighted by how long each session ran.
                double totalSeconds = sessions.Sum(s => (s.EndedAt - s.StartedAt).TotalSeconds);
                if (totalSeconds > 0)
                {
                    double weighted = sessions.Sum(s => s.ActivePercent * (s.EndedAt - s.StartedAt).TotalSeconds) / totalSeconds;
                    activityStat.Value = (int)Math.Round(weighted) + "%";
                }
                else
                {
                    activityStat.Value = "—";
                }
            }
            else
            {
                activityStat.Value = "—";
            }
        }

        /// <summary>
        /// Activity across a set of intervals, weighted by their length. Averaging the
        /// percentages directly would let a 12-second closing interval count as much as a
        /// full 20-minute one.
        /// </summary>
        private static int WeightedActivity(IEnumerable<IntervalHistoryEntry> intervals)
        {
            int active = 0;
            int total = 0;
            foreach (IntervalHistoryEntry entry in intervals)
            {
                active += entry.ActiveSeconds;
                total += entry.TotalSeconds;
            }
            return total > 0 ? (int)Math.Round(active * 100.0 / total) : 0;
        }

        private void RenderChart(List<IntervalHistoryEntry> intervals)
        {
            if (intervals.Count == 0)
            {
                chart.SetData(new ActivityBarChart.Bucket[0]);
                chart.EmptyText = selectedDay.Date == DateTime.Today
                    ? "No intervals recorded yet today. They appear here as each one completes."
                    : "No interval detail recorded for this day.";
                chartCaption.Text = string.Empty;
                return;
            }

            int grouping = GroupingMinutes[groupingControl.SelectedIndex];
            List<ActivityBarChart.Bucket> buckets = grouping == 0
                ? BuildPerIntervalBuckets(intervals)
                : BuildGroupedBuckets(intervals, grouping);

            chart.SetData(buckets);
            chartCaption.Text = DescribeCadence(intervals) + "  ·  "
                + buckets.Count + (buckets.Count == 1 ? " bar" : " bars") + "  ·  "
                + "green ≥60%, amber ≥30%";
        }

        private static List<ActivityBarChart.Bucket> BuildPerIntervalBuckets(List<IntervalHistoryEntry> intervals)
        {
            var buckets = new List<ActivityBarChart.Bucket>();
            foreach (IntervalHistoryEntry entry in intervals)
            {
                buckets.Add(new ActivityBarChart.Bucket
                {
                    Label = entry.StartedAt.ToString("h:mm"),
                    Percent = entry.ActivityPercent
                });
            }
            return buckets;
        }

        /// <summary>
        /// Buckets intervals into fixed slots of <paramref name="minutes"/>, keeping the slots
        /// that actually contain work. Empty slots are dropped rather than drawn as zero bars,
        /// so a lunch break reads as a gap in time rather than as inactivity.
        /// </summary>
        private static List<ActivityBarChart.Bucket> BuildGroupedBuckets(List<IntervalHistoryEntry> intervals, int minutes)
        {
            var order = new List<int>();
            var active = new Dictionary<int, int>();
            var total = new Dictionary<int, int>();

            foreach (IntervalHistoryEntry entry in intervals)
            {
                int slot = (int)entry.StartedAt.TimeOfDay.TotalMinutes / minutes;
                if (!total.ContainsKey(slot))
                {
                    order.Add(slot);
                    active[slot] = 0;
                    total[slot] = 0;
                }
                active[slot] += entry.ActiveSeconds;
                total[slot] += entry.TotalSeconds;
            }

            order.Sort();

            var buckets = new List<ActivityBarChart.Bucket>();
            foreach (int slot in order)
            {
                buckets.Add(new ActivityBarChart.Bucket
                {
                    Label = DateTime.Today.AddMinutes(slot * minutes).ToString(minutes >= 60 ? "h tt" : "h:mm"),
                    Percent = total[slot] > 0 ? (int)Math.Round(active[slot] * 100.0 / total[slot]) : 0
                });
            }
            return buckets;
        }

        /// <summary>
        /// Names the cadence the day was captured at. Sessions can differ — someone may switch
        /// from 5 to 20 minutes mid-day — so this reports the range when they do.
        /// </summary>
        private static string DescribeCadence(List<IntervalHistoryEntry> intervals)
        {
            var lengths = new List<int>();
            foreach (IntervalHistoryEntry entry in intervals)
            {
                // Round to the nearest minute and ignore the short closing interval a manual
                // stop produces, which would otherwise read as a cadence of its own.
                int minutes = (int)Math.Round(entry.TotalSeconds / 60.0);
                if (minutes > 0 && !lengths.Contains(minutes))
                {
                    lengths.Add(minutes);
                }
            }

            if (lengths.Count == 0)
            {
                return "Interval detail";
            }

            lengths.Sort();
            int mode = ModeLength(intervals);
            if (lengths.Count == 1 || mode > 0)
            {
                return mode + "-minute intervals";
            }
            return lengths[0] + "–" + lengths[lengths.Count - 1] + " minute intervals";
        }

        private static int ModeLength(List<IntervalHistoryEntry> intervals)
        {
            var counts = new Dictionary<int, int>();
            foreach (IntervalHistoryEntry entry in intervals)
            {
                int minutes = (int)Math.Round(entry.TotalSeconds / 60.0);
                if (minutes <= 0)
                {
                    continue;
                }
                counts[minutes] = counts.ContainsKey(minutes) ? counts[minutes] + 1 : 1;
            }

            int best = 0;
            int bestCount = 0;
            foreach (KeyValuePair<int, int> pair in counts)
            {
                if (pair.Value > bestCount)
                {
                    best = pair.Key;
                    bestCount = pair.Value;
                }
            }

            // Only call it "the" cadence when it genuinely dominates the day.
            return bestCount * 2 >= intervals.Count ? best : 0;
        }

        private void RenderSessions(List<TaskHistoryEntry> sessions, List<IntervalHistoryEntry> intervals)
        {
            sessionsCard.SuspendLayout();
            sessionsCard.Controls.Clear();

            var rows = new List<Control>();
            if (sessions.Count == 0)
            {
                rows.Add(new Label
                {
                    Height = EmptyRowHeight,
                    BackColor = RpvTheme.CardSurface,
                    Font = RpvTheme.FontBody,
                    ForeColor = RpvTheme.Stone,
                    Text = "No sessions tracked on this day.",
                    TextAlign = ContentAlignment.MiddleCenter
                });
            }
            else
            {
                for (int i = 0; i < sessions.Count; i++)
                {
                    TaskHistoryEntry session = sessions[i];
                    List<IntervalHistoryEntry> own = intervals
                        .Where(x => x.SessionId != null && x.SessionId == session.SessionId)
                        .ToList();
                    rows.Add(BuildSessionRow(session, own, i < sessions.Count - 1));
                }
            }

            sessionsCardHeight = CardHeaderHeight + rows.Sum(r => r.Height);

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                rows[i].Dock = DockStyle.Top;
                sessionsCard.Controls.Add(rows[i]);
            }

            Label header = MakeCardHeader("Sessions on " + selectedDay.ToString("d MMM yyyy"));
            header.Dock = DockStyle.Top;
            sessionsCard.Controls.Add(header);

            sessionsCard.ResumeLayout();
        }

        private static Panel BuildSessionRow(TaskHistoryEntry session, List<IntervalHistoryEntry> intervals, bool withDivider)
        {
            var row = new Panel { Height = SessionRowHeight, BackColor = RpvTheme.CardSurface };
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

            string title = session.TaskId.HasValue
                ? "#" + session.TaskId.Value
                    + (string.IsNullOrEmpty(session.TaskTitle) ? string.Empty : "  ·  " + session.TaskTitle)
                : (string.IsNullOrEmpty(session.TaskTitle) ? "Untitled session" : session.TaskTitle);

            int titleLeft = 0;
            if (session.IsOvertime)
            {
                row.Controls.Add(new Label
                {
                    AutoSize = false,
                    BackColor = RpvTheme.Warning,
                    ForeColor = RpvTheme.OnAccent,
                    Font = RpvTheme.FontMicro,
                    Text = "OT",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Bounds = new Rectangle(0, 12, 28, 16)
                });
                titleLeft = 34;
            }

            var titleLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Charcoal,
                Text = title,
                Bounds = new Rectangle(titleLeft, 10, 320 - titleLeft, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                AutoEllipsis = true
            };

            string cadence = intervals.Count > 0
                ? intervals.Count + (intervals.Count == 1 ? " interval" : " intervals")
                    + " · " + ModeLength(intervals) + " min"
                : "no interval detail";

            var metaLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Text = session.StartedAt.ToString("h:mm tt") + " – " + session.EndedAt.ToString("h:mm tt")
                    + "   ·   " + cadence
                    + "   ·   " + session.ScreenshotCount + (session.ScreenshotCount == 1 ? " shot" : " shots"),
                Bounds = new Rectangle(titleLeft, 32, 320 - titleLeft, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };

            var durationLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.HeadingText,
                Text = FormatDuration(session.EndedAt - session.StartedAt),
                Bounds = new Rectangle(0, 10, 110, 20),
                TextAlign = ContentAlignment.MiddleRight
            };

            int activity = intervals.Count > 0 ? WeightedActivity(intervals) : session.ActivePercent;
            var activityLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Terracotta,
                Text = activity + "% active",
                Bounds = new Rectangle(0, 32, 110, 18),
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

            prevDayButton.SetBounds(0, y, 36, ControlsHeight);
            dayPicker.SetBounds(42, y + 6, 220, 28);
            nextDayButton.SetBounds(268, y, 36, ControlsHeight);

            int groupingWidth = Math.Min(400, Math.Max(240, width - 320));
            groupingControl.SetBounds(width - groupingWidth, y, groupingWidth, ControlsHeight);

            y += ControlsHeight + RpvTheme.Space5;
            LayoutStatRow(y, width);

            y += StatsHeight + RpvTheme.Space5;
            chartCard.SetBounds(0, y, width, ChartHeight);

            y += ChartHeight + RpvTheme.Space5;
            sessionsCard.SetBounds(0, y, width, sessionsCardHeight);

            content.Height = y + sessionsCardHeight + RpvTheme.Space5;
            scrollHost.AutoScrollMinSize = new Size(0, content.Height);
        }

        private void LayoutStatRow(int y, int width)
        {
            StatCard[] cards = { trackedStat, sessionsStat, activityStat, shotsStat };
            int gap = RpvTheme.Space4;
            int cardWidth = (width - (gap * (cards.Length - 1))) / cards.Length;
            int cursor = 0;
            for (int i = 0; i < cards.Length; i++)
            {
                int w = i == cards.Length - 1 ? width - cursor : cardWidth;
                cards[i].SetBounds(cursor, y, w, StatsHeight);
                cursor += w + gap;
            }
        }

        // --------------------------------------------------------------- helpers

        private static string FormatHours(double hours)
        {
            int totalMinutes = (int)Math.Round(hours * 60);
            int h = totalMinutes / 60;
            int m = totalMinutes % 60;
            return h > 0 ? h + "h " + m + "m" : m + "m";
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalHours >= 1)
            {
                return (int)span.TotalHours + "h " + span.Minutes + "m";
            }
            return Math.Max(0, span.Minutes) + "m";
        }

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
                Height = CardHeaderHeight,
                Text = text,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private class DayChoice
        {
            public readonly DateTime Date;

            public DayChoice(DateTime date)
            {
                Date = date.Date;
            }

            public override string ToString()
            {
                if (Date == DateTime.Today)
                {
                    return "Today · " + Date.ToString("d MMM yyyy");
                }
                if (Date == DateTime.Today.AddDays(-1))
                {
                    return "Yesterday · " + Date.ToString("d MMM yyyy");
                }
                return Date.ToString("ddd, d MMM yyyy");
            }
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
