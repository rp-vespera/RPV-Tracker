using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// The application shell: brand nav bar over a themed content area. Pages are swapped
    /// into the content area rather than opened as separate windows.
    /// </summary>
    public partial class MainForm : Form
    {
        private const string DashboardKey = "dashboard";

        // Section title per nav key. Adding a module means adding a line here and, when
        // it has a real screen, a case in CreatePage.
        private const string TrackerKey = "tracker";
        private const string HistoryKey = "history";
        private const string AttendanceKey = "attendance";
        private const string ReportKey = "report";
        private const string SettingsKey = "settings";

        private static readonly KeyValuePair<string, string>[] Sections =
        {
            new KeyValuePair<string, string>(DashboardKey, "Dashboard"),
            new KeyValuePair<string, string>(TrackerKey, "Time tracker"),
            new KeyValuePair<string, string>(HistoryKey, "Task history"),
            new KeyValuePair<string, string>(AttendanceKey, "Attendance"),
            new KeyValuePair<string, string>(ReportKey, "Report")
        };

        private readonly Dictionary<string, UserControl> pages = new Dictionary<string, UserControl>();
        private readonly List<NavLink> navLinks = new List<NavLink>();

        // Lives in the account cluster rather than the section tabs — it's a personal
        // preference screen, not a workspace section.
        private readonly NavLink settingsLink;

        // Owned here, not by the tracker page, so tracking keeps running when the user
        // navigates to other sections and the nav bar can show a live recording indicator.
        private readonly TimeTrackingService tracking = new TimeTrackingService();

        // Lets the shell tell a user-initiated close (the ✕ button / Alt+F4) — which should
        // just hide to tray and keep tracking running — apart from a real exit (tray "Exit",
        // signing out, or Windows shutting down), which should stop tracking and let the
        // process end.
        private NotifyIcon trayIcon;
        private bool exitRequested;
        private bool trayHintShown;

        private string activeKey;

        /// <summary>When the profile card last closed, so a click that dismissed it can't reopen it.</summary>
        private DateTime profileClosedAt = DateTime.MinValue;

        public MainForm()
        {
            InitializeComponent();

            AuthenticatedUser user = AppSession.User;
            userNameLabel.Text = user != null ? user.FullName : string.Empty;
            userMonogram.Initials = user != null ? user.Initials : "RPV";

            userNameLabel.Cursor = Cursors.Hand;
            userMonogram.Cursor = Cursors.Hand;
            userNameLabel.Click += profile_Click;
            userMonogram.Click += profile_Click;

            tracking.StateChanged += tracking_StateChanged;
            tracking.IntervalCompleted += tracking_IntervalCompleted;
            tracking.SessionStarted += tracking_SessionStarted;
            tracking.SessionEnded += tracking_SessionEnded;
            tracking.IdleResumeSuggested += tracking_IdleResumeSuggested;

            SetupTrayIcon();

            settingsLink = new NavLink { Text = "Settings", PageKey = SettingsKey };
            settingsLink.Click += (s, e) => Navigate(SettingsKey);
            navBar.Controls.Add(settingsLink);

            RpvTheme.ThemeChanged += RpvTheme_ThemeChanged;

            BuildNavLinks();
            LayoutChrome();
            navBar.Resize += (s, e) => LayoutChrome();

            Navigate(DashboardKey);
        }

        private void tracking_StateChanged(object sender, EventArgs e)
        {
            trackingIndicator.Visible = tracking.IsTracking;
        }

        // Upload each interval's screenshot + activity metrics to the employee-scoped
        // /v1/tracker-sessions API (Sanctum-authenticated). Best-effort: a failed upload
        // is logged but never interrupts tracking.
        private async void tracking_IntervalCompleted(object sender, ActivityInterval interval)
        {
            // Recorded before the upload gate below, and regardless of it: the Report page
            // reads this log, and a day's shape shouldn't disappear because uploads are off
            // or a capture failed.
            IntervalHistoryStore.Append(interval, tracking.SessionId, tracking.ActiveTaskId,
                tracking.ActiveTaskTitle, tracking.IsOvertimeSession);

            if (!RpvConfig.TrackerUploadEnabled || !tracking.ActiveTaskId.HasValue
                || string.IsNullOrEmpty(interval.ScreenshotPath))
            {
                return;
            }

            try
            {
                await TrackerSessionsService.UploadAsync(
                    interval.ScreenshotPath, tracking.SessionId, interval.EndedAt, interval.KeyCount, interval.ClickCount, interval.ActivityPercent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Tracker session upload failed: " + ex.Message);
            }
        }

        // Pushes the session-level "in progress" record the moment tracking starts, so the
        // boss/lead audit list shows a live session rather than only learning about it once
        // it has already ended. Best-effort, same as the interval upload above.
        private async void tracking_SessionStarted(object sender, TrackingSessionSummary summary)
        {
            if (!RpvConfig.TrackerUploadEnabled)
            {
                return;
            }

            try
            {
                await TrackerSessionsService.UploadSummaryAsync(summary, "active");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Tracker session-summary start upload failed: " + ex.Message);
            }
        }

        // On session end, log it to the local Task history store. There is no server-side
        // end-of-session summary endpoint (the backend's tracker-sessions table is one row
        // per screenshot, not a session aggregate), so this stays local-only.
        private async void tracking_SessionEnded(object sender, TrackingSessionSummary summary)
        {
            TaskHistoryStore.Append(summary);

            if (RpvConfig.TrackerUploadEnabled)
            {
                try
                {
                    await TrackerSessionsService.UploadSummaryAsync(
                        summary, summary.StoppedByIdle ? "idle_stopped" : "completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Tracker session-summary stop upload failed: " + ex.Message);
                }
            }

            if (summary.StoppedByIdle && trayIcon != null)
            {
                trayIcon.BalloonTipTitle = "Tracking stopped";
                trayIcon.BalloonTipText = "No keyboard or mouse activity was detected for 5 minutes, so tracking was stopped automatically.";
                trayIcon.ShowBalloonTip(5000);
            }
        }

        // Fires the moment the operator produces the first key/click after an idle auto-stop.
        // Brings the window back into view (if it was hidden to tray) so the prompt has
        // context, then offers to resume the same task rather than leaving the stopped
        // session to go unnoticed until someone happens to check the tracker page.
        private void tracking_IdleResumeSuggested(object sender, TrackingSessionSummary summary)
        {
            if (!Visible)
            {
                ShowFromTray();
            }

            string task = summary.TaskId.HasValue
                ? "#" + summary.TaskId + " · " + summary.TaskTitle
                : "the same task";

            DialogResult resume = MessageBox.Show(this,
                "Tracking stopped at " + summary.EndedAt.ToString("h:mm tt") + " after 5 minutes of inactivity."
                    + "\n\nResume tracking on " + task + "?",
                "Resume tracking?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (resume == DialogResult.Yes)
            {
                tracking.Start(summary.TaskId, summary.TaskTitle, summary.IsOvertime);
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

        private void profile_Click(object sender, EventArgs e)
        {
            AuthenticatedUser user = AppSession.User;
            if (user == null)
            {
                return;
            }

            // Clicking the name while the card is already open deactivates it — which closes
            // it — and only then delivers this click. Without the guard the card would blink
            // shut and immediately reopen, so a second click could never dismiss it.
            if ((DateTime.Now - profileClosedAt).TotalMilliseconds < 250)
            {
                return;
            }

            var popup = new ProfilePopup(user);
            popup.FormClosed += (s, args) => profileClosedAt = DateTime.Now;
            popup.ShowBelow(this, userMonogram);
        }

        private void navLink_Click(object sender, EventArgs e)
        {
            var link = (NavLink)sender;
            Navigate(link.PageKey);
        }

        /// <summary>
        /// Right-aligns the account cluster (recording indicator, settings, avatar, name,
        /// sign out) against the nav bar's actual width, then gives the section tabs exactly
        /// whatever room is left. Computed at runtime — rather than trusting hardcoded
        /// Designer coordinates — so a resize, a longer name, or a future tab can never
        /// silently overlap it; the tabs just get however much space remains.
        /// </summary>
        private void LayoutChrome()
        {
            const int rightMargin = 24;
            const int gap = 12;

            int x = navBar.ClientSize.Width - rightMargin;

            x -= signOutLink.Width;
            signOutLink.Left = x;

            x -= 16;
            x -= userNameLabel.Width;
            userNameLabel.Left = x;

            x -= 8;
            x -= userMonogram.Width;
            userMonogram.Left = x;

            x -= gap;
            x -= settingsLink.Width;
            settingsLink.Top = 0;
            settingsLink.Height = RpvTheme.NavHeight;
            settingsLink.Left = x;

            x -= gap;
            x -= trackingIndicator.Width;
            trackingIndicator.Left = x;

            navLinksHost.Width = Math.Max(0, x - gap - navLinksHost.Left);
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
            settingsLink.IsActive = key == SettingsKey;
        }

        private UserControl CreatePage(string key)
        {
            if (key == DashboardKey)
            {
                var dashboard = new DashboardPage();
                dashboard.OpenTrackerRequested += (s, e) => Navigate(TrackerKey);
                return dashboard;
            }

            if (key == TrackerKey)
            {
                return new TimeTrackerPage(tracking);
            }

            if (key == HistoryKey)
            {
                return new TaskHistoryPage(tracking);
            }

            if (key == AttendanceKey)
            {
                return new AttendancePage();
            }

            if (key == ReportKey)
            {
                return new ReportPage(tracking);
            }

            if (key == SettingsKey)
            {
                return new SettingsPage(tracking);
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

        private void RpvTheme_ThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            // Defer to the next message-loop tick: this can fire from inside a click handler
            // on a control that's about to be disposed by the rebuild below (the Settings
            // page's own theme switch is exactly that), and tearing down a control tree in
            // the middle of handling its own click is asking for trouble.
            BeginInvoke((MethodInvoker)(() =>
            {
                if (IsDisposed || Disposing)
                {
                    return;
                }

                BackColor = RpvTheme.Cream;
                contentPanel.BackColor = RpvTheme.Cream;
                RebuildPages();
            }));
        }

        /// <summary>
        /// Pages bake their colours in at construction time, so a theme switch can't just
        /// repaint them — every cached page is dropped and the active one rebuilt fresh.
        /// </summary>
        private void RebuildPages()
        {
            string key = activeKey;

            foreach (UserControl page in pages.Values)
            {
                page.Dispose();
            }
            pages.Clear();

            activeKey = null;
            if (key != null)
            {
                Navigate(key);
            }
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
            exitRequested = true;
            await AuthService.LogoutAsync(AppSession.Token);
            AppSession.Clear();
            Close();
        }

        /// <summary>
        /// Installs the tray icon that lets the shell keep running (and tracking) after the
        /// operator dismisses the window, with an explicit "Exit" the only way to actually
        /// quit and stop the timer.
        /// </summary>
        private void SetupTrayIcon()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open RPV Workforce", null, (s, e) => ShowFromTray());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, trayExit_Click);

            trayIcon = new NotifyIcon
            {
                Icon = Icon ?? SystemIcons.Application,
                Text = "RPV Workforce",
                Visible = true,
                ContextMenuStrip = menu
            };
            trayIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        private void trayExit_Click(object sender, EventArgs e)
        {
            exitRequested = true;
            Close();
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        /// <summary>
        /// Closing the window (the ✕ button / Alt+F4) hides to tray instead of exiting, so
        /// tracking — and the screenshot/activity capture behind it — keeps running in the
        /// background. Minimizing already leaves the window merely hidden from view without
        /// touching tracking, so this only needs to intercept an actual close request; a real
        /// exit (tray "Exit", signing out, or Windows shutting down) still closes normally.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();

                if (!trayHintShown)
                {
                    trayHintShown = true;
                    trayIcon.BalloonTipTitle = "RPV Workforce is still running";
                    trayIcon.BalloonTipText = "Tracking continues in the background. Right-click this icon and choose Exit to close it fully.";
                    trayIcon.ShowBalloonTip(4000);
                }
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            RpvTheme.ThemeChanged -= RpvTheme_ThemeChanged;

            // Stop tracking and remove the global input hooks before the window goes away.
            // Stop() first so its SessionEnded fires while the upload handler is still attached
            // (fires the final screenshot upload + session post — best-effort on shutdown).
            if (tracking.IsTracking)
            {
                tracking.Stop();
            }
            tracking.StateChanged -= tracking_StateChanged;
            tracking.IntervalCompleted -= tracking_IntervalCompleted;
            tracking.SessionStarted -= tracking_SessionStarted;
            tracking.SessionEnded -= tracking_SessionEnded;
            tracking.IdleResumeSuggested -= tracking_IdleResumeSuggested;
            tracking.Dispose();

            trayIcon.Visible = false;
            trayIcon.Dispose();

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
