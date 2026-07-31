using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;
using RPV_Tracker.Domains.Auth.Models;
using RPV_Tracker.Domains.Auth.Services;
using RPV_Tracker.Domains.Dashboard.Models;
using RPV_Tracker.Domains.Dashboard.Services;

namespace RPV_Tracker.Pages
{
    /// <summary>
    /// The landing screen after sign-in: a personal greeting, headline figures, the
    /// approvals queue, and recent activity. Laid out on the brand's 8pt grid with a
    /// 1200px max content width, centred in whatever space the window gives it.
    /// </summary>
    internal class DashboardPage : UserControl
    {
        private const int GreetingHeight = 36;
        private const int DateHeight = 20;
        private const int StatsHeight = 96;
        private const int SectionsHeight = 392;
        private const int RowHeight = 58;

        private readonly Panel scrollHost;
        private readonly Panel content;
        private readonly Label greetingLabel;
        private readonly Label dateLabel;
        private readonly Panel statsRow;
        private readonly CardPanel approvalsCard;
        private readonly CardPanel activityCard;

        private readonly List<StatCard> statCards = new List<StatCard>();

        public DashboardPage()
        {
            BackColor = RpvTheme.Cream;
            DoubleBuffered = true;

            scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = RpvTheme.Cream
            };

            content = new Panel { BackColor = RpvTheme.Cream };

            greetingLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.Cream,
                Font = RpvTheme.FontH1,
                ForeColor = RpvTheme.Midnight,
                Location = new Point(0, 0),
                Height = GreetingHeight,
                Text = BuildGreeting()
            };

