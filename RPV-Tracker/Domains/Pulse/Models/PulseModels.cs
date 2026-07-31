using System;
using System.Collections.Generic;

namespace RPV_Tracker.Domains.Pulse.Models
{
    // The data properties below are named to match the API's JSON keys exactly so
    // JavaScriptSerializer binds them without any name mapping. Computed helpers add
    // desktop-friendly views on top and are ignored during deserialization (no setter).

    /// <summary>Response body of GET /api/shared/me.</summary>
    internal class SharedMeResponse
    {
        public List<PulseTask> reminders { get; set; }
        public List<object> habits { get; set; }
    }

    /// <summary>A single task ("reminder") assigned to the signed-in person.</summary>
    internal class PulseTask
    {
        public int id { get; set; }
        public string title { get; set; }
        public string due_at { get; set; }
        public int done { get; set; }
        public string completed_at { get; set; }
        public int? project_id { get; set; }
        public string start_at { get; set; }
        public string started_at { get; set; }
        public string list_name { get; set; }
        public bool active { get; set; }
        public bool needs_update { get; set; }
        public int blocked { get; set; }
        public string blocked_reason { get; set; }
        public TaskHandling handling { get; set; }

        public bool IsDone { get { return done == 1; } }
        public bool IsBlocked { get { return blocked == 1; } }
        public bool HasProject { get { return project_id.HasValue; } }

        /// <summary>
        /// True when this task may be tracked. Tracking keys off the task's own id (every
        /// task has one), so the only bars are that the task isn't already finished or blocked.
        /// </summary>
        public bool CanTrack { get { return !IsDone && !IsBlocked; } }

        public int DaysLate { get { return handling != null ? handling.days_late : 0; } }

        private string ShortTitle
        {
            get
            {
                if (string.IsNullOrEmpty(title))
                {
                    return "(untitled task)";
                }
                return title.Length > 64 ? title.Substring(0, 61) + "…" : title;
            }
        }

        /// <summary>Label shown in the tracker's task dropdown.</summary>
        public string SelectorLabel
        {
            get
            {
                string mark = IsDone ? "✓ " : (active ? "▶ " : "");
                string suffix;
                if (IsDone)
                {
                    suffix = "   ·   done";
                }
                else if (DaysLate > 0)
                {
                    suffix = "   ·   " + DaysLate + "d late";
                }
                else
                {
                    suffix = string.Empty;
                }
                return mark + ShortTitle + suffix;
            }
        }
    }

    internal class TaskHandling
    {
        public int score { get; set; }
        public int days_late { get; set; }
        public List<string> notes { get; set; }
    }

    /// <summary>Response body of GET /api/me/performance.</summary>
    internal class Performance
    {
        public int person_id { get; set; }
        public string name { get; set; }
        public int score { get; set; }
        public int unresolved_tasks { get; set; }
        public int overdue_tasks { get; set; }
        public int tasks_done { get; set; }
        public int tasks_total { get; set; }
        public int mentor_messages { get; set; }
        public int decision_records { get; set; }
        public int escalations { get; set; }
        public int active_concerns { get; set; }
        public List<string> tips { get; set; }
        public List<OverdueItem> overdue_handling { get; set; }
    }

    /// <summary>One overdue/handled task in the performance breakdown — the "lates" list.</summary>
    internal class OverdueItem
    {
        public int task_ref { get; set; }
        public string title { get; set; }
        public string due_at { get; set; }
        public bool done { get; set; }
        public int days_late { get; set; }
        public int updates { get; set; }
        public int mentor_msgs { get; set; }
        public int score { get; set; }
        public List<string> notes { get; set; }
    }
}
