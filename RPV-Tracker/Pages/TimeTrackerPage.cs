using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;
using RPV_Tracker.Domains.Pulse.Models;
using RPV_Tracker.Domains.Pulse.Services;
using RPV_Tracker.Domains.TimeTracking.Models;
using RPV_Tracker.Domains.TimeTracking.Services;

namespace RPV_Tracker.Pages
{
    /// <summary>
    /// Time-tracker screen. Tracking is gated on a task from the Pulse service: pick a task
    /// that isn't done or blocked and Start becomes available (the session is keyed to the
    /// task's id). The page also shows the person's performance — score, overdue "lates",
    /// and notices/tips.
    /// </summary>
    internal class TimeTrackerPage : UserControl
    {
        private const int TitleHeight = 32;
        private const int SubtitleHeight = 40;
        private const int ControlCardHeight = 182;
        private const int StatsHeight = 96;
        private const int PerfRowHeight = 252;
        private const int NoticesHeight = 128;
        private const int BottomHeight = 248;
        private const int LatesCap = 4;

        private readonly TimeTrackingService service;

        private readonly Panel scrollHost;
        private readonly Panel content;
        private readonly Label titleLabel;
        private readonly RpvButton refreshLink;
        private readonly Label subtitleLabel;

        // control card
        private readonly CardPanel controlCard;
        private readonly Label statusDot;
        private readonly Label statusLabel;
        private readonly Label elapsedLabel;
        private readonly Label subLine;               // gating reason (idle) or next-shot countdown (tracking)
        private readonly Label taskCaption;
        private readonly ComboBox taskSelector;
        private readonly RpvButton toggleButton;
        private readonly Panel progressTrack;
        private float progressFraction;

        // tracker stats
        private readonly StatCard activityStat;
        private readonly StatCard keysStat;
        private readonly StatCard clicksStat;
        private readonly StatCard shotsStat;

        // performance summary
        private readonly CardPanel perfCard;
        private readonly Label scoreBig;
        private readonly Label scoreCaption;
        private readonly Label[] countName = new Label[4];
        private readonly Label[] countValue = new Label[4];

        // lates + notices
        private readonly CardPanel latesCard;
        private readonly Panel latesBody;
        private readonly CardPanel noticesCard;
        private readonly Label noticesLabel;

        // latest screenshot + interval history
        private readonly CardPanel previewCard;
        private readonly PictureBox previewBox;
        private readonly Label previewCaption;
        private readonly RpvButton openFolderButton;
        private readonly CardPanel historyCard;
        private readonly Panel historyBody;

        private List<PulseTask> tasks = new List<PulseTask>();
        private PulseTask selectedTask;
        private bool loading;
        private bool populating;
        private string dataError;

        public TimeTrackerPage(TimeTrackingService trackingService)
        {
            service = trackingService;
            BackColor = RpvTheme.Cream;
            DoubleBuffered = true;

            scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = RpvTheme.Cream };
            content = new Panel { BackColor = RpvTheme.Cream };

            titleLabel = MakeLabel(RpvTheme.FontH1, RpvTheme.Midnight, "Time tracker", TitleHeight);
            refreshLink = new RpvButton { Text = "Refresh", Variant = RpvButtonVariant.Tertiary, Size = new Size(120, 28) };
            refreshLink.Click += async (s, e) => await LoadDataAsync();
            subtitleLabel = MakeLabel(RpvTheme.FontBody, RpvTheme.Stone, BuildDisclosure(), SubtitleHeight);

            // ---- control card ----
            controlCard = new CardPanel();

            taskCaption = MakeCardLabel(RpvTheme.FontCaption, RpvTheme.Stone, "Tracking task", ContentAlignment.MiddleLeft);
            taskSelector = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = RpvTheme.FontBody,
                DisplayMember = "SelectorLabel",
                FlatStyle = FlatStyle.Flat,
                MaxDropDownItems = 12,
                Enabled = false
            };
            taskSelector.SelectedIndexChanged += taskSelector_SelectedIndexChanged;

