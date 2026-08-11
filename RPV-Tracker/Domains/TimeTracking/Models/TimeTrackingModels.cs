using System;

namespace RPV_Tracker.Domains.TimeTracking.Models
{
    /// <summary>
    /// One completed tracking interval: the counts gathered over it, how much of it was
    /// active, and the screenshot taken at its close.
    /// </summary>
    internal class ActivityInterval
    {
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }

        /// <summary>Number of key presses during the interval. The identity of the keys is never recorded.</summary>
        public long KeyCount { get; set; }

        /// <summary>Number of mouse button presses during the interval.</summary>
        public long ClickCount { get; set; }

        /// <summary>Seconds within the interval that saw at least one key press or click.</summary>
        public int ActiveSeconds { get; set; }

        public int TotalSeconds { get; set; }

        public string ScreenshotPath { get; set; }

        /// <summary>Populated instead of <see cref="ScreenshotPath"/> when the capture failed.</summary>
        public string ScreenshotError { get; set; }

        /// <summary>
        /// Share of the interval spent active, 0–100. Derived from active seconds rather
        /// than raw counts so it can't be inflated by holding a key down.
        /// </summary>
        public int ActivityPercent
        {
            get
            {
                if (TotalSeconds <= 0)
                {
                    return 0;
                }
                return (int)Math.Round(ActiveSeconds * 100.0 / TotalSeconds);
            }
        }

        public string TimeRange
        {
            get { return StartedAt.ToString("h:mm:ss tt") + " – " + EndedAt.ToString("h:mm:ss tt"); }
        }
    }

    /// <summary>Immutable view of the tracker's live state, pushed to the UI on every tick.</summary>
    internal struct TrackingSnapshot
    {
        public bool IsTracking { get; set; }
        public TimeSpan SessionElapsed { get; set; }
        public int IntervalElapsedSeconds { get; set; }
        public int IntervalLengthSeconds { get; set; }
        public long SessionKeys { get; set; }
        public long SessionClicks { get; set; }

        /// <summary>Live activity percentage for the interval in progress.</summary>
        public int LiveActivityPercent { get; set; }

        /// <summary>Screenshots captured so far this session.</summary>
        public int ScreenshotCount { get; set; }

        /// <summary>Seconds until the next screenshot, never below zero.</summary>
        public int SecondsUntilNextShot
        {
            get { return Math.Max(0, IntervalLengthSeconds - IntervalElapsedSeconds); }
        }
    }

    /// <summary>Summary of a finished tracking session, emitted when tracking stops.</summary>
    internal class TrackingSessionSummary
    {
        public int? TaskId { get; set; }
        public string TaskTitle { get; set; }
        public string SessionId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public int ScreenshotCount { get; set; }
        public long TotalKeys { get; set; }
        public long TotalClicks { get; set; }
        public int ActivePercent { get; set; }

        /// <summary>True when the operator flagged this session as overtime (OT) when starting it.</summary>
        public bool IsOvertime { get; set; }
    }
}
