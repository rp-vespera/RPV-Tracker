using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;
using RPV_Tracker.Domains.Auth.Models;
using RPV_Tracker.Domains.Auth.Services;
using RPV_Tracker.Domains.Pulse.Models;
using RPV_Tracker.Domains.Pulse.Services;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Pages
{
    /// <summary>
    /// The landing screen after sign-in: a personal greeting, your own task headline
    /// figures, your active/overdue tasks, your performance status, and a 7-day activity
    /// chart built from locally logged tracking sessions. Everything here is the same
    /// person's own data the Time Tracker and Task history tabs use — no company-wide
    /// admin figures, since this app has no such data (or role) to show.
    /// </summary>
    internal class DashboardPage : UserControl
    {
        private const int GreetingHeight = 36;
        private const int DateHeight = 20;
        private const int StatsHeight = 96;
        private const int SectionsHeight = 400;
        private const int ChartHeight = 220;
        private const int RowHeight = 58;

        private readonly Panel scrollHost;
        private readonly Panel content;
        private readonly Label greetingLabel;
        private readonly Label dateLabel;
        private readonly Panel statsRow;
        private readonly CardPanel tasksCard;
        private readonly CardPanel statusCard;
        private readonly CardPanel activityChartCard;
        private readonly ActivityBarChart activityChart;

        private readonly List<StatCard> statCards = new List<StatCard>();

        /// <summary>Raised when "Open time tracker" is clicked, so the shell can switch tabs.</summary>
        public event EventHandler OpenTrackerRequested;

        public DashboardPage()
        {
            BackColor = RpvTheme.Cream;
            DoubleBuffered = true;

            scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = RpvTheme.Cream
            };

            content = new Panel { BackColor = RpvTheme.Cream };

            greetingLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.Cream,
                Font = RpvTheme.FontH1,
                ForeColor = RpvTheme.HeadingText,
                Location = new Point(0, 0),
                Height = GreetingHeight,
                Text = BuildGreeting()
            };

            dateLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.Cream,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Stone,
                Location = new Point(0, GreetingHeight + 2),
                Height = DateHeight,
                Text = DateTime.Now.ToString("dddd, d MMMM yyyy")
            };

            statsRow = new Panel { BackColor = RpvTheme.Cream, Height = StatsHeight };

            tasksCard = new CardPanel();
            statusCard = new CardPanel();

            activityChartCard = new CardPanel();
            Label chartHeader = BuildCardHeader("Activity — last 7 days");
            chartHeader.Dock = DockStyle.Top;
            activityChart = new ActivityBarChart
            {
                Dock = DockStyle.Fill,
                EmptyText = "Start tracking time on the Time tracker tab to see your activity here."
            };
            activityChartCard.Controls.Add(activityChart);
            activityChartCard.Controls.Add(chartHeader);

            content.Controls.Add(greetingLabel);
            content.Controls.Add(dateLabel);
            content.Controls.Add(statsRow);
            content.Controls.Add(tasksCard);
            content.Controls.Add(statusCard);
            content.Controls.Add(activityChartCard);

            scrollHost.Controls.Add(content);
            Controls.Add(scrollHost);

            scrollHost.Resize += (s, e) => LayoutContent();
            LayoutContent();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            List<PulseTask> tasks = null;
            Performance perf = null;

            try
            {
                tasks = await PulseService.GetMyTasksAsync();
            }
            catch (Exception)
            {
                // Read-only landing screen — a failed load leaves the shell usable rather
                // than throwing an unhandled exception onto the UI thread.
                tasks = null;
            }

            try
            {
                perf = await PulseService.GetMyPerformanceAsync();
            }
            catch (Exception)
            {
                perf = null;
            }

            if (IsDisposed)
            {
                return;
            }

            List<PulseTask> active = (tasks ?? new List<PulseTask>())
                .Where(t => !t.IsDone)
                .OrderByDescending(t => t.DaysLate)
                .ThenByDescending(t => t.active)
                .ToList();

            RenderStats(active, perf);
            RenderTasks(active);
            RenderStatus(perf);
            RenderActivityChart();
            LayoutContent();
        }

        private static string BuildGreeting()
        {
            AuthenticatedUser user = AppSession.User;
            string name = user != null ? user.FirstName : "there";

            int hour = DateTime.Now.Hour;
            string part;
            if (hour < 12)
            {
                part = "Good morning";
            }
            else if (hour < 18)
            {
                part = "Good afternoon";
            }
            else
            {
                part = "Good evening";
            }

            return part + ", " + name;
        }

        protected virtual void OnOpenTrackerRequested()
        {
            EventHandler handler = OpenTrackerRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        // ------------------------------------------------------------------ layout

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
            content.Top = RpvTheme.Space6;
            content.Width = width;

            greetingLabel.Width = width;
            dateLabel.Width = width;

            int statsTop = GreetingHeight + DateHeight + RpvTheme.Space5;
            statsRow.SetBounds(0, statsTop, width, StatsHeight);
            LayoutStatCards(width);

            int sectionsTop = statsTop + StatsHeight + RpvTheme.Space7;
            int gap = RpvTheme.Space4;
            int leftWidth = (int)((width - gap) * 0.62);
            int rightWidth = width - gap - leftWidth;

            tasksCard.SetBounds(0, sectionsTop, leftWidth, SectionsHeight);
            statusCard.SetBounds(leftWidth + gap, sectionsTop, rightWidth, SectionsHeight);

            int chartTop = sectionsTop + SectionsHeight + RpvTheme.Space5;
            activityChartCard.SetBounds(0, chartTop, width, ChartHeight);

            content.Height = chartTop + ChartHeight;

            scrollHost.AutoScrollMinSize = new Size(0, content.Height);
        }

        private void LayoutStatCards(int width)
        {
            if (statCards.Count == 0)
            {
                return;
            }

            int gap = RpvTheme.Space4;
            int count = statCards.Count;
            int cardWidth = (width - (gap * (count - 1))) / count;
            int x = 0;

            for (int i = 0; i < count; i++)
            {
                // The last card absorbs the rounding remainder so the row ends flush.
                int w = i == count - 1 ? width - x : cardWidth;
                statCards[i].SetBounds(x, 0, w, StatsHeight);
                x += w + gap;
            }
        }

        // --------------------------------------------------------------- rendering

        private void RenderStats(List<PulseTask> activeTasks, Performance perf)
        {
            statsRow.Controls.Clear();
            statCards.Clear();

            int overdue = activeTasks.Count(t => t.DaysLate > 0);

            AddStat("Active tasks", activeTasks.Count.ToString(), false);
            AddStat("Overdue", overdue.ToString(), overdue > 0);
            AddStat("Performance score", perf != null ? perf.score.ToString() : "—", false);
            AddStat("Tracked today", FormatHours(HoursTrackedOn(DateTime.Today)), false);
        }

        private void AddStat(string label, string value, bool accent)
        {
            var card = new StatCard { Label = label, Value = value, IsAccent = accent };
            statCards.Add(card);
            statsRow.Controls.Add(card);
        }

        private static double HoursTrackedOn(DateTime day)
        {
            double totalHours = 0;
            foreach (TaskHistoryEntry entry in TaskHistoryStore.LoadAll())
            {
                if (entry.StartedAt.Date == day.Date)
                {
                    totalHours += (entry.EndedAt - entry.StartedAt).TotalHours;
                }
            }
            return totalHours;
        }

        private static string FormatHours(double hours)
        {
            int totalMinutes = (int)Math.Round(hours * 60);
            int h = totalMinutes / 60;
            int m = totalMinutes % 60;
            return h > 0 ? h + "h " + m + "m" : m + "m";
        }

        private void RenderTasks(List<PulseTask> activeTasks)
        {
            var stack = new List<Control> { BuildCardHeader("My tasks") };

            int shown = Math.Min(activeTasks.Count, 5);
            for (int i = 0; i < shown; i++)
            {
                stack.Add(BuildTaskRow(activeTasks[i], i < shown - 1));
            }

            if (activeTasks.Count == 0)
            {
                stack.Add(BuildEmptyRow("No active or overdue tasks — nice work."));
            }

            FillCard(tasksCard, stack, "Open time tracker", (s, e) => OnOpenTrackerRequested());
        }

        private void RenderStatus(Performance perf)
        {
            var stack = new List<Control> { BuildCardHeader("Your status") };

            var scoreBig = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontDisplay,
                ForeColor = perf != null ? ScoreColor(perf.score) : RpvTheme.Stone,
                Height = 64,
                Text = perf != null ? perf.score.ToString() : "—",
                TextAlign = ContentAlignment.MiddleLeft
            };
            var scoreCaption = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Height = 22,
                Text = "Performance score (out of 100)",
                TextAlign = ContentAlignment.MiddleLeft
            };
            stack.Add(scoreBig);
            stack.Add(scoreCaption);

            string[] captions = { "Tasks done", "Overdue", "Unresolved", "Active concerns" };
            string[] values = perf != null
                ? new[]
                {
                    perf.tasks_done + " / " + perf.tasks_total,
                    perf.overdue_tasks.ToString(),
                    perf.unresolved_tasks.ToString(),
                    perf.active_concerns.ToString()
                }
                : new[] { "—", "—", "—", "—" };

            for (int i = 0; i < captions.Length; i++)
            {
                stack.Add(BuildStatusRow(captions[i], values[i]));
            }

            FillCard(statusCard, stack, null, null);
        }

        private static Panel BuildStatusRow(string caption, string value)
        {
            var row = new Panel { Height = 30, BackColor = RpvTheme.CardSurface };

            var name = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Charcoal,
                Dock = DockStyle.Left,
                Width = 200,
                Text = caption,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var figure = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.HeadingText,
                Dock = DockStyle.Fill,
                Text = value,
                TextAlign = ContentAlignment.MiddleRight
            };

            row.Controls.Add(figure);
            row.Controls.Add(name);
            return row;
        }

        private static Color ScoreColor(int score)
        {
            if (score >= 80)
            {
                return RpvTheme.Success;
            }
            return score >= 60 ? RpvTheme.Warning : RpvTheme.Danger;
        }

        /// <summary>Buckets local tracking history into the last 7 calendar days, oldest first.</summary>
        private void RenderActivityChart()
        {
            List<TaskHistoryEntry> entries = TaskHistoryStore.LoadAll();
            if (entries.Count == 0)
            {
                activityChart.SetData(new ActivityBarChart.Bucket[0]);
                return;
            }

            var buckets = new List<ActivityBarChart.Bucket>();
            for (int i = 6; i >= 0; i--)
            {
                DateTime day = DateTime.Today.AddDays(-i);
                List<TaskHistoryEntry> dayEntries = entries.Where(x => x.StartedAt.Date == day).ToList();
                int percent = dayEntries.Count > 0 ? (int)Math.Round(dayEntries.Average(x => x.ActivePercent)) : 0;

                buckets.Add(new ActivityBarChart.Bucket
                {
                    Label = i == 0 ? "Today" : day.ToString("ddd"),
                    Percent = percent
                });
            }

            activityChart.SetData(buckets);
        }

        /// <summary>
        /// Docked children stack in reverse z-order, so the list is added back to front
        /// to end up reading top to bottom.
        /// </summary>
        private static void FillCard(CardPanel card, List<Control> stack, string footerText, EventHandler footerClick)
        {
            card.SuspendLayout();
            card.Controls.Clear();

            if (footerText != null)
            {
                var footer = new RpvButton
                {
                    Text = footerText,
                    Variant = RpvButtonVariant.Tertiary,
                    Dock = DockStyle.Bottom,
                    Height = 30
                };
                if (footerClick != null)
                {
                    footer.Click += footerClick;
                }
                card.Controls.Add(footer);
            }

            for (int i = stack.Count - 1; i >= 0; i--)
            {
                stack[i].Dock = DockStyle.Top;
                card.Controls.Add(stack[i]);
            }

            card.ResumeLayout();
        }

        private static Label BuildCardHeader(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontH3,
                ForeColor = RpvTheme.HeadingText,
                Height = 34,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label BuildEmptyRow(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Stone,
                Height = RowHeight,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Panel BuildTaskRow(PulseTask task, bool withDivider)
        {
            var row = NewRow(withDivider);

            Label title = RowTitle("#" + task.id + "  ·  " + task.title);
            Label meta = RowMeta(string.IsNullOrEmpty(task.due_at) ? "No deadline" : "Due " + task.due_at);

            bool overdue = task.DaysLate > 0;
            var status = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontMicro,
                ForeColor = overdue ? RpvTheme.Danger : RpvTheme.Steel,
                Height = RowHeight,
                Top = 0,
                Width = 100,
                Text = overdue ? task.DaysLate + (task.DaysLate == 1 ? " day late" : " days late") : "Active",
                TextAlign = ContentAlignment.MiddleRight
            };

            row.Controls.Add(title);
            row.Controls.Add(meta);
            row.Controls.Add(status);

            row.Resize += (s, e) =>
            {
                status.Left = Math.Max(0, row.ClientSize.Width - status.Width);
                int textWidth = Math.Max(40, status.Left - RpvTheme.Space3);
                title.Width = textWidth;
                meta.Width = textWidth;
            };

            return row;
        }

        private static Panel NewRow(bool withDivider)
        {
            var row = new Panel
            {
                Height = RowHeight,
                BackColor = RpvTheme.CardSurface
            };

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

            return row;
        }

        private static Label RowTitle(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Charcoal,
                Height = 20,
                Top = 8,
                Left = 0,
                Text = text,
                UseMnemonic = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label RowMeta(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Height = 18,
                Top = 30,
                Left = 0,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }
    }
}