            dateLabel = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.Cream,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Stone,
                Location = new Point(0, GreetingHeight + 2),
                Height = DateHeight,
                Text = DateTime.Now.ToString("dddd, d MMMM yyyy")
            };

            statsRow = new Panel { BackColor = RpvTheme.Cream, Height = StatsHeight };

            approvalsCard = new CardPanel();
            activityCard = new CardPanel();

            content.Controls.Add(greetingLabel);
            content.Controls.Add(dateLabel);
            content.Controls.Add(statsRow);
            content.Controls.Add(approvalsCard);
            content.Controls.Add(activityCard);

            scrollHost.Controls.Add(content);
            Controls.Add(scrollHost);

            scrollHost.Resize += (s, e) => LayoutContent();
            LayoutContent();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            DashboardSummary summary;
            try
            {
                summary = await DashboardService.GetSummaryAsync();
            }
            catch (Exception)
            {
                // The dashboard is read-only; a failed load should leave the shell usable
                // rather than throw an unhandled exception onto the UI thread.
                summary = new DashboardSummary();
            }

            if (IsDisposed)
            {
                return;
            }

            RenderStats(summary.Stats);
            RenderApprovals(summary.Approvals);
            RenderActivity(summary.Activity);
            LayoutContent();
        }

        private static string BuildGreeting()
        {
            AuthenticatedUser user = AppSession.User;
            string name = user != null ? user.FirstName : "there";

            int hour = DateTime.Now.Hour;
            string part;
            if (hour < 12)
            {
                part = "Good morning";
            }
            else if (hour < 18)
            {
                part = "Good afternoon";
            }
            else
            {
                part = "Good evening";
            }

            return part + ", " + name;
        }

        // ------------------------------------------------------------------ layout

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
            content.Top = RpvTheme.Space6;
            content.Width = width;

            greetingLabel.Width = width;
            dateLabel.Width = width;

            int statsTop = GreetingHeight + DateHeight + RpvTheme.Space5;
            statsRow.SetBounds(0, statsTop, width, StatsHeight);
            LayoutStatCards(width);

            int sectionsTop = statsTop + StatsHeight + RpvTheme.Space7;
            int gap = RpvTheme.Space4;
            int leftWidth = (int)((width - gap) * 0.62);
            int rightWidth = width - gap - leftWidth;

            approvalsCard.SetBounds(0, sectionsTop, leftWidth, SectionsHeight);
            activityCard.SetBounds(leftWidth + gap, sectionsTop, rightWidth, SectionsHeight);

            content.Height = sectionsTop + SectionsHeight;
        }

        private void LayoutStatCards(int width)
        {
            if (statCards.Count == 0)
            {
                return;
            }

            int gap = RpvTheme.Space4;
            int count = statCards.Count;
            int cardWidth = (width - (gap * (count - 1))) / count;
            int x = 0;

            for (int i = 0; i < count; i++)
            {
                // The last card absorbs the rounding remainder so the row ends flush.
                int w = i == count - 1 ? width - x : cardWidth;
                statCards[i].SetBounds(x, 0, w, StatsHeight);
                x += w + gap;
            }
        }

        // --------------------------------------------------------------- rendering

        private void RenderStats(List<StatItem> stats)
        {
            statsRow.Controls.Clear();
            statCards.Clear();

            foreach (StatItem stat in stats)
            {
                var card = new StatCard
                {
                    Label = stat.Label,
                    Value = stat.Value,
                    IsAccent = stat.IsAccent
                };
                statCards.Add(card);
                statsRow.Controls.Add(card);
            }
        }

        private void RenderApprovals(List<ApprovalItem> approvals)
        {
            var stack = new List<Control> { BuildCardHeader("Pending your review") };

            for (int i = 0; i < approvals.Count; i++)
            {
                stack.Add(BuildApprovalRow(approvals[i], i < approvals.Count - 1));
            }

            if (approvals.Count == 0)
            {
                stack.Add(BuildEmptyRow("Nothing is waiting on you right now."));
            }

            FillCard(approvalsCard, stack, "Open the requests queue");
        }

        private void RenderActivity(List<ActivityItem> activity)
        {
            var stack = new List<Control> { BuildCardHeader("Recent activity") };

            for (int i = 0; i < activity.Count; i++)
            {
                stack.Add(BuildActivityRow(activity[i], i < activity.Count - 1));
            }

            if (activity.Count == 0)
            {
                stack.Add(BuildEmptyRow("No activity in the last seven days."));
            }

            FillCard(activityCard, stack, null);
        }

        /// <summary>
        /// Docked children stack in reverse z-order, so the list is added back to front
        /// to end up reading top to bottom.
        /// </summary>
        private static void FillCard(CardPanel card, List<Control> stack, string footerText)
        {
            card.SuspendLayout();
            card.Controls.Clear();

            if (footerText != null)
            {
                var footer = new RpvButton
                {
                    Text = footerText,
                    Variant = RpvButtonVariant.Tertiary,
                    Dock = DockStyle.Bottom,
                    Height = 30
                };
                card.Controls.Add(footer);
            }

            for (int i = stack.Count - 1; i >= 0; i--)
            {
                stack[i].Dock = DockStyle.Top;
                card.Controls.Add(stack[i]);
            }

            card.ResumeLayout();
        }

        private static Label BuildCardHeader(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontH3,
                ForeColor = RpvTheme.Midnight,
                Height = 34,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label BuildEmptyRow(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontBody,
                ForeColor = RpvTheme.Stone,
                Height = RowHeight,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Panel BuildApprovalRow(ApprovalItem item, bool withDivider)
        {
            var row = NewRow(withDivider);

            Label title = RowTitle(item.Title);
            Label meta = RowMeta(item.Requester + " · " + item.Meta);
            var pill = new StatusPill { Status = item.Status, Top = 18 };

            row.Controls.Add(title);
            row.Controls.Add(meta);
            row.Controls.Add(pill);

            row.Resize += (s, e) =>
            {
                pill.Left = Math.Max(0, row.ClientSize.Width - pill.Width);
                int textWidth = Math.Max(40, pill.Left - RpvTheme.Space3);
                title.Width = textWidth;
                meta.Width = textWidth;
            };

            return row;
        }

        private static Panel BuildActivityRow(ActivityItem item, bool withDivider)
        {
            var row = NewRow(withDivider);

            Label title = RowTitle(item.Title);
            Label meta = RowMeta(item.Meta);

            var time = new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Height = 20,
                Top = 6,
                Width = 90,
                TextAlign = ContentAlignment.MiddleRight,
                Text = item.TimeAgo
            };

            row.Controls.Add(title);
            row.Controls.Add(meta);
            row.Controls.Add(time);

            row.Resize += (s, e) =>
            {
                time.Left = Math.Max(0, row.ClientSize.Width - time.Width);
                int textWidth = Math.Max(40, time.Left - RpvTheme.Space3);
                title.Width = textWidth;
                meta.Width = textWidth;
            };

            return row;
        }

        private static Panel NewRow(bool withDivider)
        {
            var row = new Panel
            {
                Height = RowHeight,
                BackColor = RpvTheme.White
            };

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

            return row;
        }

        private static Label RowTitle(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontBodyMedium,
                ForeColor = RpvTheme.Charcoal,
                Height = 20,
                Top = 8,
                Left = 0,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label RowMeta(string text)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = RpvTheme.White,
                Font = RpvTheme.FontCaption,
                ForeColor = RpvTheme.Stone,
                Height = 18,
                Top = 30,
                Left = 0,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }
    }
}
