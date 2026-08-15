using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using RPV_Tracker.Domains.TimeTracking.Models;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Domains.TimeTracking.Services
{
    /// <summary>
    /// Drives a tracking session: a one-second heartbeat measures activity, and every
    /// <see cref="IntervalLengthSeconds"/> it closes an interval and captures a screenshot.
    /// </summary>
    /// <remarks>
    /// A <see cref="System.Windows.Forms.Timer"/> is used deliberately: its Tick runs on the
    /// UI thread, as do the input-hook callbacks, so counters and UI updates all touch the
    /// same thread and need no locking. The screenshot capture is synchronous inside the tick;
    /// grabbing the screen takes tens of milliseconds once per interval, far too rare to be
    /// worth the complexity of marshalling a background capture back to the UI thread.
    /// </remarks>
    internal sealed class TimeTrackingService : IDisposable
    {
        // Global input hooks keep counting keys/clicks regardless of window focus, minimize,
        // or hide state — so idle time is measured off real input, never off window visibility.
        // That's what lets minimizing or hiding the window leave tracking untouched: only a
        // genuine stretch of no keyboard/mouse input stops the session.
        private const int IdleThresholdSeconds = 5 * 60;

        private readonly InputMonitor monitor = new InputMonitor();
        private readonly Timer heartbeat = new Timer();

        // After an idle auto-stop, watches for the operator's very next key/click so a resume
        // offer can appear the moment they're back — rather than leaving them to notice the
        // tray balloon and restart by hand. Polls at a coarser interval than the heartbeat
        // since it only needs to notice that *any* input occurred, not measure it per-second.
        private readonly Timer resumeWatch = new Timer { Interval = 500 };
        private TrackingSessionSummary lastSummary;

        private readonly List<ActivityInterval> intervals = new List<ActivityInterval>();
        private int intervalLength;

        private bool tracking;
        private string sessionFolder;
        private DateTime sessionStart;
        private int sessionSeconds;
        private int idleSeconds;
        private bool autoStoppedIdle;
        private int? activeTaskId;
        private string activeTaskTitle;
        private bool overtime;

        private DateTime intervalStart;
        private int intervalSeconds;
        private int intervalActiveSeconds;
        private long intervalBaseKeys;
        private long intervalBaseClicks;
        private long previousTotal;

        public TimeTrackingService()
        {
            intervalLength = RpvConfig.TrackingIntervalSecondsOverride ?? (AppSettings.ScreenshotIntervalMinutes * 60);
            heartbeat.Interval = 1000;
            heartbeat.Tick += OnHeartbeat;
            resumeWatch.Tick += OnResumeWatchTick;
        }

        /// <summary>
        /// Applies a screenshot interval chosen on the Settings page. Ignored once tracking
        /// has started — the interval that began the session sees it through to the end.
        /// </summary>
        public void SetIntervalMinutes(int minutes)
        {
            if (tracking || RpvConfig.TrackingIntervalSecondsOverride.HasValue)
            {
                return;
            }
            intervalLength = minutes * 60;
        }

        /// <summary>Fired once per second while tracking, and once on start and stop.</summary>
        public event EventHandler<TrackingSnapshot> Ticked;

        /// <summary>Fired when an interval closes and its screenshot has been captured.</summary>
        public event EventHandler<ActivityInterval> IntervalCompleted;

        /// <summary>Fired when tracking starts or stops.</summary>
        public event EventHandler StateChanged;

        /// <summary>
        /// Fired once a session starts, carrying a summary with zeroed totals — lets the
        /// shell push a "session in progress" record to the backend immediately, rather than
        /// the audit trail only learning about a session once it has already ended.
        /// </summary>
        public event EventHandler<TrackingSessionSummary> SessionStarted;

        /// <summary>Fired once when a session stops, carrying its summary (for backend sync).</summary>
        public event EventHandler<TrackingSessionSummary> SessionEnded;

        /// <summary>
        /// Fired once, the moment the operator produces the first key/click after an idle
        /// auto-stop — a chance for the shell to ask whether to resume the same task rather
        /// than silently leaving the stopped session unnoticed.
        /// </summary>
        public event EventHandler<TrackingSessionSummary> IdleResumeSuggested;

        public bool IsTracking { get { return tracking; } }

        public int IntervalLengthSeconds { get { return intervalLength; } }

        public string SessionFolder { get { return sessionFolder; } }

        /// <summary>Stable id for the current session — the screenshot folder's leaf name.</summary>
        public string SessionId
        {
            get { return string.IsNullOrEmpty(sessionFolder) ? null : Path.GetFileName(sessionFolder); }
        }

        public IList<ActivityInterval> Intervals { get { return intervals; } }

        /// <summary>Task id the current (or most recent) session is tracking against.</summary>
        public int? ActiveTaskId { get { return activeTaskId; } }

        /// <summary>Title of the task the current (or most recent) session is tracking.</summary>
        public string ActiveTaskTitle { get { return activeTaskTitle; } }

        /// <summary>True when the current (or most recent) session was flagged as overtime at start.</summary>
        public bool IsOvertimeSession { get { return overtime; } }

        /// <summary>
        /// Starts a session against a task. The tracker gates on the task id (every task has
        /// one), so the caller passes the selected task's id and title; both are recorded with
        /// the session and the id is folded into the screenshot folder name. <paramref name="isOvertime"/>
        /// carries the operator's own OT request — set when they started outside the configured
        /// work schedule and chose to flag it — through to the session summary and history.
        /// </summary>
        public void Start(int? taskId = null, string taskTitle = null, bool isOvertime = false)
        {
            if (tracking)
            {
                return;
            }

            // A fresh, explicit start supersedes any pending idle-resume offer — whether this
            // is the operator accepting it or starting something else entirely.
            EndResumeWatch();

            activeTaskId = taskId;
            activeTaskTitle = taskTitle;
            overtime = isOvertime;

            DateTime now = DateTime.Now;
            sessionStart = now;
            string stamp = now.ToString("yyyyMMdd-HHmmss");
            string leaf = taskId.HasValue ? stamp + "-task-" + taskId.Value : stamp;
            sessionFolder = Path.Combine(RpvConfig.ScreenshotRoot, leaf);
            sessionSeconds = 0;
            idleSeconds = 0;
            autoStoppedIdle = false;
            intervals.Clear();

            monitor.ResetCounts();
            monitor.Start();
            previousTotal = 0;

            BeginInterval();

            tracking = true;
            heartbeat.Start();

            RaiseState();
            RaiseTick();

            EventHandler<TrackingSessionSummary> started = SessionStarted;
            if (started != null)
            {
                started(this, new TrackingSessionSummary
                {
                    TaskId = activeTaskId,
                    TaskTitle = activeTaskTitle,
                    SessionId = SessionId,
                    StartedAt = sessionStart,
                    EndedAt = sessionStart,
                    IsOvertime = overtime
                });
            }
        }

        public void Stop()
        {
            if (!tracking)
            {
                return;
            }

            heartbeat.Stop();

            // Close out the partial interval so the final stretch of work — and a last
            // screenshot — are recorded rather than discarded.
            if (intervalSeconds > 0)
            {
                FinalizeInterval();
            }

            TrackingSessionSummary summary = BuildSummary();
            summary.StoppedByIdle = autoStoppedIdle;
            lastSummary = summary;

            monitor.Stop();
            tracking = false;
            idleSeconds = 0;
            autoStoppedIdle = false;

            RaiseState();
            RaiseTick();

            EventHandler<TrackingSessionSummary> ended = SessionEnded;
            if (ended != null)
            {
                ended(this, summary);
            }
        }

        /// <summary>
        /// Stops the session after <see cref="IdleThresholdSeconds"/> of no keyboard/mouse
        /// input, flagging the summary so the shell can tell the operator why it stopped
        /// rather than leaving it looking like a manual stop.
        /// </summary>
        private void StopForIdle()
        {
            autoStoppedIdle = true;
            Stop();
            BeginResumeWatch();
        }

        /// <summary>Reinstalls the input hooks (without resuming the heartbeat) purely to
        /// detect the operator's next key/click.</summary>
        private void BeginResumeWatch()
        {
            monitor.ResetCounts();
            monitor.Start();
            resumeWatch.Start();
        }

        private void OnResumeWatchTick(object sender, EventArgs e)
        {
            if (monitor.KeyCount + monitor.ClickCount == 0)
            {
                return;
            }

            TrackingSessionSummary summary = lastSummary;
            EndResumeWatch();

            EventHandler<TrackingSessionSummary> handler = IdleResumeSuggested;
            if (handler != null)
            {
                handler(this, summary);
            }
        }

        private void EndResumeWatch()
        {
            resumeWatch.Stop();
            monitor.Stop();
        }

        private TrackingSessionSummary BuildSummary()
        {
            int totalSeconds = 0;
            int activeSeconds = 0;
            int shots = 0;
            foreach (ActivityInterval interval in intervals)
            {
                totalSeconds += interval.TotalSeconds;
                activeSeconds += interval.ActiveSeconds;
                if (!string.IsNullOrEmpty(interval.ScreenshotPath))
                {
                    shots++;
                }
            }

            return new TrackingSessionSummary
            {
                TaskId = activeTaskId,
                TaskTitle = activeTaskTitle,
                SessionId = SessionId,
                StartedAt = sessionStart,
                EndedAt = DateTime.Now,
                ScreenshotCount = shots,
                TotalKeys = monitor.KeyCount,
                TotalClicks = monitor.ClickCount,
                ActivePercent = totalSeconds > 0 ? (int)Math.Round(activeSeconds * 100.0 / totalSeconds) : 0,
                IsOvertime = overtime
            };
        }

        private void BeginInterval()
        {
            intervalStart = DateTime.Now;
            intervalSeconds = 0;
            intervalActiveSeconds = 0;
            intervalBaseKeys = monitor.KeyCount;
            intervalBaseClicks = monitor.ClickCount;
        }

        private void OnHeartbeat(object sender, EventArgs e)
        {
            long total = monitor.KeyCount + monitor.ClickCount;
            bool activeThisSecond = total > previousTotal;
            previousTotal = total;

            idleSeconds = activeThisSecond ? 0 : idleSeconds + 1;

            sessionSeconds++;
            intervalSeconds++;
            if (activeThisSecond)
            {
                intervalActiveSeconds++;
            }

            if (intervalSeconds >= intervalLength)
            {
                FinalizeInterval();
                BeginInterval();
            }

            RaiseTick();

            if (idleSeconds >= IdleThresholdSeconds)
            {
                StopForIdle();
            }
        }

        private void FinalizeInterval()
        {
            var interval = new ActivityInterval
            {
                StartedAt = intervalStart,
                EndedAt = DateTime.Now,
                KeyCount = monitor.KeyCount - intervalBaseKeys,
                ClickCount = monitor.ClickCount - intervalBaseClicks,
                ActiveSeconds = intervalActiveSeconds,
                TotalSeconds = intervalSeconds
            };

            try
            {
                interval.ScreenshotPath = ScreenshotService.Capture(sessionFolder, AppSettings.ResolveCaptureBounds());
            }
            catch (Exception ex)
            {
                // A failed capture (locked screen, permissions) must not tear down the
                // session — record why and keep tracking.
                interval.ScreenshotError = ex.Message;
            }

            intervals.Add(interval);

            EventHandler<ActivityInterval> handler = IntervalCompleted;
            if (handler != null)
            {
                handler(this, interval);
            }
        }

        private void RaiseTick()
        {
            EventHandler<TrackingSnapshot> handler = Ticked;
            if (handler == null)
            {
                return;
            }

            handler(this, new TrackingSnapshot
            {
                IsTracking = tracking,
                SessionElapsed = TimeSpan.FromSeconds(sessionSeconds),
                IntervalElapsedSeconds = intervalSeconds,
                IntervalLengthSeconds = intervalLength,
                SessionKeys = monitor.KeyCount,
                SessionClicks = monitor.ClickCount,
                LiveActivityPercent = intervalSeconds == 0
                    ? 0
                    : (int)Math.Round(intervalActiveSeconds * 100.0 / intervalSeconds),
                ScreenshotCount = intervals.Count
            });
        }

        private void RaiseState()
        {
            EventHandler handler = StateChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            heartbeat.Stop();
            heartbeat.Dispose();
            resumeWatch.Stop();
            resumeWatch.Dispose();
            monitor.Dispose();
        }
    }
}
