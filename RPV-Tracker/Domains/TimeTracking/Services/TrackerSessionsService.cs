using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPV_Tracker.Domains.Auth.Services;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Domains.TimeTracking.Services
{
    /// <summary>
    /// Uploads one interval's screenshot + activity metrics to POST /v1/tracker-sessions.
    /// Authenticated as the signed-in employee (Sanctum bearer token) — the server resolves
    /// s_bpartner_employee_id from the token, never from a client-supplied value.
    /// </summary>
    internal static class TrackerSessionsService
    {
        /// <summary>POST /v1/tracker-sessions. Returns the new row's id, or null if the response didn't carry one.</summary>
        public static async Task<int?> UploadAsync(
            string screenshotPath, DateTime sessionAt, long keyboardTaps, long mouseTaps, int activityPercent)
        {
            string token = AppSession.Token;
            if (string.IsNullOrEmpty(token))
            {
                throw new ApiException("You're not signed in.");
            }

            var fields = new Dictionary<string, string>
            {
                { "session_at", sessionAt.ToString("o") },
                { "keyboard_taps", keyboardTaps.ToString() },
                { "mouse_taps", mouseTaps.ToString() },
                { "activity_percent", activityPercent.ToString() }
            };

            Dictionary<string, object> response = await ApiClient
                .PostFileAsync("v1/tracker-sessions", "image", screenshotPath, "image/jpeg", fields, token)
                .ConfigureAwait(false);

            Dictionary<string, object> data = ApiClient.ReadMap(response, "data");
            string id = data != null ? ApiClient.ReadString(data, "wbs_t_tracker_session_id") : null;

            int parsed;
            return id != null && int.TryParse(id, out parsed) ? (int?)parsed : null;
        }
    }
}
