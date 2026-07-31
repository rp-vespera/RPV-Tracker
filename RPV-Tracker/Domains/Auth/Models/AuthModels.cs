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
        public string Id { get; set; }
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
