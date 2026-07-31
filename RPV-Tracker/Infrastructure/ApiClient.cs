using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RPV_Tracker.Infrastructure
{
    /// <summary>Raised for any non-success API response, carrying a user-safe message.</summary>
    internal class ApiException : Exception
    {
        public ApiException(string message) : base(message) { }
        public ApiException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thin HTTP wrapper over the RPV Workforce API — the desktop equivalent of the
    /// web client's axios layer. Responses come back as loosely-typed dictionaries so
    /// services can map only the fields they need without a JSON library dependency.
    /// </summary>
    internal static class ApiClient
    {
        // One HttpClient for the process. Creating one per request exhausts sockets.
        private static readonly HttpClient Http = CreateClient();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(RpvConfig.ApiBaseUrl + "/"),
                Timeout = TimeSpan.FromSeconds(RpvConfig.RequestTimeoutSeconds)
            };
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public static async Task<Dictionary<string, object>> PostAsync(string path, object payload, string bearerToken = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'))
            {
                Content = new StringContent(Json.Serialize(payload), Encoding.UTF8, "application/json")
            };
            return await SendAsync(request, bearerToken).ConfigureAwait(false);
        }

        public static async Task<Dictionary<string, object>> GetAsync(string path, string bearerToken = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path.TrimStart('/'));
            return await SendAsync(request, bearerToken).ConfigureAwait(false);
        }

        private static async Task<Dictionary<string, object>> SendAsync(HttpRequestMessage request, string bearerToken)
        {
            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            HttpResponseMessage response;
            try
            {
                response = await Http.SendAsync(request).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                throw new ApiException("The server took too long to respond. Check your connection and try again.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ApiException("We can't reach the RPV server right now. Check your connection and try again.", ex);
            }

            string body;
            using (response)
            {
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApiException(DescribeFailure((int)response.StatusCode, body));
                }
            }

            return Parse(body);
        }

        private static string DescribeFailure(int statusCode, string body)
        {
            // Prefer the API's own message — Laravel returns { "message": "..." } on validation
            // and auth failures, and it is more specific than anything we could invent here.
            string apiMessage = ReadString(Parse(body), "message");
            if (!string.IsNullOrWhiteSpace(apiMessage))
            {
                return apiMessage;
            }

            switch (statusCode)
            {
                case 401:
                case 422:
                    return "That username and password combination didn't work. Check them and try again.";
                case 403:
                    return "Your account doesn't have access to this app. Contact your HR administrator.";
                case 404:
                    return "The sign-in service wasn't found at the configured address. Check Rpv.ApiBaseUrl in App.config.";
                default:
                    return "The server returned an unexpected error (" + statusCode + "). Try again in a moment.";
            }
        }

        private static Dictionary<string, object> Parse(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                return Json.Deserialize<Dictionary<string, object>>(body) ?? new Dictionary<string, object>();
            }
            catch (Exception)
            {
                // A non-JSON body (an HTML error page, usually) is not something the caller
                // can interpret, so hand back an empty map and let it fall through to a
                // status-code-based message.
                return new Dictionary<string, object>();
            }
        }

        public static string ReadString(Dictionary<string, object> map, params string[] keys)
        {
            if (map == null)
            {
                return null;
            }

            foreach (string key in keys)
            {
                object value;
                if (map.TryGetValue(key, out value) && value != null)
                {
                    string text = value.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
            return null;
        }

        public static Dictionary<string, object> ReadMap(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value))
            {
                return value as Dictionary<string, object>;
            }
            return null;
        }
    }
}
