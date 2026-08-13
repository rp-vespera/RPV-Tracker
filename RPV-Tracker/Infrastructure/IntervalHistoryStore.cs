using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using RPV_Tracker.Domains.TimeTracking.Models;

namespace RPV_Tracker.Infrastructure
{
    /// <summary>One completed interval, kept for the Report page's day view.</summary>
    public class IntervalHistoryEntry
    {
        public IntervalHistoryEntry() { }

        public string SessionId;
        public int? TaskId;
        public string TaskTitle;
        public DateTime StartedAt;
        public DateTime EndedAt;
        public long KeyCount;
        public long ClickCount;
        public int ActiveSeconds;
        public int TotalSeconds;
        public int ActivityPercent;
        public bool IsOvertime;
        public bool HasScreenshot;
    }

    /// <summary>
    /// Local log of every completed interval, not just the per-session averages
    /// <see cref="TaskHistoryStore"/> keeps.
    /// </summary>
    /// <remarks>
    /// A session average tells you a day was "24% active" and nothing more; the shape of the
    /// day — which stretches were heads-down and which were meetings — only exists at interval
    /// resolution, and that data used to live in memory and die with the process. Interval rows
    /// are small (no image, no keystrokes) so a generous cap still covers months: at a 5-minute
    /// cadence an 8-hour day is ~96 rows, so 40,000 is roughly a working year.
    /// </remarks>
    internal static class IntervalHistoryStore
    {
        private const int MaxEntries = 40000;

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RPV Workforce", "interval-history.json");

        public static void Append(ActivityInterval interval, string sessionId, int? taskId, string taskTitle, bool isOvertime)
        {
            if (interval == null)
            {
                return;
            }

            List<IntervalHistoryEntry> entries = LoadAll();
            entries.Add(new IntervalHistoryEntry
            {
                SessionId = sessionId,
                TaskId = taskId,
                TaskTitle = taskTitle,
                StartedAt = interval.StartedAt,
                EndedAt = interval.EndedAt,
                KeyCount = interval.KeyCount,
                ClickCount = interval.ClickCount,
                ActiveSeconds = interval.ActiveSeconds,
                TotalSeconds = interval.TotalSeconds,
                ActivityPercent = interval.ActivityPercent,
                IsOvertime = isOvertime,
                HasScreenshot = !string.IsNullOrEmpty(interval.ScreenshotPath)
            });

            if (entries.Count > MaxEntries)
            {
                entries.RemoveRange(0, entries.Count - MaxEntries);
            }

            Save(entries);
        }

        /// <summary>Every recorded interval, oldest first.</summary>
        public static List<IntervalHistoryEntry> LoadAll()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    List<IntervalHistoryEntry> loaded =
                        serializer.Deserialize<List<IntervalHistoryEntry>>(File.ReadAllText(FilePath));
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Write("history", "Interval history unreadable, starting empty: " + ex.Message);
            }

            return new List<IntervalHistoryEntry>();
        }

        /// <summary>Intervals that started on <paramref name="day"/>, in chronological order.</summary>
        public static List<IntervalHistoryEntry> LoadForDay(DateTime day)
        {
            var result = new List<IntervalHistoryEntry>();
            foreach (IntervalHistoryEntry entry in LoadAll())
            {
                if (entry.StartedAt.Date == day.Date)
                {
                    result.Add(entry);
                }
            }
            result.Sort((a, b) => a.StartedAt.CompareTo(b.StartedAt));
            return result;
        }

        private static void Save(List<IntervalHistoryEntry> entries)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                File.WriteAllText(FilePath, serializer.Serialize(entries));
            }
            catch (Exception ex)
            {
                // Best-effort, exactly like the session log — a failed write must never
                // interrupt tracking.
                DebugLog.Write("history", "Interval history write failed: " + ex.Message);
            }
        }
    }
}
