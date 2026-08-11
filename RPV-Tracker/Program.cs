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
    }
}
