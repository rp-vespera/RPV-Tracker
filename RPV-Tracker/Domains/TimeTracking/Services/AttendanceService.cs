using System;
using System.Collections.Generic;
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
    /// whether that attendance covers enough of the day to request overtime for it. The
    /// employee is always resolved server-side from the Sanctum bearer token — there is no
    /// employee-id parameter to pass, by design (see HrTapsController::attendanceCheck).
    /// </summary>
    internal static class AttendanceService
    {
        public static async Task<AttendanceCheckResult> CheckAsync(DateTime date)
        {
            string token = AppSession.Token;
            if (string.IsNullOrEmpty(token))
            {
                throw new ApiException("You're not signed in.");
            }

            string path = "v1/hr/taps-sync/attendance-check?date=" + date.ToString("yyyy-MM-dd");
            Dictionary<string, object> response = await ApiClient.GetAsync(path, token).ConfigureAwait(false);

            return new AttendanceCheckResult
            {
                HasAttendance = ApiClient.ReadBool(response, "has_attendance"),
                WorkedMorning = ApiClient.ReadBool(response, "worked_morning"),
                WorkedAfternoon = ApiClient.ReadBool(response, "worked_afternoon"),
                CanRequestOvertime = ApiClient.ReadBool(response, "can_request_ot")
            };
        }
    }
}
