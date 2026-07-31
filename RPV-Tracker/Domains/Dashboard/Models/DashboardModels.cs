using System.Collections.Generic;

namespace RPV_Tracker.Domains.Dashboard.Models
{
    /// <summary>Status vocabulary shared by badges and pills across the app.</summary>
    internal enum ItemStatus
    {
        Approved,
        Pending,
        Rejected,
        Draft,
        Info
    }

    /// <summary>A single headline figure on the dashboard.</summary>
    internal class StatItem
    {
        public string Label { get; set; }
        public string Value { get; set; }

        /// <summary>Renders the number in Terracotta. Reserved for the one figure that needs attention.</summary>
        public bool IsAccent { get; set; }
    }

    /// <summary>A request waiting on the signed-in user.</summary>
    internal class ApprovalItem
    {
        public string Title { get; set; }
        public string Requester { get; set; }
        public string Meta { get; set; }
        public ItemStatus Status { get; set; }
    }

    /// <summary>An entry in the recent activity feed.</summary>
    internal class ActivityItem
    {
        public string Title { get; set; }
        public string Meta { get; set; }
        public string TimeAgo { get; set; }
    }

    /// <summary>Everything the dashboard needs, fetched in one round trip.</summary>
    internal class DashboardSummary
    {
        public DashboardSummary()
        {
            Stats = new List<StatItem>();
            Approvals = new List<ApprovalItem>();
            Activity = new List<ActivityItem>();
        }

        public List<StatItem> Stats { get; set; }
        public List<ApprovalItem> Approvals { get; set; }
        public List<ActivityItem> Activity { get; set; }
    }
}
