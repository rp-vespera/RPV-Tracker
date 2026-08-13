using System;
using System.Configuration;

namespace RPV_Tracker.Infrastructure
{
    /// <summary>
    /// Reads app settings from App.config. Values live there rather than in code so the
    /// same build can be pointed at local, staging, or production without recompiling.
    /// </summary>
    internal static class RpvConfig
    {
        /// <summary>
        /// Base URL of the RPV Workforce API — the same value the web client reads from
        /// NEXT_PUBLIC_API_URL. Defaults to the local Laravel dev server.
        /// </summary>
        public static string ApiBaseUrl
        {
            get
            {
                string value = ConfigurationManager.AppSettings["Rpv.ApiBaseUrl"];
                return string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:8000/api" : value.TrimEnd('/');
            }
        }

        /// <summary>
        /// When true the app runs entirely on local sample data and never calls the API.
        /// Lets the UI be reviewed without the backend running. Turn this off to sign in
        /// against the real API.
        /// </summary>
        public static bool DemoMode
        {
            get
            {
                bool parsed;
                return bool.TryParse(ConfigurationManager.AppSettings["Rpv.DemoMode"], out parsed) && parsed;
            }
        }

        public static int RequestTimeoutSeconds
        {
            get
            {
                int parsed;
                if (int.TryParse(ConfigurationManager.AppSettings["Rpv.RequestTimeoutSeconds"], out parsed) && parsed > 0)
                {
                    return parsed;
                }
                return 20;
            }
        }

        /// <summary>
        /// Explicit low-level override for the screenshot/activity interval, in seconds.
        /// Set only for local testing, where waiting out a real user-facing interval
        /// (5+ minutes) would be impractical. When absent, the interval instead comes from
        /// whatever the user picked on the Settings page (<see cref="AppSettings.ScreenshotIntervalMinutes"/>).
        /// </summary>
        public static int? TrackingIntervalSecondsOverride
        {
            get
            {
                int seconds;
                if (int.TryParse(ConfigurationManager.AppSettings["Rpv.Tracking.IntervalSeconds"], out seconds) && seconds > 0)
                {
                    return seconds;
                }
                return null;
            }
        }

        /// <summary>
        /// Base URL of the Pulse / Habit service that serves the signed-in person's tasks
        /// (shared/me) and performance (me/performance). This is a separate service from the
        /// RPV Workforce login backend and uses its own X-Habit-Token auth.
        /// </summary>
        public static string HabitApiBaseUrl
        {
            get
            {
                string value = ConfigurationManager.AppSettings["Rpv.Habit.ApiBaseUrl"];
                return string.IsNullOrWhiteSpace(value)
                    ? "https://37-59-111-221.sslip.io/api"
                    : value.TrimEnd('/');
            }
        }

        /// <summary>
        /// Per-user token for the Pulse / Habit service, sent as the X-Habit-Token header.
        /// This is a personal secret — it identifies the employee to the tasks/performance API.
        /// </summary>
        public static string HabitToken
        {
            get { return ConfigurationManager.AppSettings["Rpv.Habit.Token"] ?? string.Empty; }
        }

        /// <summary>
        /// When true, each interval screenshot + activity metrics is uploaded to
        /// POST /v1/tracker-sessions (Sanctum-authenticated, same host as <see cref="ApiBaseUrl"/>).
        /// Uploads are best-effort — failures never interrupt tracking. Turn off to keep
        /// screenshots purely local.
        /// </summary>
        public static bool TrackerUploadEnabled
        {
            get
            {
                bool parsed;
                // Default on, but only meaningful once a token is configured.
                if (bool.TryParse(ConfigurationManager.AppSettings["Rpv.Tracker.UploadEnabled"], out parsed))
                {
                    return parsed;
                }
                return true;
            }
        }

        /// <summary>
        /// Folder that holds captured screenshots, grouped into one subfolder per session.
        /// Kept under the user's local app data so captures never sync to roaming profiles.
        /// </summary>
        public static string ScreenshotRoot
        {
            get
            {
                string configured = ConfigurationManager.AppSettings["Rpv.Tracking.ScreenshotRoot"];
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return configured;
                }

                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RPV Workforce", "Screenshots");
            }
        }
    }
}
