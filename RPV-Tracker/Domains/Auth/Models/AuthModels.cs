using System;

namespace RPV_Tracker.Domains.Auth.Models
{
    /// <summary>Payload for POST /v1/login — matches the web client's login contract.</summary>
    internal class LoginRequest
    {
        public string username { get; set; }
        public string password { get; set; }
    }

    /// <summary>The signed-in employee, as returned alongside the auth token.</summary>
    internal class AuthenticatedUser
    {
        /// <summary>
        /// The login account's own id (<c>user.id</c>). Identifies the account, not the person
        /// in the HR system — keep it away from employee-scoped API calls.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// <c>s_bpartner_employee_id</c> — the HR employee record behind this account, and the
        /// value employee-scoped endpoints expect. Distinct from <see cref="Id"/>: for a real
        /// account these differ (account 1 ↔ employee 3723), so conflating them silently sends
        /// the wrong person's id. Null when the login response carried no employee record.
        /// </summary>
        public string EmployeeId { get; set; }

        /// <summary>
        /// <c>s_bpartner_id</c> — the business-partner record the employee hangs off. Distinct
        /// again from <see cref="EmployeeId"/> (3363 vs 3723 for a real account).
        /// </summary>
        public string BusinessPartnerId { get; set; }

        /// <summary>Human-facing payroll number, e.g. "C3793" — not an API key.</summary>
        public string EmployeeNo { get; set; }

        public string Username { get; set; }

        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }

        /// <summary>
        /// First name only. The brand voice calls for addressing people by first name
        /// ("Good morning, Sarah"), so this is what the dashboard greeting uses.
        /// </summary>
        public string FirstName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName))
                {
                    return "there";
                }
                return FullName.Trim().Split(' ')[0];
            }
        }

        /// <summary>Up to two initials, for the nav bar avatar monogram.</summary>
        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName))
                {
                    return "RPV";
                }

                string[] parts = FullName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    return parts[0].Substring(0, 1).ToUpperInvariant();
                }
                return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
            }
        }
    }

    /// <summary>Result of a sign-in attempt: the token plus the user it belongs to.</summary>
    internal class LoginResult
    {
        public string Token { get; set; }
        public AuthenticatedUser User { get; set; }
    }
}
