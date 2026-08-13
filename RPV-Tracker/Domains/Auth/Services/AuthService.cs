using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPV_Tracker.Domains.Auth.Models;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Domains.Auth.Services
{
    /// <summary>
    /// Sign-in against the RPV Workforce API. Endpoints mirror the web client's
    /// auth.service.ts so both clients stay on the same contract.
    /// </summary>
    internal static class AuthService
    {
        // Demo credentials, used only when Rpv.DemoMode is enabled in App.config.
        private const string DemoUsername = "demo";
        private const string DemoPassword = "demo";

        /// <summary>
        /// POST /v1/login. Throws <see cref="ApiException"/> with a user-safe message
        /// when the credentials are rejected or the server can't be reached.
        /// </summary>
        public static async Task<LoginResult> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ApiException("Enter your username to continue.");
            }
            if (string.IsNullOrEmpty(password))
            {
                throw new ApiException("Enter your password to continue.");
            }

            if (RpvConfig.DemoMode)
            {
                return await DemoLoginAsync(username, password).ConfigureAwait(false);
            }

            var payload = new LoginRequest { username = username.Trim(), password = password };
            Dictionary<string, object> response = await ApiClient.PostAsync("v1/login", payload).ConfigureAwait(false);

            string token = ApiClient.ReadString(response, "token", "access_token");
            if (string.IsNullOrEmpty(token))
            {
                DebugLog.Write("auth", "No token in the login response. Top level: " + DebugLog.DescribeShape(response));
                throw new ApiException("Sign-in succeeded but the server didn't return a token. Contact your HR administrator.");
            }

            Dictionary<string, object> user = ApiClient.ReadMap(response, "user");
            LogIdentity(response, user, token);

            return new LoginResult
            {
                Token = token,
                User = MapUser(user, username)
            };
        }

        /// <summary>
        /// Records what the login response actually carried about who signed in. The employee
        /// id matters beyond the greeting: every employee-scoped endpoint (attendance-check,
        /// tracker-sessions) resolves s_bpartner_employee_id server-side from this token, so a
        /// login that returns no employee identity is the first thing to check when those
        /// endpoints reject the user.
        /// </summary>
        private static void LogIdentity(Dictionary<string, object> response, Dictionary<string, object> user, string token)
        {
            DebugLog.Write("auth", "Login OK. Token " + DebugLog.Fingerprint(token));
            DebugLog.Write("auth", "Response keys: " + DebugLog.DescribeShape(response));

            if (user == null)
            {
                DebugLog.Write("auth", "No 'user' object in the response — the profile may be nested "
                    + "under another key (see the response keys above) or omitted entirely.");
                return;
            }

            DebugLog.Write("auth", "user keys: " + DebugLog.DescribeShape(user));

            string[] idKeys = { "s_bpartner_employee_id", "employee_id", "id" };
            foreach (string key in idKeys)
            {
                object value;
                bool present = user.TryGetValue(key, out value) && value != null;
                DebugLog.Write("auth", "  user." + key + " = " + (present ? value.ToString() : "(absent)"));
            }

            DebugLog.Write("auth", "Resolved account Id = " + (ApiClient.ReadString(user, "id") ?? "(none)")
                + ", EmployeeId (s_bpartner_employee_id) = " + (ReadEmployeeId(user) ?? "(none)"));
        }

        /// <summary>GET /user — refreshes the profile using an existing token.</summary>
        public static async Task<AuthenticatedUser> GetCurrentUserAsync(string token)
        {
            Dictionary<string, object> response = await ApiClient.GetAsync("user", token).ConfigureAwait(false);
            return MapUser(response, null);
        }

        /// <summary>POST /logout. Never throws — a failed logout must not trap the user in the app.</summary>
        public static async Task LogoutAsync(string token)
        {
            if (RpvConfig.DemoMode || string.IsNullOrEmpty(token))
            {
                return;
            }

            try
            {
                await ApiClient.PostAsync("logout", new { }, token).ConfigureAwait(false);
            }
            catch (ApiException)
            {
                // The local session is cleared by the caller regardless, so a server-side
                // logout failure is not worth blocking or alarming the user over.
            }
        }

        private static AuthenticatedUser MapUser(Dictionary<string, object> user, string fallbackUsername)
        {
            if (user == null)
            {
                return new AuthenticatedUser { FullName = fallbackUsername, Role = "Employee" };
            }

            string first = ApiClient.ReadString(user, "firstname", "first_name");
            string last = ApiClient.ReadString(user, "lastname", "last_name");
            string name = ApiClient.ReadString(user, "name", "fullname", "full_name");

            if (string.IsNullOrWhiteSpace(name))
            {
                name = string.Join(" ", new[] { first, last }).Trim();
            }

            Dictionary<string, object> employee = ApiClient.ReadMap(user, "employee");

            return new AuthenticatedUser
            {
                Id = ApiClient.ReadString(user, "id"),
                EmployeeId = ReadEmployeeId(user),
                BusinessPartnerId = ReadEither(user, employee, "s_bpartner_id"),
                EmployeeNo = ReadEither(user, employee, "employee_no"),
                Username = ApiClient.ReadString(user, "username"),
                FullName = string.IsNullOrWhiteSpace(name) ? fallbackUsername : name,
                Email = ApiClient.ReadString(user, "email"),
                Role = ApiClient.ReadString(user, "role", "position", "designation") ?? "Employee"
            };
        }

        /// <summary>Reads a key off the user object, falling back to the nested employee copy.</summary>
        private static string ReadEither(Dictionary<string, object> user, Dictionary<string, object> employee, string key)
        {
            string value = ApiClient.ReadString(user, key);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
            return employee != null ? ApiClient.ReadString(employee, key) : null;
        }

        /// <summary>
        /// Pulls s_bpartner_employee_id out of the login response. It arrives on the user object
        /// itself, but the same value is also repeated on a nested "employee" object, so that is
        /// checked as a fallback rather than failing when only the nested copy is present.
        /// </summary>
        private static string ReadEmployeeId(Dictionary<string, object> user)
        {
            string id = ApiClient.ReadString(user, "s_bpartner_employee_id", "employee_id");
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }

            Dictionary<string, object> employee = ApiClient.ReadMap(user, "employee");
            return employee != null
                ? ApiClient.ReadString(employee, "s_bpartner_employee_id", "employee_id")
                : null;
        }

        private static async Task<LoginResult> DemoLoginAsync(string username, string password)
        {
            // A short delay so the button's loading state is actually visible while
            // reviewing the UI without a backend.
            await Task.Delay(400).ConfigureAwait(false);

            bool matches = string.Equals(username.Trim(), DemoUsername, StringComparison.OrdinalIgnoreCase)
                           && password == DemoPassword;

            if (!matches)
            {
                throw new ApiException("That username and password combination didn't work. Demo mode expects demo / demo.");
            }

            return new LoginResult
            {
                Token = "demo-token",
                User = new AuthenticatedUser
                {
                    Id = "1001",
                    EmployeeId = "3723",
                    BusinessPartnerId = "3363",
                    EmployeeNo = "C3793",
                    Username = "demo",
                    FullName = "Sarah Mendoza",
                    Email = "sarah.mendoza@rpvespera.com",
                    Role = "HR Manager"
                }
            };
        }
    }
}
