using System;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace RPV_Tracker.Infrastructure
{
    /// <summary>Screenshot cadence choices offered in Settings, in minutes between captures.</summary>
    internal static class ScreenshotIntervals
    {
        public static readonly int[] Minutes = { 5, 10, 20, 30 };
    }

    /// <summary>
    /// User-adjustable preferences personal to this machine: theme, screenshot cadence, and
    /// which monitor(s) to capture. Persisted as JSON under LocalApplicationData — separate
    /// from App.config, which holds deployment-wide values an admin sets once for everyone.
    /// </summary>
    internal static class AppSettings
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RPV Workforce", "settings.json");

        private static readonly Data data = Load();

        /// <summary>Raised after any setting changes and the new value has been saved to disk.</summary>
        public static event EventHandler Changed;

        public static bool DarkMode
        {
            get { return data.DarkMode; }
            set
            {
                if (data.DarkMode == value) return;
                data.DarkMode = value;
                Save();
            }
        }

        /// <summary>Minutes between screenshots. Always one of <see cref="ScreenshotIntervals.Minutes"/>.</summary>
        public static int ScreenshotIntervalMinutes
        {
            get { return data.ScreenshotIntervalMinutes; }
            set
            {
                int clamped = NearestInterval(value);
                if (data.ScreenshotIntervalMinutes == clamped) return;
                data.ScreenshotIntervalMinutes = clamped;
                Save();
            }
        }

        /// <summary>True to capture every monitor (the virtual desktop); false for just <see cref="SelectedMonitorId"/>.</summary>
        public static bool CaptureAllMonitors
        {
            get { return data.CaptureAllMonitors; }
            set
            {
                if (data.CaptureAllMonitors == value) return;
                data.CaptureAllMonitors = value;
                Save();
            }
        }

        /// <summary>Screen.DeviceName of the monitor to capture when <see cref="CaptureAllMonitors"/> is false.</summary>
        public static string SelectedMonitorId
        {
            get { return data.SelectedMonitorId; }
            set
            {
                if (data.SelectedMonitorId == value) return;
                data.SelectedMonitorId = value;
                Save();
            }
        }

        /// <summary>Start of the normal (non-overtime) work day. Defaults to 8:00 AM.</summary>
        public static TimeSpan WorkScheduleStart
        {
            get { return TimeSpan.FromMinutes(data.WorkScheduleStartMinutes); }
            set
            {
                int minutes = ClampToDay(value);
                if (data.WorkScheduleStartMinutes == minutes) return;
                data.WorkScheduleStartMinutes = minutes;
                Save();
            }
        }

        /// <summary>End of the normal (non-overtime) work day. Defaults to 5:00 PM.</summary>
        public static TimeSpan WorkScheduleEnd
        {
            get { return TimeSpan.FromMinutes(data.WorkScheduleEndMinutes); }
            set
            {
                int minutes = ClampToDay(value);
                if (data.WorkScheduleEndMinutes == minutes) return;
                data.WorkScheduleEndMinutes = minutes;
                Save();
            }
        }

        /// <summary>
        /// True when <paramref name="when"/> falls outside the configured work schedule —
        /// a weekend, or before/after the scheduled hours — the trigger for offering an
        /// overtime (OT) request when starting a tracking session.
        /// </summary>
        public static bool IsOutsideSchedule(DateTime when)
        {
            if (when.DayOfWeek == DayOfWeek.Saturday || when.DayOfWeek == DayOfWeek.Sunday)
            {
                return true;
            }

            TimeSpan time = when.TimeOfDay;
            return time < WorkScheduleStart || time >= WorkScheduleEnd;
        }

        private static int ClampToDay(TimeSpan value)
        {
            int minutes = (int)value.TotalMinutes;
            return Math.Max(0, Math.Min(24 * 60, minutes));
        }

        /// <summary>Resolves the current settings to the actual rectangle a screenshot should grab.</summary>
        public static Rectangle ResolveCaptureBounds()
        {
            if (CaptureAllMonitors)
            {
                return SystemInformation.VirtualScreen;
            }

            string wanted = SelectedMonitorId;
            if (!string.IsNullOrEmpty(wanted))
            {
                foreach (Screen screen in Screen.AllScreens)
                {
                    if (screen.DeviceName == wanted)
                    {
                        return screen.Bounds;
                    }
                }
            }

            // Saved monitor unplugged, or none chosen yet — fall back to the primary display
            // rather than silently reverting to a full multi-monitor capture.
            return Screen.PrimaryScreen.Bounds;
        }

        private static int NearestInterval(int minutes)
        {
            int best = ScreenshotIntervals.Minutes[0];
            int bestDiff = Math.Abs(minutes - best);
            foreach (int candidate in ScreenshotIntervals.Minutes)
            {
                int diff = Math.Abs(minutes - candidate);
                if (diff < bestDiff)
                {
                    best = candidate;
                    bestDiff = diff;
                }
            }
            return best;
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var serializer = new JavaScriptSerializer();
                File.WriteAllText(FilePath, serializer.Serialize(data));
            }
            catch (Exception)
            {
                // Settings are a convenience, not critical state — a failed write (locked
                // file, read-only profile) should never crash the app.
            }

            EventHandler handler = Changed;
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        private static Data Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var serializer = new JavaScriptSerializer();
                    Data loaded = serializer.Deserialize<Data>(File.ReadAllText(FilePath));
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception)
            {
                // Corrupt or unreadable settings file — start from defaults rather than crash.
            }

            return new Data();
        }

        /// <summary>Plain data bag for JavaScriptSerializer. Public with an explicit constructor
        /// so reflection-based (de)serialization can instantiate it despite the outer class
        /// otherwise keeping everything internal.</summary>
        public class Data
        {
            public Data() { }

            public bool DarkMode = false;
            public int ScreenshotIntervalMinutes = 20;
            public bool CaptureAllMonitors = true;
            public string SelectedMonitorId = null;
            public int WorkScheduleStartMinutes = 8 * 60;
            public int WorkScheduleEndMinutes = 17 * 60;
        }
    }
}
