using System;
using System.Windows.Forms;
using RPV_Tracker.Branding;
using RPV_Tracker.Controls;
using RPV_Tracker.Domains.Auth.Services;
using RPV_Tracker.Forms;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ScrollWheelRouter.Install();

            LogStartup();

            // Applied once, up front, so the very first frame — the login screen — already
            // matches the user's saved preference instead of flashing light before switching.
            RpvTheme.ApplyMode(AppSettings.DarkMode);

            // Login is modal and runs before the message loop starts, so closing the shell
            // after a sign-out returns here and shows the login screen again rather than
            // leaving a headless process running.
            while (true)
            {
                using (var login = new LoginForm())
                {
                    if (login.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }
                }

                bool signOut;
                using (var shell = new MainForm())
                {
                    Application.Run(shell);
                    signOut = shell.SignOutRequested;
                }

                if (!signOut)
                {
                    return;
                }

                AppSession.Clear();
            }
        }

        /// <summary>
        /// Opens every run's log with the configuration that decides where requests go. Which
        /// backend the build is pointed at — and whether demo mode is quietly on — explains
        /// most "it works on my machine" reports before any request is even traced.
        /// </summary>
        private static void LogStartup()
        {
            DebugLog.Write("startup", "RPV Tracker starting. Log file: " + DebugLog.FilePath);
            DebugLog.Write("startup", "Rpv.ApiBaseUrl      = " + RpvConfig.ApiBaseUrl);
            DebugLog.Write("startup", "Rpv.DemoMode        = " + RpvConfig.DemoMode);
            DebugLog.Write("startup", "Rpv.Habit.ApiBaseUrl= " + RpvConfig.HabitApiBaseUrl);
            DebugLog.Write("startup", "Rpv.Habit.Token     = " + DebugLog.Fingerprint(RpvConfig.HabitToken));
            DebugLog.Write("startup", "Tracker upload      = " + RpvConfig.TrackerUploadEnabled);
            DebugLog.Write("startup", "Screenshot root     = " + RpvConfig.ScreenshotRoot);
            DebugLog.Write("startup", "Work schedule       = " + AppSettings.WorkScheduleStart
                + " – " + AppSettings.WorkScheduleEnd + " (local setting)");
        }
    }
}
