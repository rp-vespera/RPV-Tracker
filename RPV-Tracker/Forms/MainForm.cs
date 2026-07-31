using System;
using System.Collections.Generic;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;
using RPV_Tracker.Domains.Auth.Models;
using RPV_Tracker.Domains.Auth.Services;
using RPV_Tracker.Domains.TimeTracking.Models;
using RPV_Tracker.Domains.TimeTracking.Services;
using RPV_Tracker.Infrastructure;
using RPV_Tracker.Pages;

namespace RPV_Tracker.Forms
{
    /// <summary>
    /// The application shell: Midnight nav bar over a Cream content area. Pages are
    /// swapped into the content area rather than opened as separate windows.
    /// </summary>
    public partial class MainForm : Form
    {
        private const string DashboardKey = "dashboard";

        // Section title per nav key. Adding a module means adding a line here and, when
        // it has a real screen, a case in CreatePage.
        private const string TrackerKey = "tracker";

        private static readonly KeyValuePair<string, string>[] Sections =
        {
            new KeyValuePair<string, string>(DashboardKey, "Dashboard"),
            new KeyValuePair<string, string>(TrackerKey, "Time tracker"),
            new KeyValuePair<string, string>("employees", "Employees"),
            new KeyValuePair<string, string>("requests", "Requests"),
            new KeyValuePair<string, string>("reports", "Reports")
        };

        private readonly Dictionary<string, UserControl> pages = new Dictionary<string, UserControl>();
        private readonly List<NavLink> navLinks = new List<NavLink>();

        // Owned here, not by the tracker page, so tracking keeps running when the user
        // navigates to other sections and the nav bar can show a live recording indicator.
        private readonly TimeTrackingService tracking = new TimeTrackingService();

        private string activeKey;

        public MainForm()
        {
            InitializeComponent();

            AuthenticatedUser user = AppSession.User;
            userNameLabel.Text = user != null ? user.FullName : string.Empty;
            userMonogram.Initials = user != null ? user.Initials : "RPV";

            tracking.StateChanged += tracking_StateChanged;
            tracking.IntervalCompleted += tracking_IntervalCompleted;
            tracking.SessionEnded += tracking_SessionEnded;

            BuildNavLinks();
            Navigate(DashboardKey);
        }

        private void tracking_StateChanged(object sender, EventArgs e)
        {
            trackingIndicator.Visible = tracking.IsTracking;
        }

        // Upload each interval's screenshot to the backend (Cloudflare) as it completes.
        // Best-effort: a failed upload is logged but never interrupts tracking.
        private async void tracking_IntervalCompleted(object sender, ActivityInterval interval)
        {
            if (!RpvConfig.TrackerUploadEnabled || !tracking.ActiveTaskId.HasValue
                || string.IsNullOrEmpty(interval.ScreenshotPath))
            {
                return;
            }

            try
            {
                await TrackerUploadService.UploadScreenshotAsync(
                    tracking.ActiveTaskId.Value, tracking.SessionId, interval.ScreenshotPath, interval.EndedAt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Tracker screenshot upload failed: " + ex.Message);
            }
        }

        // On session end, post the summary "attachment" to the backend, keyed by task id.
        private async void tracking_SessionEnded(object sender, TrackingSessionSummary summary)
        {
            if (!RpvConfig.TrackerUploadEnabled || !summary.TaskId.HasValue)
            {
                return;
            }

            try
            {
                await TrackerUploadService.EndSessionAsync(summary);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Tracker session post failed: " + ex.Message);
            }
        }

        /// <summary>
        /// True when the window closed because the user signed out, which tells
        /// <see cref="Program"/> to show the login screen again instead of exiting.
        /// </summary>
        public bool SignOutRequested { get; private set; }

        private void BuildNavLinks()
        {
            foreach (KeyValuePair<string, string> section in Sections)
            {
                var link = new NavLink
                {
                    Text = section.Value,
                    PageKey = section.Key,
                    Margin = new Padding(0),
                    Height = RpvTheme.NavHeight
                };
                link.Click += navLink_Click;

                navLinks.Add(link);
                navLinksHost.Controls.Add(link);
            }
        }

        private void navLink_Click(object sender, EventArgs e)
        {
            var link = (NavLink)sender;
            Navigate(link.PageKey);
        }

        private void Navigate(string key)
        {
            if (key == activeKey)
            {
                return;
            }

            UserControl page;
            if (!pages.TryGetValue(key, out page))
            {
                page = CreatePage(key);
                pages[key] = page;
            }

            contentPanel.SuspendLayout();
            contentPanel.Controls.Clear();
            page.Dock = DockStyle.Fill;
            contentPanel.Controls.Add(page);
            contentPanel.ResumeLayout();

            activeKey = key;

            foreach (NavLink link in navLinks)
            {
                link.IsActive = link.PageKey == key;
            }
        }

        private UserControl CreatePage(string key)
        {
            if (key == DashboardKey)
            {
                return new DashboardPage();
            }

            if (key == TrackerKey)
            {
                return new TimeTrackerPage(tracking);
            }

            string title = key;
            foreach (KeyValuePair<string, string> section in Sections)
            {
                if (section.Key == key)
                {
                    title = section.Value;
                    break;
                }
            }

            var placeholder = new PlaceholderPage(title);
            placeholder.BackRequested += (s, e) => Navigate(DashboardKey);
            return placeholder;
        }

        private async void signOutLink_Click(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show(this,
                "Sign out of RPV Workforce?",
                "Sign out",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            SignOutRequested = true;
            await AuthService.LogoutAsync(AppSession.Token);
            AppSession.Clear();
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Stop tracking and remove the global input hooks before the window goes away.
            // Stop() first so its SessionEnded fires while the upload handler is still attached
            // (fires the final screenshot upload + session post — best-effort on shutdown).
            if (tracking.IsTracking)
            {
                tracking.Stop();
            }
            tracking.StateChanged -= tracking_StateChanged;
            tracking.IntervalCompleted -= tracking_IntervalCompleted;
            tracking.SessionEnded -= tracking_SessionEnded;
            tracking.Dispose();

            // Pages detached by Controls.Clear() are not covered by the form's Dispose.
            foreach (UserControl page in pages.Values)
            {
                page.Dispose();
            }
            pages.Clear();
            base.OnFormClosed(e);
        }
    }
}
