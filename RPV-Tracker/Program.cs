using System;
using System.Windows.Forms;
using RPV_Tracker.Domains.Auth.Services;
using RPV_Tracker.Forms;

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
