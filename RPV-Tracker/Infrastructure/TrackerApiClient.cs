using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RPV_Tracker.Infrastructure
{
    /// <summary>
    /// HTTP client for the RPV backend's time-tracker upload endpoints
    /// (POST tracker/screenshot, POST tracker/session). Separate from the Pulse and
    /// Sanctum clients: it targets its own host (Rpv.Tracker.ApiBaseUrl) and
    /// authenticates with the shared-secret X-Tracker-Token header.
    /// </summary>
    internal static class TrackerApiClient
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
                BaseAddress = new Uri(RpvConfig.TrackerApiBaseUrl + "/"),
                // Screenshot uploads carry an image; allow more time than a JSON call.
                Timeout = TimeSpan.FromSeconds(Math.Max(RpvConfig.RequestTimeoutSeconds, 60))
            };
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        /// <summary>POST a JSON body and deserialize the response into <typeparamref name="T"/>.</summary>
        public static Task<T> PostJsonAsync<T>(string path, object payload)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'))
            {
                Content = new StringContent(Json.Serialize(payload), Encoding.UTF8, "application/json")
            };
            return SendAsync<T>(request);
        }

        /// <summary>
        /// POST a file (multipart/form-data) plus string fields — used to upload a screenshot.
        /// The file is streamed from disk so large captures don't sit in memory.
        /// </summary>
        public static async Task<T> PostFileAsync<T>(
            string path,
            string fileFieldName,
            string filePath,
            string contentType,
            IDictionary<string, string> fields)
        {
            if (!File.Exists(filePath))
            {
                throw new ApiException("Screenshot file to upload was not found: " + filePath);
            }

            using (var form = new MultipartFormDataContent())
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fields != null)
                {
                    foreach (KeyValuePair<string, string> field in fields)
                    {
                        form.Add(new StringContent(field.Value ?? string.Empty), field.Key);
                    }
                }

                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                form.Add(fileContent, fileFieldName, Path.GetFileName(filePath));

                var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/')) { Content = form };
                return await SendAsync<T>(request).ConfigureAwait(false);
            }
        }

        private static async Task<T> SendAsync<T>(HttpRequestMessage request)
        {
            string token = RpvConfig.TrackerToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ApiException("No tracker token is configured. Set Rpv.Tracker.Token in App.config.");
            }
            request.Headers.TryAddWithoutValidation("X-Tracker-Token", token);

            HttpResponseMessage response;
            try
            {
                response = await Http.SendAsync(request).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                throw new ApiException("The tracker service took too long to respond.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ApiException("Can't reach the tracker service right now.", ex);
            }

            string body;
            using (response)
            {
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApiException(DescribeFailure((int)response.StatusCode));
                }
            }

            try
            {
                return Json.Deserialize<T>(body);
            }
            catch (Exception ex)
            {
                throw new ApiException("The tracker service returned data we couldn't read.", ex);
            }
        }

        private static string DescribeFailure(int statusCode)
        {
            switch (statusCode)
            {
                case 401:
                case 403:
                    return "The tracker token was rejected. Check Rpv.Tracker.Token matches the backend's TRACKER_TOKEN.";
                case 404:
                    return "The tracker endpoint wasn't found. Check Rpv.Tracker.ApiBaseUrl.";
                case 413:
                    return "The screenshot was too large for the server to accept.";
                case 422:
                    return "The server rejected the upload (missing or invalid fields).";
                case 502:
                    return "The server could not store the screenshot in Cloudflare R2.";
                default:
                    return "The tracker service returned an unexpected error (" + statusCode + ").";
            }
        }
    }
}
