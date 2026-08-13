using System.Collections.Generic;
using System.Threading.Tasks;
using RPV_Tracker.Domains.Auth.Services;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Domains.TimeTracking.Services
{
    /// <summary>Server-side tracker settings for the signed-in employee (GET/PUT /v1/tracker-settings).</summary>
    internal class TrackerSettingsDto
    {
        public string Theme { get; set; }
        public int ScreenshotIntervalSeconds { get; set; }
        public string MorningStart { get; set; }
        public string MorningEnd { get; set; }
        public string AfternoonStart { get; set; }
        public string AfternoonEnd { get; set; }
        public string ScreenCaptureMode { get; set; }
    }

    /// <summary>
    /// Per-employee tracker settings that live on the server rather than purely on this
    /// machine — appearance theme, screenshot cadence, the morning/afternoon window the
    /// tracker should be capturing during (also what HrTapsController::attendanceCheck uses
    /// to decide OT eligibility), and single/dual monitor capture. Upsert-on-read: GET always
    /// returns a row, defaulted for a first-time employee.
    /// </summary>
    internal static class TrackerSettingsService
    {
        public static async Task<TrackerSettingsDto> GetAsync()
        {
            Dictionary<string, object> response = await ApiClient.GetAsync("v1/tracker-settings", RequireToken()).ConfigureAwait(false);
            return Map(ApiClient.ReadMap(response, "data"));
        }

        /// <summary>
        /// PUT /v1/tracker-settings with only the given fields — e.g. { "theme", "dark" }.
        /// Omitting a field (rather than sending it as null) is what tells the server to leave
        /// it as-is, so callers should only include the field(s) they're actually changing.
        /// </summary>
        public static async Task<TrackerSettingsDto> UpdateAsync(Dictionary<string, object> changedFields)
        {
            Dictionary<string, object> response = await ApiClient
                .PutAsync("v1/tracker-settings", changedFields, RequireToken())
                .ConfigureAwait(false);
            return Map(ApiClient.ReadMap(response, "data"));
        }

        private static string RequireToken()
        {
            string token = AppSession.Token;
            if (string.IsNullOrEmpty(token))
            {
                throw new ApiException("You're not signed in.");
            }
            return token;
        }

        private static TrackerSettingsDto Map(Dictionary<string, object> data)
        {
            if (data == null)
            {
                return new TrackerSettingsDto();
            }

            return new TrackerSettingsDto
            {
                Theme = ApiClient.ReadString(data, "theme"),
                ScreenshotIntervalSeconds = ReadInt(data, "screenshot_interval_seconds", 300),
                MorningStart = ApiClient.ReadString(data, "morning_start"),
                MorningEnd = ApiClient.ReadString(data, "morning_end"),
                AfternoonStart = ApiClient.ReadString(data, "afternoon_start"),
                AfternoonEnd = ApiClient.ReadString(data, "afternoon_end"),
                ScreenCaptureMode = ApiClient.ReadString(data, "screen_capture_mode")
            };
        }

        private static int ReadInt(Dictionary<string, object> map, string key, int fallback)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value) && value != null)
            {
                int parsed;
                if (int.TryParse(value.ToString(), out parsed))
                {
                    return parsed;
                }
            }
            return fallback;
        }
    }
}
