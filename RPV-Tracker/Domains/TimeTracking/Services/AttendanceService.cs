using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RPV_Tracker.Domains.Auth.Services;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Domains.TimeTracking.Services
{
    /// <summary>Result of GET /v1/hr/taps-sync/attendance-check for one date.</summary>
    internal class AttendanceCheckResult
    {
        public bool HasAttendance { get; set; }
        public bool WorkedMorning { get; set; }
        public bool WorkedAfternoon { get; set; }

        /// <summary>True when the signed-in employee actually worked the morning and/or
        /// afternoon half of the date, per TAPS — the gate for offering an OT request.</summary>
        public bool CanRequestOvertime { get; set; }
    }

    /// <summary>
    /// Whether the signed-in employee has TAPS attendance on file for a given date, and
    /// whether that attendance covers enough of the day to request overtime for it.
    /// </summary>
    /// <remarks>
    /// Unlike POST /v1/tracker-sessions — which resolves the employee server-side from the
    /// Sanctum token — this endpoint validates s_bpartner_employee_id as a required request
    /// field and answers 422 without it. So the id is sent explicitly, taken from the login
    /// response (<see cref="AppSession.EmployeeId"/>) rather than from the account id.
    ///
    /// The endpoint no longer returns worked_morning/worked_afternoon/can_request_ot flags
    /// directly (an earlier version of this service read those keys straight off the
    /// response, which silently produced "false" for all three once the API moved to the
    /// current shape). What it returns instead is has_attendance/has_tapped/is_matched plus
    /// a detail.matched_logs (and detail.raw_logs) array of actual clock-in timestamps, so
    /// this service now buckets those timestamps into the employee's morning/afternoon
    /// window itself (from GET /v1/tracker-settings) to reconstruct the same three flags.
    /// This is a best-effort client-side approximation, not the official HR/payroll
    /// determination — if TAPS and this ever disagree on OT eligibility, TAPS wins.
    /// </remarks>
    internal static class AttendanceService
    {
        // Used only when /v1/tracker-settings has no morning/afternoon window configured for
        // the employee (or the call fails) — a fixed noon split so the app still shows
        // something sensible rather than nothing.
        private static readonly TimeSpan FallbackMorningStart = TimeSpan.FromHours(0);
        private static readonly TimeSpan FallbackMorningEnd = TimeSpan.FromHours(12);
        private static readonly TimeSpan FallbackAfternoonStart = TimeSpan.FromHours(12);
        private static readonly TimeSpan FallbackAfternoonEnd = TimeSpan.FromHours(24);

        public static async Task<AttendanceCheckResult> CheckAsync(DateTime date)
        {
            string token = AppSession.Token;
            if (string.IsNullOrEmpty(token))
            {
                DebugLog.Write("attendance", "Skipped — no session token. (Demo mode issues 'demo-token', "
                    + "which the real API will reject.)");
                throw new ApiException("You're not signed in.");
            }

            string employeeId = AppSession.EmployeeId;
            if (string.IsNullOrEmpty(employeeId))
            {
                DebugLog.Write("attendance", "No s_bpartner_employee_id on the session — the login "
                    + "response carried no employee record, so attendance can't be checked.");
                throw new ApiException("Your account isn't linked to an employee record, so today's "
                    + "attendance can't be checked. Contact your HR administrator.");
            }

            string path = "v1/hr/taps-sync/attendance-check?date=" + date.ToString("yyyy-MM-dd")
                + "&s_bpartner_employee_id=" + Uri.EscapeDataString(employeeId);
            Dictionary<string, object> response = await ApiClient.GetAsync(path, token).ConfigureAwait(false);

            bool hasAttendance = ApiClient.ReadBool(response, "has_attendance");
            List<DateTime> tapTimes = CollectTapTimes(response);

            TimeSpan? morningStart = null, morningEnd = null, afternoonStart = null, afternoonEnd = null;
            try
            {
                TrackerSettingsDto settings = await TrackerSettingsService.GetAsync().ConfigureAwait(false);
                morningStart = ParseTimeOfDay(settings.MorningStart);
                morningEnd = ParseTimeOfDay(settings.MorningEnd);
                afternoonStart = ParseTimeOfDay(settings.AfternoonStart);
                afternoonEnd = ParseTimeOfDay(settings.AfternoonEnd);
            }
            catch (Exception ex)
            {
                DebugLog.Write("attendance", "Couldn't load the morning/afternoon window from "
                    + "tracker-settings — falling back to a fixed noon split. " + ex.Message);
            }

            bool workedMorning = tapTimes.Any(t => FallsWithin(t.TimeOfDay,
                morningStart ?? FallbackMorningStart, morningEnd ?? FallbackMorningEnd));
            bool workedAfternoon = tapTimes.Any(t => FallsWithin(t.TimeOfDay,
                afternoonStart ?? FallbackAfternoonStart, afternoonEnd ?? FallbackAfternoonEnd));

            var result = new AttendanceCheckResult
            {
                HasAttendance = hasAttendance,
                WorkedMorning = workedMorning,
                WorkedAfternoon = workedAfternoon,
                CanRequestOvertime = hasAttendance && (workedMorning || workedAfternoon)
            };

            DebugLog.Write("attendance", "Checked " + date.ToString("yyyy-MM-dd")
                + " for employee " + employeeId
                + " → has_attendance=" + result.HasAttendance
                + ", tap_times=[" + string.Join(", ", tapTimes.Select(t => t.ToString("HH:mm:ss"))) + "]"
                + ", worked_morning=" + result.WorkedMorning
                + ", worked_afternoon=" + result.WorkedAfternoon
                + ", can_request_ot=" + result.CanRequestOvertime);

            return result;
        }

        /// <summary>Clock-in timestamps for the day, preferring the matched log's actual_time_in
        /// (the reconciled figure TAPS itself trusts) and falling back to raw "IN" taps if there
        /// are no matched logs yet (e.g. still pending reconciliation).</summary>
        private static List<DateTime> CollectTapTimes(Dictionary<string, object> response)
        {
            var times = new List<DateTime>();

            Dictionary<string, object> detail = ApiClient.ReadMap(response, "detail");
            if (detail == null)
            {
                return times;
            }

            foreach (Dictionary<string, object> log in ApiClient.ReadList(detail, "matched_logs"))
            {
                DateTime parsed;
                string raw = ApiClient.ReadString(log, "actual_time_in");
                if (raw != null && DateTime.TryParse(raw, out parsed))
                {
                    times.Add(parsed);
                }
            }

            if (times.Count == 0)
            {
                foreach (Dictionary<string, object> log in ApiClient.ReadList(detail, "raw_logs"))
                {
                    string logType = ApiClient.ReadString(log, "log_type");
                    if (!string.Equals(logType, "IN", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    DateTime parsed;
                    string raw = ApiClient.ReadString(log, "time_logged");
                    if (raw != null && DateTime.TryParse(raw, out parsed))
                    {
                        times.Add(parsed);
                    }
                }
            }

            return times;
        }

        private static TimeSpan? ParseTimeOfDay(string text)
        {
            TimeSpan parsed;
            if (!string.IsNullOrWhiteSpace(text) && TimeSpan.TryParse(text, out parsed))
            {
                return parsed;
            }
            return null;
        }

        private static bool FallsWithin(TimeSpan time, TimeSpan start, TimeSpan end)
        {
            return time >= start && time < end;
        }
    }
}
