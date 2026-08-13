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
    /// whether that attendance covers enough of the day to request overtime for it.
    /// </summary>
    /// <remarks>
    /// Unlike POST /v1/tracker-sessions — which resolves the employee server-side from the
    /// Sanctum token — this endpoint validates s_bpartner_employee_id as a required request
    /// field and answers 422 without it. So the id is sent explicitly, taken from the login
    /// response (<see cref="AppSession.EmployeeId"/>) rather than from the account id.
    /// </remarks>
    internal static class AttendanceService
    {
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

            var result = new AttendanceCheckResult
            {
                HasAttendance = ApiClient.ReadBool(response, "has_attendance"),
                WorkedMorning = ApiClient.ReadBool(response, "worked_morning"),
                WorkedAfternoon = ApiClient.ReadBool(response, "worked_afternoon"),
                CanRequestOvertime = ApiClient.ReadBool(response, "can_request_ot")
            };

            DebugLog.Write("attendance", "Checked " + date.ToString("yyyy-MM-dd")
                + " for employee " + employeeId
                + " → has_attendance=" + result.HasAttendance
                + ", worked_morning=" + result.WorkedMorning
                + ", worked_afternoon=" + result.WorkedAfternoon
                + ", can_request_ot=" + result.CanRequestOvertime);

            // A flag we read as false while the raw body clearly said otherwise means the key
            // is named differently or nested — worth spelling out rather than inferring from
            // a wall of falses.
            if (!result.HasAttendance && !result.CanRequestOvertime)
            {
                DebugLog.Write("attendance", "All flags false. Response keys: " + DebugLog.DescribeShape(response));
            }

            return result;
        }
    }
}
