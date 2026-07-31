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
        private readonly InputMonitor monitor = new InputMonitor();
        private readonly Timer heartbeat = new Timer();
        private readonly List<ActivityInterval> intervals = new List<ActivityInterval>();
        private readonly int intervalLength;

        private bool tracking;
        private string sessionFolder;
        private DateTime sessionStart;
        private int sessionSeconds;
        private int? activeTaskId;
        private string activeTaskTitle;

        private DateTime intervalStart;
        private int intervalSeconds;
        private int intervalActiveSeconds;
        private long intervalBaseKeys;
        private long intervalBaseClicks;
        private long previousTotal;

        public TimeTrackingService()
        {
            intervalLength = RpvConfig.TrackingIntervalSeconds;
            heartbeat.Interval = 1000;
            heartbeat.Tick += OnHeartbeat;
        }

        /// <summary>Fired once per second while tracking, and once on start and stop.</summary>
        public event EventHandler<TrackingSnapshot> Ticked;

        /// <summary>Fired when an interval closes and its screenshot has been captured.</summary>
        public event EventHandler<ActivityInterval> IntervalCompleted;

        /// <summary>Fired when tracking starts or stops.</summary>
        public event EventHandler StateChanged;

        /// <summary>Fired once when a session stops, carrying its summary (for backend sync).</summary>
        public event EventHandler<TrackingSessionSummary> SessionEnded;

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

        /// <summary>
        /// Starts a session against a task. The tracker gates on the task id (every task has
        /// one), so the caller passes the selected task's id and title; both are recorded with
        /// the session and the id is folded into the screenshot folder name.
        /// </summary>
        public void Start(int? taskId = null, string taskTitle = null)
        {
            if (tracking)
            {
                return;
            }

            activeTaskId = taskId;
            activeTaskTitle = taskTitle;

            DateTime now = DateTime.Now;
            sessionStart = now;
            string stamp = now.ToString("yyyyMMdd-HHmmss");
            string leaf = taskId.HasValue ? stamp + "-task-" + taskId.Value : stamp;
            sessionFolder = Path.Combine(RpvConfig.ScreenshotRoot, leaf);
            sessionSeconds = 0;
            intervals.Clear();

            monitor.ResetCounts();
            monitor.Start();
            previousTotal = 0;

            BeginInterval();

            tracking = true;
            heartbeat.Start();

            RaiseState();
            RaiseTick();
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

            monitor.Stop();
            tracking = false;

            RaiseState();
            RaiseTick();

            EventHandler<TrackingSessionSummary> ended = SessionEnded;
            if (ended != null)
            {
                ended(this, summary);
            }
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
                ActivePercent = totalSeconds > 0 ? (int)Math.Round(activeSeconds * 100.0 / totalSeconds) : 0
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
                interval.ScreenshotPath = ScreenshotService.Capture(sessionFolder);
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
            monitor.Dispose();
        }
    }
}