            statusDot = MakeCardLabel(new Font(RpvTheme.BaseFamily, 14f), RpvTheme.Stone, "●", ContentAlignment.MiddleLeft);
            statusLabel = MakeCardLabel(RpvTheme.FontBodyMedium, RpvTheme.Charcoal, "Not tracking", ContentAlignment.MiddleLeft);
            statusLabel.AutoEllipsis = true;
            elapsedLabel = MakeCardLabel(RpvTheme.FontDisplay, RpvTheme.Midnight, "00:00:00", ContentAlignment.MiddleLeft);
            subLine = MakeCardLabel(RpvTheme.FontCaption, RpvTheme.Stone, string.Empty, ContentAlignment.MiddleLeft);
            subLine.AutoEllipsis = true;

            toggleButton = new RpvButton { Text = "Start tracking", Variant = RpvButtonVariant.Primary, Size = new Size(170, 42), Enabled = false };
            toggleButton.Click += toggleButton_Click;

            progressTrack = new Panel { BackColor = RpvTheme.White, Height = 6 };
            progressTrack.Paint += progressTrack_Paint;

            controlCard.Controls.Add(taskCaption);
            controlCard.Controls.Add(taskSelector);
            controlCard.Controls.Add(statusDot);
            controlCard.Controls.Add(statusLabel);
            controlCard.Controls.Add(elapsedLabel);
            controlCard.Controls.Add(subLine);
            controlCard.Controls.Add(toggleButton);
            controlCard.Controls.Add(progressTrack);

            // ---- tracker stat row ----
            activityStat = new StatCard { Label = "Activity", Value = "0%", IsAccent = true };
            keysStat = new StatCard { Label = "Keyboard taps", Value = "0" };
            clicksStat = new StatCard { Label = "Mouse clicks", Value = "0" };
            shotsStat = new StatCard { Label = "Screenshots", Value = "0" };

            // ---- performance summary card ----
            perfCard = new CardPanel();
            Label perfHeader = MakeCardHeader("Your status");
            perfHeader.Dock = DockStyle.Top;
            scoreBig = MakeCardLabel(RpvTheme.FontDisplay, RpvTheme.Midnight, "—", ContentAlignment.MiddleLeft);
            scoreCaption = MakeCardLabel(RpvTheme.FontCaption, RpvTheme.Stone, "Performance score (out of 100)", ContentAlignment.MiddleLeft);
            perfCard.Controls.Add(scoreBig);
            perfCard.Controls.Add(scoreCaption);

            string[] captions = { "Tasks done", "Overdue", "Unresolved", "Active concerns" };
            for (int i = 0; i < captions.Length; i++)
            {
                countName[i] = MakeCardLabel(RpvTheme.FontBody, RpvTheme.Charcoal, captions[i], ContentAlignment.MiddleLeft);
                countValue[i] = MakeCardLabel(RpvTheme.FontBodyMedium, RpvTheme.Midnight, "—", ContentAlignment.MiddleRight);
                perfCard.Controls.Add(countName[i]);
                perfCard.Controls.Add(countValue[i]);
            }
            perfCard.Controls.Add(perfHeader);

            // ---- lates card ----
            latesCard = new CardPanel();
            Label latesHeader = MakeCardHeader("Running late & overdue");
            latesHeader.Dock = DockStyle.Top;
            latesBody = new Panel { Dock = DockStyle.Fill, BackColor = RpvTheme.White };
            latesCard.Controls.Add(latesBody);
            latesCard.Controls.Add(latesHeader);

