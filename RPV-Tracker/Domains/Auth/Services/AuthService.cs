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
                throw new ApiException("Sign-in succeeded but the server didn't return a token. Contact your HR administrator.");
            }

            return new LoginResult
            {
                Token = token,
                User = MapUser(ApiClient.ReadMap(response, "user"), username)
            };
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

            return new AuthenticatedUser
            {
                Id = ApiClient.ReadString(user, "id", "employee_id", "s_bpartner_employee_id"),
                FullName = string.IsNullOrWhiteSpace(name) ? fallbackUsername : name,
                Email = ApiClient.ReadString(user, "email"),
                Role = ApiClient.ReadString(user, "role", "position", "designation") ?? "Employee"
            };
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
                    FullName = "Sarah Mendoza",
                    Email = "sarah.mendoza@rpvespera.com",
                    Role = "HR Manager"
                }
            };
        }
    }
}
