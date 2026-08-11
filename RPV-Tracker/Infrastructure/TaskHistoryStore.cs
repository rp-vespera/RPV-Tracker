using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using RPV_Tracker.Domains.TimeTracking.Models;

namespace RPV_Tracker.Infrastructure
{
    /// <summary>One completed tracking session, as shown on the Task history page.</summary>
    public class TaskHistoryEntry
    {
        public TaskHistoryEntry() { }

        public int? TaskId;
        public string TaskTitle;
        public string SessionId;
        public DateTime StartedAt;
        public DateTime EndedAt;
        public int ScreenshotCount;
        public int ActivePercent;
        public bool IsOvertime;
    }

    /// <summary>
    /// Local, append-only log of finished tracking sessions — when each one started and
    /// finished — so the Task history page has something to show across app restarts.
    /// </summary>
    internal static class TaskHistoryStore
    {
        private const int MaxEntries = 1000;

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RPV Workforce", "task-history.json");

        public static void Append(TrackingSessionSummary summary)
        {
            List<TaskHistoryEntry> entries = LoadAll();
            entries.Add(new TaskHistoryEntry
            {
                TaskId = summary.TaskId,
                TaskTitle = summary.TaskTitle,
                SessionId = summary.SessionId,
                StartedAt = summary.StartedAt,
                EndedAt = summary.EndedAt,
                ScreenshotCount = summary.ScreenshotCount,
                ActivePercent = summary.ActivePercent,
                IsOvertime = summary.IsOvertime
            });

            if (entries.Count > MaxEntries)
            {
                entries.RemoveRange(0, entries.Count - MaxEntries);
            }

            Save(entries);
        }

        /// <summary>All recorded sessions, oldest first.</summary>
        public static List<TaskHistoryEntry> LoadAll()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var serializer = new JavaScriptSerializer();
                    List<TaskHistoryEntry> loaded =
                        serializer.Deserialize<List<TaskHistoryEntry>>(File.ReadAllText(FilePath));
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception)
            {
                // Corrupt or unreadable history file — behave as if there's no history yet.
            }

            return new List<TaskHistoryEntry>();
        }

        private static void Save(List<TaskHistoryEntry> entries)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var serializer = new JavaScriptSerializer();
                File.WriteAllText(FilePath, serializer.Serialize(entries));
            }
            catch (Exception)
            {
                // Best-effort persistence — a failed write must never interrupt tracking.
            }
        }
    }
}
