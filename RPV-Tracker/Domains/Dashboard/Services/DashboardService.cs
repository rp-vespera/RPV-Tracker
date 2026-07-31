using System.Threading.Tasks;
using RPV_Tracker.Domains.Dashboard.Models;

namespace RPV_Tracker.Domains.Dashboard.Services
{
    /// <summary>
    /// Supplies the dashboard's figures and feeds.
    /// </summary>
    /// <remarks>
    /// The RPV API has no dashboard summary endpoint yet, so this returns representative
    /// sample data shaped exactly like the real response should be. When the endpoint
    /// lands, replace the body of <see cref="GetSummaryAsync"/> with the ApiClient call —
    /// the models and every screen bound to them stay as they are.
    /// </remarks>
    internal static class DashboardService
    {
        public static async Task<DashboardSummary> GetSummaryAsync()
        {
            await Task.Delay(150).ConfigureAwait(false);

            var summary = new DashboardSummary();

            summary.Stats.Add(new StatItem { Label = "Total headcount", Value = "1,248" });
            summary.Stats.Add(new StatItem { Label = "Pending your review", Value = "12", IsAccent = true });
            summary.Stats.Add(new StatItem { Label = "On leave today", Value = "37" });
            summary.Stats.Add(new StatItem { Label = "Open requisitions", Value = "8" });

            summary.Approvals.Add(new ApprovalItem
            {
                Title = "Leave request — 3 days",
                Requester = "Marco Alvarez",
                Meta = "Filed 2 days ago · Grounds Maintenance",
                Status = ItemStatus.Pending
            });
            summary.Approvals.Add(new ApprovalItem
            {
                Title = "Overtime filing — 6 hours",
                Requester = "Divina Reyes",
                Meta = "Filed yesterday · Interment Services",
                Status = ItemStatus.Pending
            });
            summary.Approvals.Add(new ApprovalItem
            {
                Title = "Purchase requisition PR-2291",
                Requester = "Noel Bautista",
                Meta = "Filed yesterday · Procurement",
                Status = ItemStatus.Pending
            });
            summary.Approvals.Add(new ApprovalItem
            {
                Title = "Certificate of employment",
                Requester = "Grace Lim",
                Meta = "Filed 4 hours ago · Sales",
                Status = ItemStatus.Info
            });

            summary.Activity.Add(new ActivityItem
            {
                Title = "Payroll cut-off closed",
                Meta = "July 1–15 period",
                TimeAgo = "1h ago"
            });
            summary.Activity.Add(new ActivityItem
            {
                Title = "9 new employees onboarded",
                Meta = "Lawn Services, Vespera",
                TimeAgo = "3h ago"
            });
            summary.Activity.Add(new ActivityItem
            {
                Title = "Q3 goal setting opened",
                Meta = "All departments",
                TimeAgo = "Yesterday"
            });
            summary.Activity.Add(new ActivityItem
            {
                Title = "2 requisitions approved",
                Meta = "Procurement",
                TimeAgo = "Yesterday"
            });

            return summary;
        }
    }
}
