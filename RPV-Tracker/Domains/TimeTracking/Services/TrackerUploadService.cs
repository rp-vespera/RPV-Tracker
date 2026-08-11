using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPV_Tracker.Domains.TimeTracking.Models;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Domains.TimeTracking.Services
{
    /// <summary>
    /// Sends tracker screenshots and the end-of-session record to the RPV backend
    /// (which stores images in Cloudflare R2), keyed by the task id. All calls are
    /// best-effort — callers swallow failures so a lost upload never disrupts tracking.
    /// </summary>
    internal static class TrackerUploadService
    {
        /// <summary>Minimal response shape — we only care that the call succeeded.</summary>
        private class UploadResponse
        {
            public bool success { get; set; }
        }

        /// <summary>POST tracker/screenshot — upload one interval capture.</summary>
        public static async Task UploadScreenshotAsync(int taskId, string sessionId, string filePath, DateTime capturedAt)
        {
            var fields = new Dictionary<string, string>
            {
                { "reminder_id", taskId.ToString() },
                { "session", sessionId ?? string.Empty },
                { "captured_at", capturedAt.ToString("o") }
            };

            await TrackerApiClient
                .PostFileAsync<UploadResponse>("tracker/screenshot", "image", filePath, "image/jpeg", fields)
                .ConfigureAwait(false);
        }

        /// <summary>POST tracker/session — record the session summary when tracking stops.</summary>
        public static async Task EndSessionAsync(TrackingSessionSummary summary)
        {
            var payload = new
            {
                reminder_id = summary.TaskId,
                session = summary.SessionId,
                task_title = summary.TaskTitle,
                started_at = summary.StartedAt.ToString("o"),
                ended_at = summary.EndedAt.ToString("o"),
                screenshot_count = summary.ScreenshotCount,
                keystrokes = summary.TotalKeys,
                clicks = summary.TotalClicks,
                active_percent = summary.ActivePercent,
                is_overtime = summary.IsOvertime
            };

            await TrackerApiClient
                .PostJsonAsync<UploadResponse>("tracker/session", payload)
                .ConfigureAwait(false);
        }
    }
}
