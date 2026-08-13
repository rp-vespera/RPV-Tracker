using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RPV_Tracker.Infrastructure
{
    /// <summary>
    /// HTTP client for the Pulse / Habit service (tasks + performance). Separate from
    /// <see cref="ApiClient"/> because it targets a different host and authenticates with an
    /// X-Habit-Token header instead of a Sanctum Bearer token. Deserializes straight into
    /// typed DTOs whose property names mirror the API's JSON keys.
    /// </summary>
    internal static class PulseApiClient
    {
        private static readonly HttpClient Http = CreateClient();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        private static HttpClient CreateClient()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch (NotSupportedException)
            {
            }

            var client = new HttpClient
            {
                BaseAddress = new Uri(RpvConfig.HabitApiBaseUrl + "/"),
                Timeout = TimeSpan.FromSeconds(RpvConfig.RequestTimeoutSeconds)
            };
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public static async Task<T> GetAsync<T>(string path)
        {
            string token = RpvConfig.HabitToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ApiException("No Pulse token is configured. Set Rpv.Habit.Token in App.config.");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, path.TrimStart('/'));
            request.Headers.TryAddWithoutValidation("X-Habit-Token", token);

            string label = "GET " + new Uri(Http.BaseAddress, path.TrimStart('/'));
            DebugLog.Write("pulse", "→ " + label + "  X-Habit-Token=" + DebugLog.Fingerprint(token));

            HttpResponseMessage response;
            try
            {
                response = await Http.SendAsync(request).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                DebugLog.Write("pulse", "✗ " + label + " timed out");
                throw new ApiException("The tasks service took too long to respond.", ex);
            }
            catch (HttpRequestException ex)
            {
                DebugLog.Write("pulse", "✗ " + label + " could not be reached");
                DebugLog.Exception("pulse", ex);
                throw new ApiException("Can't reach the tasks service right now. Check your connection.", ex);
            }

            string body;
            using (response)
            {
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                DebugLog.Write("pulse", "← " + (int)response.StatusCode + " " + response.StatusCode + "  " + label);

                if (!response.IsSuccessStatusCode)
                {
                    DebugLog.Write("pulse", "   body " + DebugLog.Body(body));
                    throw new ApiException(DescribeFailure((int)response.StatusCode));
                }
            }

            try
            {
                return Json.Deserialize<T>(body);
            }
            catch (Exception ex)
            {
                throw new ApiException("The tasks service returned data we couldn't read.", ex);
            }
        }

        private static string DescribeFailure(int statusCode)
        {
            switch (statusCode)
            {
                case 401:
                case 403:
                    return "Your tasks session isn't valid. The Pulse token may have expired — sign in again on the web app and update Rpv.Habit.Token.";
                case 404:
                    return "The tasks endpoint wasn't found at the configured address. Check Rpv.Habit.ApiBaseUrl.";
                default:
                    return "The tasks service returned an unexpected error (" + statusCode + ").";
            }
        }
    }
}