            // ---- notices card ----
            noticesCard = new CardPanel();
            Label noticesHeader = MakeCardHeader("Notices & tips");
            noticesHeader.Dock = DockStyle.Top;
            noticesLabel = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Charcoal,
                Text = string.Empty,
                UseMnemonic = false,
                TextAlign = ContentAlignment.TopLeft
            };
            noticesCard.Controls.Add(noticesLabel);
            noticesCard.Controls.Add(noticesHeader);

            // ---- latest screenshot ----
            previewCard = new CardPanel();
            Label previewHeader = MakeCardHeader("Latest screenshot");
            previewHeader.Dock = DockStyle.Top;
            previewBox = new PictureBox { Dock = DockStyle.Fill, BackColor = RpvTheme.Mist, SizeMode = PictureBoxSizeMode.Zoom };
            previewCaption = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Text = "No screenshots captured yet.",
                TextAlign = ContentAlignment.MiddleLeft
            };
            openFolderButton = new RpvButton { Text = "Open screenshots folder", Variant = RpvButtonVariant.Tertiary, Dock = DockStyle.Bottom, Height = 28, Enabled = false };
            openFolderButton.Click += openFolderButton_Click;
            previewCard.Controls.Add(previewBox);
            previewCard.Controls.Add(previewCaption);
            previewCard.Controls.Add(openFolderButton);
            previewCard.Controls.Add(previewHeader);

            // ---- interval history ----
            historyCard = new CardPanel();
            Label historyHeader = MakeCardHeader("Interval history");
            historyHeader.Dock = DockStyle.Top;
            historyBody = new Panel { Dock = DockStyle.Fill, BackColor = RpvTheme.White };
            historyCard.Controls.Add(historyBody);
            historyCard.Controls.Add(historyHeader);

            content.Controls.Add(titleLabel);
            content.Controls.Add(refreshLink);
            content.Controls.Add(subtitleLabel);
            content.Controls.Add(controlCard);
            content.Controls.Add(activityStat);
            content.Controls.Add(keysStat);
            content.Controls.Add(clicksStat);
            content.Controls.Add(shotsStat);
            content.Controls.Add(perfCard);
            content.Controls.Add(latesCard);
            content.Controls.Add(noticesCard);
            content.Controls.Add(previewCard);
            content.Controls.Add(historyCard);

            scrollHost.Controls.Add(content);
            Controls.Add(scrollHost);

            scrollHost.Resize += (s, e) => LayoutContent();

            service.Ticked += service_Ticked;
            service.IntervalCompleted += service_IntervalCompleted;
            service.StateChanged += service_StateChanged;

            RenderPerformance(null);
            RenderHistory();
            UpdateForState();
            LayoutContent();
        }

        private string BuildDisclosure()
        {
            int seconds = service.IntervalLengthSeconds;
            string every = seconds % 60 == 0
                ? (seconds / 60) + (seconds == 60 ? " minute" : " minutes")
                : seconds + " seconds";

            return "Pick a task, then start. While tracking, RPV captures a screenshot every "
                + every + " and counts keyboard and mouse activity — never which keys you press.";
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadDataAsync();
        }

        // --------------------------------------------------------------- data

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            loading = true;
            dataError = null;
            UpdateForState();

            List<PulseTask> loadedTasks = null;
            Performance perf = null;
            string error = null;

            try
            {
                loadedTasks = await PulseService.GetMyTasksAsync();
            }
            catch (Exception ex)
            {
                error = ex.Message;
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

            loading = false;
            dataError = error;
            PopulateTasks(loadedTasks ?? new List<PulseTask>());
            RenderPerformance(perf);
            UpdateForState();
            LayoutContent();
        }

        private void PopulateTasks(List<PulseTask> loaded)
        {
            // Trackable tasks first (not done / not blocked), then active, then done.
            tasks = loaded
                .OrderByDescending(t => t.CanTrack)
                .ThenByDescending(t => t.active)
                .ThenBy(t => t.IsDone)
                .ThenBy(t => t.due_at ?? "9999")
                .ToList();

            populating = true;
            taskSelector.BeginUpdate();
            taskSelector.Items.Clear();
            foreach (PulseTask task in tasks)
            {
                taskSelector.Items.Add(task);
            }
            taskSelector.EndUpdate();

            int defaultIndex = tasks.FindIndex(t => t.CanTrack);
            if (defaultIndex < 0 && tasks.Count > 0)
            {
                defaultIndex = 0;
            }
            taskSelector.SelectedIndex = defaultIndex;
            selectedTask = defaultIndex >= 0 ? tasks[defaultIndex] : null;
            populating = false;
        }

        // --------------------------------------------------------------- service events

        private void service_Ticked(object sender, TrackingSnapshot snap)
        {
            elapsedLabel.Text = FormatElapsed(snap.SessionElapsed);
            activityStat.Value = snap.LiveActivityPercent + "%";
            keysStat.Value = snap.SessionKeys.ToString("N0");
            clicksStat.Value = snap.SessionClicks.ToString("N0");
            shotsStat.Value = snap.ScreenshotCount.ToString();

            if (snap.IsTracking)
            {
                subLine.ForeColor = RpvTheme.Stone;
                subLine.Text = "Next screenshot in " + FormatShort(snap.SecondsUntilNextShot);
                progressFraction = snap.IntervalLengthSeconds == 0
                    ? 0f
                    : (float)snap.IntervalElapsedSeconds / snap.IntervalLengthSeconds;
            }
            else
            {
                progressFraction = 0f;
            }
            progressTrack.Invalidate();
        }

        private void service_IntervalCompleted(object sender, ActivityInterval interval)
        {
            if (!string.IsNullOrEmpty(interval.ScreenshotPath))
            {
                ShowPreview(interval.ScreenshotPath);
            }
            RenderHistory();
        }

        private void service_StateChanged(object sender, EventArgs e)
        {
            UpdateForState();
        }

        // --------------------------------------------------------------- user actions

        private void taskSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (populating)
            {
                return;
            }
            selectedTask = taskSelector.SelectedItem as PulseTask;
            UpdateForState();
        }

        private void toggleButton_Click(object sender, EventArgs e)
        {
            if (service.IsTracking)
            {
                service.Stop();
                return;
            }

            string reason;
            if (!CanStart(out reason))
            {
                return;
            }

            service.Start(selectedTask.id, selectedTask.title);
        }

        private void openFolderButton_Click(object sender, EventArgs e)
        {
            string folder = service.SessionFolder;
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\"");
            }
        }

        // --------------------------------------------------------------- gating

        /// <summary>
        /// The tracker's rules: a task must be selected and be neither done nor blocked.
        /// Tracking is keyed to the task's id. <paramref name="reason"/> explains any block.
        /// </summary>
        private bool CanStart(out string reason)
        {
            if (loading)
            {
                reason = "Loading your tasks…";
                return false;
            }
            if (dataError != null)
            {
                reason = "Couldn't load your tasks: " + dataError;
                return false;
            }
            if (tasks.Count == 0)
            {
                reason = "You have no tasks to track.";
                return false;
            }
            if (selectedTask == null)
            {
                reason = "Select a task to start tracking.";
                return false;
            }
            if (selectedTask.IsBlocked)
            {
                string why = string.IsNullOrWhiteSpace(selectedTask.blocked_reason) ? "see the task details" : selectedTask.blocked_reason;
                reason = "This task is blocked — " + why + ".";
                return false;
            }
            if (selectedTask.IsDone)
            {
                reason = "This task is already done — pick an active task to track.";
                return false;
            }

            reason = "Ready to track Task #" + selectedTask.id + " · " + selectedTask.title;
            return true;
        }

        // --------------------------------------------------------------- rendering

        private void UpdateForState()
        {
            bool on = service.IsTracking;

            taskSelector.Enabled = !on && !loading && tasks.Count > 0;
            refreshLink.Enabled = !on && !loading;

            if (on)
            {
                statusDot.ForeColor = RpvTheme.Danger;
                string task = service.ActiveTaskId.HasValue ? "Task #" + service.ActiveTaskId + " · " : string.Empty;
                statusLabel.Text = "Tracking  ·  " + task + (service.ActiveTaskTitle ?? string.Empty);
                toggleButton.Text = "Stop tracking";
                toggleButton.Variant = RpvButtonVariant.Secondary;
                toggleButton.Enabled = true;
                openFolderButton.Enabled = !string.IsNullOrEmpty(service.SessionFolder);
                return;
            }

            statusDot.ForeColor = RpvTheme.Stone;
            statusLabel.Text = "Not tracking";
            toggleButton.Text = "Start tracking";
            toggleButton.Variant = RpvButtonVariant.Primary;

            string reason;
            bool canStart = CanStart(out reason);
            toggleButton.Enabled = canStart;
            subLine.Text = reason;
            subLine.ForeColor = canStart ? RpvTheme.Stone : (dataError != null ? RpvTheme.Danger : RpvTheme.Warning);
        }

        private void RenderPerformance(Performance perf)
        {
            if (perf == null)
            {
                scoreBig.Text = "—";
                scoreBig.ForeColor = RpvTheme.Stone;
                for (int i = 0; i < countValue.Length; i++)
                {
                    countValue[i].Text = "—";
                }
                RenderLates(null);
                noticesLabel.Text = dataError != null ? "Performance couldn't be loaded." : "No notices right now.";
                return;
            }

            scoreBig.Text = perf.score.ToString();
            scoreBig.ForeColor = ScoreColor(perf.score);
            countValue[0].Text = perf.tasks_done + " / " + perf.tasks_total;
            countValue[1].Text = perf.overdue_tasks.ToString();
            countValue[2].Text = perf.unresolved_tasks.ToString();
            countValue[3].Text = perf.active_concerns.ToString();

            RenderLates(perf.overdue_handling);

            noticesLabel.Text = (perf.tips != null && perf.tips.Count > 0)
                ? string.Join("\n\n", perf.tips.Select(t => "•  " + t))
                : "No notices right now — nice work.";
        }

        private void RenderLates(List<OverdueItem> items)
        {
            latesBody.SuspendLayout();
            latesBody.Controls.Clear();

            List<OverdueItem> list = (items ?? new List<OverdueItem>())
                .OrderBy(i => i.done)                 // unresolved (false) first
                .ThenByDescending(i => i.days_late)
                .ToList();

            if (list.Count == 0)
            {
                latesBody.Controls.Add(new Label
                {
                    Dock = DockStyle.Top,
                    Height = 44,
                    BackColor = RpvTheme.White,
                    Font = RpvTheme.FontBody,
                    ForeColor = RpvTheme.Stone,
                    Text = dataError != null ? "Couldn't load overdue items." : "Nothing overdue — all caught up.",
                    TextAlign = ContentAlignment.MiddleLeft
                });
                latesBody.ResumeLayout();
                return;
            }

            var rows = new List<Control>();
            int shown = Math.Min(list.Count, LatesCap);
            for (int i = 0; i < shown; i++)
            {
                rows.Add(BuildLateRow(list[i], i < shown - 1));
            }
            if (list.Count > LatesCap)
            {
                rows.Add(new Label
                {
                    Height = 24,
                    BackColor = RpvTheme.White,
                    Font = RpvTheme.FontCaption,
                    ForeColor = RpvTheme.Stone,
                    Text = "+ " + (list.Count - LatesCap) + " more",
                    TextAlign = ContentAlignment.MiddleLeft
                });
            }

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                rows[i].Dock = DockStyle.Top;
                latesBody.Controls.Add(rows[i]);
            }
            latesBody.ResumeLayout();
        }

        private Panel BuildLateRow(OverdueItem item, bool withDivider)
        {
            var row = new Panel { Height = 46, BackColor = RpvTheme.White };
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

            var title = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Charcoal,
                Text = item.title,
                Bounds = new Rectangle(0, 6, 240, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                AutoEllipsis = true
            };
            string meta = item.days_late + (item.days_late == 1 ? " day late" : " days late") + "  ·  " + item.updates + " update(s)";
            var metaLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Text = meta,
                Bounds = new Rectangle(0, 26, 240, 16),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var status = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = item.done ? RpvTheme.Success : RpvTheme.Danger,
                Text = item.done ? "Resolved" : "Overdue",
                Bounds = new Rectangle(0, 6, 90, 34),
                TextAlign = ContentAlignment.MiddleRight
            };

            row.Controls.Add(title);
            row.Controls.Add(metaLabel);
            row.Controls.Add(status);
            row.Resize += (s, e) =>
            {
                status.Left = Math.Max(0, row.ClientSize.Width - status.Width);
                int textWidth = Math.Max(60, status.Left - RpvTheme.Space3);
                title.Width = textWidth;
                metaLabel.Width = textWidth;
            };

            return row;
        }

        private void RenderHistory()
        {
            historyBody.SuspendLayout();
            historyBody.Controls.Clear();

            IList<ActivityInterval> all = service.Intervals;
            if (all.Count == 0)
            {
                historyBody.Controls.Add(new Label
                {
                    Dock = DockStyle.Top,
                    Height = 44,
                    BackColor = RpvTheme.White,
                    Font = RpvTheme.FontBody,
                    ForeColor = RpvTheme.Stone,
                    Text = "Completed intervals will appear here.",
                    TextAlign = ContentAlignment.MiddleLeft
                });
                historyBody.ResumeLayout();
                return;
            }

            var rows = new List<Control>();
            int limit = Math.Min(all.Count, 5);
            for (int i = all.Count - 1, shown = 0; i >= 0 && shown < limit; i--, shown++)
            {
                rows.Add(BuildHistoryRow(all[i], shown < limit - 1));
            }
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                rows[i].Dock = DockStyle.Top;
                historyBody.Controls.Add(rows[i]);
            }
            historyBody.ResumeLayout();
        }

        private Panel BuildHistoryRow(ActivityInterval interval, bool withDivider)
        {
            var row = new Panel { Height = 48, BackColor = RpvTheme.White };
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

            var range = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Charcoal,
                Text = interval.TimeRange,
                Bounds = new Rectangle(0, 6, 240, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var meta = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Text = interval.KeyCount + " taps · " + interval.ClickCount + " clicks",
                Bounds = new Rectangle(0, 26, 240, 18),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var activity = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Terracotta,
                Text = interval.ActivityPercent + "%",
                Bounds = new Rectangle(0, 6, 80, 36),
                TextAlign = ContentAlignment.MiddleRight
            };

            row.Controls.Add(range);
            row.Controls.Add(meta);
            row.Controls.Add(activity);
            row.Resize += (s, e) =>
            {
                activity.Left = Math.Max(0, row.ClientSize.Width - activity.Width);
                int textWidth = Math.Max(60, activity.Left - RpvTheme.Space3);
                range.Width = textWidth;
                meta.Width = textWidth;
            };

            return row;
        }

        private void ShowPreview(string path)
        {
            try
            {
                using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                using (var loaded = Image.FromStream(stream))
                {
                    Image previous = previewBox.Image;
                    previewBox.Image = new Bitmap(loaded);
                    if (previous != null)
                    {
                        previous.Dispose();
                    }
                }
                previewCaption.Text = Path.GetFileName(path) + "  ·  " + DateTime.Now.ToString("h:mm:ss tt");
                openFolderButton.Enabled = true;
            }
            catch (Exception)
            {
                previewCaption.Text = "Captured, but the preview could not be loaded.";
            }
        }

        private void progressTrack_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(RpvTheme.White);
            RpvTheme.EnableSmoothing(g);

            var track = new Rectangle(0, 0, progressTrack.Width - 1, progressTrack.Height - 1);
            using (var path = RpvTheme.RoundedRect(track, progressTrack.Height / 2))
            using (var brush = new SolidBrush(RpvTheme.Mist))
            {
                g.FillPath(brush, path);
            }

            int fillWidth = (int)Math.Round(track.Width * Math.Max(0f, Math.Min(1f, progressFraction)));
            if (fillWidth > 2)
            {
                var fill = new Rectangle(0, 0, fillWidth, track.Height);
                using (var path = RpvTheme.RoundedRect(fill, track.Height / 2))
                using (var brush = new SolidBrush(RpvTheme.Terracotta))
                {
                    g.FillPath(brush, path);
                }
            }
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
            controlCard.SetBounds(0, y, width, ControlCardHeight);
            LayoutControlCard(width);

            y += ControlCardHeight + RpvTheme.Space5;
            LayoutStatRow(0, y, width);

            y += StatsHeight + RpvTheme.Space5;
            int gap = RpvTheme.Space4;
            int perfWidth = (int)((width - gap) * 0.40);
            int latesWidth = width - gap - perfWidth;
            perfCard.SetBounds(0, y, perfWidth, PerfRowHeight);
            LayoutPerfCard(perfWidth);
            latesCard.SetBounds(perfWidth + gap, y, latesWidth, PerfRowHeight);

            y += PerfRowHeight + gap;
            noticesCard.SetBounds(0, y, width, NoticesHeight);

            y += NoticesHeight + RpvTheme.Space5;
            int previewWidth = (int)((width - gap) * 0.46);
            int historyWidth = width - gap - previewWidth;
            previewCard.SetBounds(0, y, previewWidth, BottomHeight);
            historyCard.SetBounds(previewWidth + gap, y, historyWidth, BottomHeight);

            content.Height = y + BottomHeight;
        }

        private void LayoutControlCard(int width)
        {
            int pad = RpvTheme.Space5;

            // Full-width task selector at the top so long task titles are readable.
            taskCaption.SetBounds(pad, 12, width - (pad * 2), 16);
            taskSelector.SetBounds(pad, 32, width - (pad * 2), 26);
            taskSelector.DropDownWidth = Math.Max(taskSelector.Width, 640);

            statusDot.SetBounds(pad, 74, 18, 22);
            statusLabel.SetBounds(pad + 22, 74, width - (pad * 2) - 22, 22);
            elapsedLabel.SetBounds(pad - 2, 98, width - (pad * 2) - 190, 46);
            toggleButton.SetBounds(width - pad - toggleButton.Width, 100, toggleButton.Width, 42);
            subLine.SetBounds(pad, 150, width - (pad * 2), 18);
            progressTrack.SetBounds(pad, ControlCardHeight - 12, width - (pad * 2), 6);
        }

        private void LayoutPerfCard(int width)
        {
            scoreBig.SetBounds(24, 46, width - 48, 46);
            scoreCaption.SetBounds(24, 94, width - 48, 18);

            int rowY = 124;
            for (int i = 0; i < countName.Length; i++)
            {
                countName[i].SetBounds(24, rowY, width - 48 - 90, 22);
                countValue[i].SetBounds(width - 24 - 90, rowY, 90, 22);
                rowY += 27;
            }
        }

        private void LayoutStatRow(int x, int y, int width)
        {
            StatCard[] cards = { activityStat, keysStat, clicksStat, shotsStat };
            int gap = RpvTheme.Space4;
            int cardWidth = (width - (gap * (cards.Length - 1))) / cards.Length;
            int cursor = x;
            for (int i = 0; i < cards.Length; i++)
            {
                int w = i == cards.Length - 1 ? (x + width) - cursor : cardWidth;
                cards[i].SetBounds(cursor, y, w, StatsHeight);
                cursor += w + gap;
            }
        }

        // --------------------------------------------------------------- helpers

        private static Color ScoreColor(int score)
        {
            if (score >= 80)
            {
                return RpvTheme.Success;
            }
            return score >= 60 ? RpvTheme.Warning : RpvTheme.Danger;
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

        private static Label MakeCardLabel(Font font, Color color, string text, ContentAlignment align)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = font,
                ForeColor = color,
                Text = text,
                UseMnemonic = false,
                TextAlign = align
            };
        }

        private static Label MakeCardHeader(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontH3,
                ForeColor = RpvTheme.Midnight,
                Height = 34,
                Text = text,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static string FormatElapsed(TimeSpan span)
        {
            return ((int)span.TotalHours).ToString("00") + span.ToString("\\:mm\\:ss");
        }

        private static string FormatShort(int totalSeconds)
        {
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                service.Ticked -= service_Ticked;
                service.IntervalCompleted -= service_IntervalCompleted;
                service.StateChanged -= service_StateChanged;

                if (previewBox != null && previewBox.Image != null)
                {
                    previewBox.Image.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
