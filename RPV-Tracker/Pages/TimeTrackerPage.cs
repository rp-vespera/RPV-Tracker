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
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Pages
{
    /// <summary>
    /// Time-tracker screen. You start by pasting a task id (not a dropdown); the id is
    /// validated against your active/overdue tasks, which are listed beside it for reference.
    /// The page also shows performance — score, counts, notices, and an hour-by-hour
    /// activity chart.
    /// </summary>
    internal class TimeTrackerPage : UserControl
    {
        private const int TitleHeight = 32;
        private const int SubtitleHeight = 40;
        private const int ControlCardHeight = 206;
        private const int StatsHeight = 96;
        private const int PerfRowHeight = 236;
        private const int NoticesHeight = 120;
        private const int BottomHeight = 240;
        private const int ChartHeight = 220;

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
        private readonly Label subLine;
        private readonly RpvField taskIdField;
        private readonly RpvButton toggleButton;
        private readonly CheckBox otCheckbox;
        private readonly Label otNote;
        private readonly Panel progressTrack;
        private readonly Timer scheduleTimer;
        private float progressFraction;
        private bool otPreviouslyOffered;

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

        // active & overdue task list
        private readonly CardPanel tasksCard;
        private readonly Panel tasksBody;

        // notices
        private readonly CardPanel noticesCard;
        private readonly Label noticesLabel;

        // latest screenshot + interval history
        private readonly CardPanel previewCard;
        private readonly PictureBox previewBox;
        private readonly Label previewCaption;
        private readonly RpvButton openFolderButton;
        private readonly CardPanel historyCard;
        private readonly Panel historyBody;

        // activity per hour
        private readonly CardPanel activityChartCard;
        private readonly ActivityBarChart activityChart;

        private List<PulseTask> tasks = new List<PulseTask>();
        private PulseTask selectedTask;
        private bool loading;
        private string dataError;

        // Cached result of today's server-side attendance check — refreshed on load/Refresh,
        // not on the 30s schedule timer. Null (never fetched, or the fetch failed) fails safe:
        // no cached confirmation of attendance means the OT checkbox stays hidden.
        private AttendanceCheckResult attendanceCheck;

        /// <summary>Why the last attendance check failed, so the OT note can say so instead of
        /// leaving the operator guessing at a generic "couldn't be verified".</summary>
        private string attendanceError;

        /// <summary>Last OT-gate state written to the log, so repeats aren't logged again.</summary>
        private string lastOtGateLog;

        public TimeTrackerPage(TimeTrackingService trackingService)
        {
            service = trackingService;
            BackColor = RpvTheme.Cream;
            DoubleBuffered = true;

            scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = RpvTheme.Cream };
            content = new Panel { BackColor = RpvTheme.Cream };

            titleLabel = MakeLabel(RpvTheme.FontH1, RpvTheme.HeadingText, "Time tracker", TitleHeight);
            refreshLink = new RpvButton { Text = "Refresh", Variant = RpvButtonVariant.Tertiary, Size = new Size(120, 28) };
            refreshLink.Click += async (s, e) => await LoadDataAsync();
            subtitleLabel = MakeLabel(RpvTheme.FontBody, RpvTheme.Stone, BuildDisclosure(), SubtitleHeight);

            // ---- control card ----
            controlCard = new CardPanel();

            statusDot = MakeCardLabel(new Font(RpvTheme.BaseFamily, 14f), RpvTheme.Stone, "●", ContentAlignment.MiddleLeft);
            statusLabel = MakeCardLabel(RpvTheme.FontBodyMedium, RpvTheme.Charcoal, "Not tracking", ContentAlignment.MiddleLeft);
            statusLabel.AutoEllipsis = true;
            elapsedLabel = MakeCardLabel(RpvTheme.FontDisplay, RpvTheme.HeadingText, "00:00:00", ContentAlignment.MiddleLeft);
            subLine = MakeCardLabel(RpvTheme.FontCaption, RpvTheme.Stone, string.Empty, ContentAlignment.MiddleLeft);
            subLine.AutoEllipsis = true;

            taskIdField = new RpvField
            {
                LabelText = "Task ID",
                PlaceholderText = "Paste task ID, e.g. 203",
                BackColor = RpvTheme.CardSurface
            };
            taskIdField.ValueChanged += (s, e) => { ResolveSelectedFromField(); UpdateForState(); };

            toggleButton = new RpvButton { Text = "Start tracking", Variant = RpvButtonVariant.Primary, Size = new Size(220, 42), Enabled = false };
            toggleButton.Click += toggleButton_Click;

            otCheckbox = new CheckBox
            {
                Text = "This is overtime (OT)",
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Charcoal,
                BackColor = RpvTheme.CardSurface,
                FlatStyle = FlatStyle.Flat,
                AutoSize = false,
                Visible = false
            };
            otNote = MakeCardLabel(RpvTheme.FontCaption, RpvTheme.Warning, string.Empty, ContentAlignment.MiddleLeft);
            otNote.Visible = false;

            progressTrack = new Panel { BackColor = RpvTheme.CardSurface, Height = 6 };
            progressTrack.Paint += progressTrack_Paint;

            controlCard.Controls.Add(statusDot);
            controlCard.Controls.Add(statusLabel);
            controlCard.Controls.Add(elapsedLabel);
            controlCard.Controls.Add(subLine);
            controlCard.Controls.Add(taskIdField);
            controlCard.Controls.Add(toggleButton);
            controlCard.Controls.Add(otCheckbox);
            controlCard.Controls.Add(otNote);
            controlCard.Controls.Add(progressTrack);

            // Re-checks OT eligibility on a timer, not just on state changes, so the prompt
            // still appears if the page is simply left open across the schedule boundary.
            scheduleTimer = new Timer { Interval = 30000 };
            scheduleTimer.Tick += (s, e) => UpdateForState();
            scheduleTimer.Start();

            // ---- tracker stat row ----
            activityStat = new StatCard { Label = "Activity", Value = "0%", IsAccent = true };
            keysStat = new StatCard { Label = "Keyboard taps", Value = "0" };
            clicksStat = new StatCard { Label = "Mouse clicks", Value = "0" };
            shotsStat = new StatCard { Label = "Screenshots", Value = "0" };

            // ---- performance summary card ----
            perfCard = new CardPanel();
            Label perfHeader = MakeCardHeader("Your status");
            perfHeader.Dock = DockStyle.Top;
            scoreBig = MakeCardLabel(RpvTheme.FontDisplay, RpvTheme.HeadingText, "—", ContentAlignment.MiddleLeft);
            scoreCaption = MakeCardLabel(RpvTheme.FontCaption, RpvTheme.Stone, "Performance score (out of 100)", ContentAlignment.MiddleLeft);
            perfCard.Controls.Add(scoreBig);
            perfCard.Controls.Add(scoreCaption);

            string[] captions = { "Tasks done", "Overdue", "Unresolved", "Active concerns" };
            for (int i = 0; i < captions.Length; i++)
            {
                countName[i] = MakeCardLabel(RpvTheme.FontBody, RpvTheme.Charcoal, captions[i], ContentAlignment.MiddleLeft);
                countValue[i] = MakeCardLabel(RpvTheme.FontBodyMedium, RpvTheme.HeadingText, "—", ContentAlignment.MiddleRight);
                perfCard.Controls.Add(countName[i]);
                perfCard.Controls.Add(countValue[i]);
            }
            perfCard.Controls.Add(perfHeader);

            // ---- active & overdue task list ----
            tasksCard = new CardPanel();
            Label tasksHeader = MakeCardHeader("Active & overdue tasks");
            tasksHeader.Dock = DockStyle.Top;
            tasksBody = new Panel { Dock = DockStyle.Fill, BackColor = RpvTheme.CardSurface, AutoScroll = true };
            tasksCard.Controls.Add(tasksBody);
            tasksCard.Controls.Add(tasksHeader);

            // ---- notices card ----
            noticesCard = new CardPanel();
            Label noticesHeader = MakeCardHeader("Notices & tips");
            noticesHeader.Dock = DockStyle.Top;
            noticesLabel = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = RpvTheme.CardSurface,
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
                BackColor = RpvTheme.CardSurface,
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
            historyBody = new Panel { Dock = DockStyle.Fill, BackColor = RpvTheme.CardSurface };
            historyCard.Controls.Add(historyBody);
            historyCard.Controls.Add(historyHeader);

            // ---- activity per hour ----
            activityChartCard = new CardPanel();
            Label activityChartHeader = MakeCardHeader("Activity per hour");
            activityChartHeader.Dock = DockStyle.Top;
            activityChart = new ActivityBarChart { Dock = DockStyle.Fill };
            activityChartCard.Controls.Add(activityChart);
            activityChartCard.Controls.Add(activityChartHeader);

            content.Controls.Add(titleLabel);
            content.Controls.Add(refreshLink);
            content.Controls.Add(subtitleLabel);
            content.Controls.Add(controlCard);
            content.Controls.Add(activityStat);
            content.Controls.Add(keysStat);
            content.Controls.Add(clicksStat);
            content.Controls.Add(shotsStat);
            content.Controls.Add(perfCard);
            content.Controls.Add(tasksCard);
            content.Controls.Add(noticesCard);
            content.Controls.Add(previewCard);
            content.Controls.Add(historyCard);
            content.Controls.Add(activityChartCard);

            scrollHost.Controls.Add(content);
            Controls.Add(scrollHost);

            scrollHost.Resize += (s, e) => LayoutContent();

            service.Ticked += service_Ticked;
            service.IntervalCompleted += service_IntervalCompleted;
            service.StateChanged += service_StateChanged;

            RenderPerformance(null);
            RenderTaskList();
            RenderHistory();
            RenderActivityChart();
            UpdateForState();
            LayoutContent();
        }

        private string BuildDisclosure()
        {
            int seconds = service.IntervalLengthSeconds;
            string every = seconds % 60 == 0
                ? (seconds / 60) + (seconds == 60 ? " minute" : " minutes")
                : seconds + " seconds";

            return "Paste a task id from the list to start. While tracking, RPV captures a screenshot every "
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

            AttendanceCheckResult attendance;
            string attendanceFailure = null;
            try
            {
                attendance = await AttendanceService.CheckAsync(DateTime.Today);
            }
            catch (Exception ex)
            {
                // Fails safe — no confirmed attendance means the OT checkbox stays hidden
                // rather than trusting the local schedule setting alone. The reason is kept
                // (and logged) so it can be shown rather than swallowed.
                attendance = null;
                attendanceFailure = ex.Message;
                DebugLog.Exception("attendance", ex);
            }

            if (IsDisposed)
            {
                return;
            }

            loading = false;
            dataError = error;
            attendanceCheck = attendance;
            attendanceError = attendanceFailure;
            tasks = (loadedTasks ?? new List<PulseTask>())
                .OrderByDescending(t => t.CanTrack)
                .ThenByDescending(t => t.DaysLate)
                .ThenByDescending(t => t.active)
                .ToList();

            RenderTaskList();
            RenderPerformance(perf);
            ResolveSelectedFromField();
            UpdateForState();
            LayoutContent();
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
            RenderActivityChart();
        }

        private void service_StateChanged(object sender, EventArgs e)
        {
            UpdateForState();
        }

        // --------------------------------------------------------------- user actions

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

            service.Start(selectedTask.id, selectedTask.title, otCheckbox.Visible && otCheckbox.Checked);
        }

        private void openFolderButton_Click(object sender, EventArgs e)
        {
            string folder = service.SessionFolder;
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\"");
            }
        }

        /// <summary>Digits typed/pasted into the Task ID field, or null if none.</summary>
        private int? EnteredTaskId()
        {
            string raw = taskIdField.Value ?? string.Empty;
            string digits = new string(raw.Where(char.IsDigit).ToArray());
            int id;
            return int.TryParse(digits, out id) ? (int?)id : null;
        }

        private void ResolveSelectedFromField()
        {
            int? id = EnteredTaskId();
            selectedTask = id.HasValue ? tasks.FirstOrDefault(t => t.id == id.Value) : null;
        }

        // --------------------------------------------------------------- gating

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

            int? id = EnteredTaskId();
            if (!id.HasValue)
            {
                reason = "Paste a task id from the list to start.";
                return false;
            }
            if (selectedTask == null)
            {
                reason = "No task found with id #" + id.Value + ".";
                return false;
            }
            if (selectedTask.IsBlocked)
            {
                string why = string.IsNullOrWhiteSpace(selectedTask.blocked_reason) ? "see the task details" : selectedTask.blocked_reason;
                reason = "Task #" + id.Value + " is blocked — " + why + ".";
                return false;
            }
            if (selectedTask.IsDone)
            {
                reason = "Task #" + id.Value + " is already done — pick an active task.";
                return false;
            }

            reason = "Ready to track #" + selectedTask.id + " · " + selectedTask.title;
            return true;
        }

        // --------------------------------------------------------------- rendering

        private void UpdateForState()
        {
            bool on = service.IsTracking;

            taskIdField.Enabled = !on && !loading;
            refreshLink.Enabled = !on && !loading;

            if (on)
            {
                statusDot.ForeColor = RpvTheme.Danger;
                string task = service.ActiveTaskId.HasValue ? "#" + service.ActiveTaskId + " · " : string.Empty;
                string otTag = service.IsOvertimeSession ? "  ·  OT" : string.Empty;
                statusLabel.Text = "Tracking  ·  " + task + (service.ActiveTaskTitle ?? string.Empty) + otTag;
                toggleButton.Text = "Stop tracking";
                toggleButton.Variant = RpvButtonVariant.Secondary;
                toggleButton.Enabled = true;
                openFolderButton.Enabled = !string.IsNullOrEmpty(service.SessionFolder);
                taskIdField.HasError = false;
                otCheckbox.Visible = false;
                otNote.Visible = false;
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
            subLine.ForeColor = canStart ? RpvTheme.Steel : (dataError != null ? RpvTheme.Danger : RpvTheme.Warning);

            // Red field border only once something invalid has actually been entered.
            taskIdField.HasError = !canStart && EnteredTaskId().HasValue && dataError == null;

            // Starting outside the configured work schedule offers an OT request — but only
            // when today's server-side attendance check confirms the employee actually worked
            // the morning or afternoon half of the day (see AttendanceService/HrTapsController
            // ::attendanceCheck). Checked by default the moment it appears, but the operator
            // can uncheck it before starting.
            bool outsideSchedule = AppSettings.IsOutsideSchedule(DateTime.Now);
            bool canRequestOt = outsideSchedule && attendanceCheck != null && attendanceCheck.CanRequestOvertime;

            LogOtGate(outsideSchedule, canRequestOt);

            otCheckbox.Visible = canRequestOt;
            otNote.Visible = outsideSchedule;
            if (canRequestOt)
            {
                if (!otPreviouslyOffered)
                {
                    otCheckbox.Checked = true;
                }
                otNote.Text = "Outside your scheduled hours (" + FormatTimeOfDay(AppSettings.WorkScheduleStart)
                    + " – " + FormatTimeOfDay(AppSettings.WorkScheduleEnd) + ") — check to request overtime.";
            }
            else if (outsideSchedule)
            {
                if (attendanceCheck == null)
                {
                    // Naming the failure turns "it just doesn't work" into something the
                    // operator (or their admin) can act on — an expired session, an
                    // unreachable server, or a missing endpoint each need a different fix.
                    otNote.Text = "Outside your scheduled hours, but today's attendance couldn't be verified"
                        + (string.IsNullOrWhiteSpace(attendanceError) ? "." : ": " + attendanceError)
                        + " Overtime can't be requested right now.";
                }
                else
                {
                    otNote.Text = "Outside your scheduled hours, but no attendance is on file for today"
                        + " (morning: " + (attendanceCheck.WorkedMorning ? "yes" : "no")
                        + ", afternoon: " + (attendanceCheck.WorkedAfternoon ? "yes" : "no")
                        + ") — overtime can't be requested.";
                }
            }
            otPreviouslyOffered = canRequestOt;
        }

        private static string FormatTimeOfDay(TimeSpan time)
        {
            return DateTime.Today.Add(time).ToString("h:mm tt");
        }

        /// <summary>
        /// Records every input to the OT decision, but only when the outcome actually changes —
        /// <see cref="UpdateForState"/> runs on a 30-second timer and on every keystroke in the
        /// task field, which would otherwise bury the log.
        /// </summary>
        private void LogOtGate(bool outsideSchedule, bool canRequestOt)
        {
            string state = "outsideSchedule=" + outsideSchedule
                + ", schedule=" + FormatTimeOfDay(AppSettings.WorkScheduleStart)
                + "–" + FormatTimeOfDay(AppSettings.WorkScheduleEnd)
                + ", day=" + DateTime.Now.DayOfWeek
                + ", attendanceCheck=" + (attendanceCheck == null
                    ? "null (" + (attendanceError ?? "not fetched") + ")"
                    : "has=" + attendanceCheck.HasAttendance
                        + "/am=" + attendanceCheck.WorkedMorning
                        + "/pm=" + attendanceCheck.WorkedAfternoon
                        + "/canOt=" + attendanceCheck.CanRequestOvertime)
                + " ⇒ OT offered=" + canRequestOt;

            if (state == lastOtGateLog)
            {
                return;
            }

            lastOtGateLog = state;
            DebugLog.Write("ot", state);
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
                noticesLabel.Text = dataError != null ? "Performance couldn't be loaded." : "No notices right now.";
                return;
            }

            scoreBig.Text = perf.score.ToString();
            scoreBig.ForeColor = ScoreColor(perf.score);
            countValue[0].Text = perf.tasks_done + " / " + perf.tasks_total;
            countValue[1].Text = perf.overdue_tasks.ToString();
            countValue[2].Text = perf.unresolved_tasks.ToString();
            countValue[3].Text = perf.active_concerns.ToString();

            noticesLabel.Text = (perf.tips != null && perf.tips.Count > 0)
                ? string.Join("\n\n", perf.tips.Select(t => "•  " + t))
                : "No notices right now — nice work.";
        }

        /// <summary>Minimalist list of active (not-done) tasks, most overdue first.</summary>
        private void RenderTaskList()
        {
            tasksBody.SuspendLayout();
            tasksBody.Controls.Clear();

            List<PulseTask> active = tasks
                .Where(t => !t.IsDone)
                .OrderByDescending(t => t.DaysLate)
                .ThenByDescending(t => t.active)
                .ToList();

            if (active.Count == 0)
            {
                tasksBody.Controls.Add(new Label
                {
                    Dock = DockStyle.Top,
                    Height = 44,
                    BackColor = RpvTheme.CardSurface,
                    Font = RpvTheme.FontBody,
                    ForeColor = RpvTheme.Stone,
                    Text = dataError != null ? "Couldn't load tasks." : "No active or overdue tasks.",
                    TextAlign = ContentAlignment.MiddleLeft
                });
                tasksBody.ResumeLayout();
                return;
            }

            var rows = new List<Control>();
            for (int i = 0; i < active.Count; i++)
            {
                rows.Add(BuildTaskRow(active[i], i < active.Count - 1));
            }
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                rows[i].Dock = DockStyle.Top;
                tasksBody.Controls.Add(rows[i]);
            }
            tasksBody.ResumeLayout();
        }

        private Panel BuildTaskRow(PulseTask task, bool withDivider)
        {
            var row = new Panel { Height = 52, BackColor = RpvTheme.CardSurface, Cursor = Cursors.Hand };
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

            var idLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Steel,
                Text = "#" + task.id,
                Bounds = new Rectangle(0, 0, 64, 52),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var titleLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Charcoal,
                Text = task.title,
                Bounds = new Rectangle(74, 8, 240, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                AutoEllipsis = true
            };
            var metaLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Text = string.IsNullOrEmpty(task.due_at) ? "No deadline" : "Due " + task.due_at,
                Bounds = new Rectangle(74, 28, 240, 16),
                TextAlign = ContentAlignment.MiddleLeft
            };
            bool overdue = task.DaysLate > 0;
            var statusLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontMicro,
                ForeColor = overdue ? RpvTheme.Danger : RpvTheme.Steel,
                Text = overdue ? task.DaysLate + (task.DaysLate == 1 ? " day late" : " days late") : "Active",
                Bounds = new Rectangle(0, 0, 96, 52),
                TextAlign = ContentAlignment.MiddleRight
            };

            row.Controls.Add(idLabel);
            row.Controls.Add(titleLabel);
            row.Controls.Add(metaLabel);
            row.Controls.Add(statusLabel);

            row.Resize += (s, e) =>
            {
                statusLabel.Left = Math.Max(0, row.ClientSize.Width - statusLabel.Width);
                int textWidth = Math.Max(60, statusLabel.Left - 74 - RpvTheme.Space3);
                titleLabel.Width = textWidth;
                metaLabel.Width = textWidth;
            };

            // The whole row fills the Task ID field — a convenience on top of pasting.
            EventHandler fill = (s, e) =>
            {
                taskIdField.Value = task.id.ToString();
                taskIdField.Focus();
            };
            row.Click += fill;
            foreach (Control child in row.Controls)
            {
                child.Cursor = Cursors.Hand;
                child.Click += fill;
            }

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
                    BackColor = RpvTheme.CardSurface,
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
            var row = new Panel { Height = 48, BackColor = RpvTheme.CardSurface };
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
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Charcoal,
                Text = interval.TimeRange,
                Bounds = new Rectangle(0, 6, 240, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var meta = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Text = interval.KeyCount + " taps · " + interval.ClickCount + " clicks",
                Bounds = new Rectangle(0, 26, 240, 18),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var activity = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.CardSurface,
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

        /// <summary>Buckets completed intervals by the hour they started and averages their activity %.</summary>
        private void RenderActivityChart()
        {
            IList<ActivityInterval> all = service.Intervals;

            var hourOrder = new List<int>();
            var sums = new Dictionary<int, int>();
            var counts = new Dictionary<int, int>();

            foreach (ActivityInterval interval in all)
            {
                int hour = interval.StartedAt.Hour;
                if (!sums.ContainsKey(hour))
                {
                    sums[hour] = 0;
                    counts[hour] = 0;
                    hourOrder.Add(hour);
                }
                sums[hour] += interval.ActivityPercent;
                counts[hour]++;
            }

            var buckets = new List<ActivityBarChart.Bucket>();
            foreach (int hour in hourOrder)
            {
                buckets.Add(new ActivityBarChart.Bucket
                {
                    Label = FormatHourLabel(hour),
                    Percent = (int)Math.Round(sums[hour] / (double)counts[hour])
                });
            }

            activityChart.SetData(buckets);
        }

        private static string FormatHourLabel(int hour)
        {
            return DateTime.Today.AddHours(hour).ToString("h tt");
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
            g.Clear(RpvTheme.CardSurface);
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
            int tasksWidth = width - gap - perfWidth;
            perfCard.SetBounds(0, y, perfWidth, PerfRowHeight);
            LayoutPerfCard(perfWidth);
            tasksCard.SetBounds(perfWidth + gap, y, tasksWidth, PerfRowHeight);

            y += PerfRowHeight + gap;
            noticesCard.SetBounds(0, y, width, NoticesHeight);

            y += NoticesHeight + RpvTheme.Space5;
            int previewWidth = (int)((width - gap) * 0.46);
            int historyWidth = width - gap - previewWidth;
            previewCard.SetBounds(0, y, previewWidth, BottomHeight);
            historyCard.SetBounds(previewWidth + gap, y, historyWidth, BottomHeight);

            y += BottomHeight + RpvTheme.Space5;
            activityChartCard.SetBounds(0, y, width, ChartHeight);

            content.Height = y + ChartHeight;

            scrollHost.AutoScrollMinSize = new Size(0, content.Height);
        }

        private void LayoutControlCard(int width)
        {
            int pad = RpvTheme.Space5;
            int colWidth = 240;
            int colX = width - pad - colWidth;

            statusDot.SetBounds(pad, 16, 18, 22);
            statusLabel.SetBounds(pad + 22, 16, colX - pad - 30, 22);
            elapsedLabel.SetBounds(pad - 2, 44, colX - pad - 20, 46);
            subLine.SetBounds(pad, 100, colX - pad - 20, 20);

            taskIdField.SetBounds(colX, 14, colWidth, taskIdField.Height);
            toggleButton.SetBounds(colX, 92, colWidth, 42);

            otCheckbox.SetBounds(pad, 142, width - (pad * 2), 22);
            otNote.SetBounds(pad + 22, 164, width - (pad * 2) - 22, 18);

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
                BackColor = RpvTheme.CardSurface,
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
                BackColor = RpvTheme.CardSurface,
                Font = RpvTheme.FontH3,
                ForeColor = RpvTheme.HeadingText,
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

                scheduleTimer.Stop();
                scheduleTimer.Dispose();

                if (previewBox != null && previewBox.Image != null)
                {
                    previewBox.Image.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
