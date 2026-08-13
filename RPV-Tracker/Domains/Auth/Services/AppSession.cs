using RPV_Tracker.Domains.Auth.Models;

namespace RPV_Tracker.Domains.Auth.Services
{
    /// <summary>
    /// The signed-in session for the life of the process — the desktop equivalent of the
    /// web client's localStorage token/user pair.
    /// </summary>
    /// <remarks>
    /// Deliberately in-memory only. Persisting the token to disk would need OS-level
    /// protection (DPAPI) and a considered expiry policy; until "remember me" is a real
    /// requirement, signing in each launch is the safer default.
    /// </remarks>
    internal static class AppSession
    {
        public static string Token { get; private set; }

        public static AuthenticatedUser User { get; private set; }

        public static bool IsAuthenticated
        {
            get { return !string.IsNullOrEmpty(Token); }
        }

        /// <summary>
        /// The signed-in person's s_bpartner_employee_id, or null when the login response
        /// carried no employee record. Endpoints that take an employee id explicitly read it
        /// from here rather than from <see cref="AuthenticatedUser.Id"/>, which is the account id.
        /// </summary>
        public static string EmployeeId
        {
            get { return User != null ? User.EmployeeId : null; }
        }

        public static void Start(LoginResult result)
        {
            Token = result.Token;
            User = result.User;
        }

        public static void Clear()
        {
            Token = null;
            User = null;
        }
    }
}
