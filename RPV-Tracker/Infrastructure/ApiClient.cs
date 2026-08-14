using System;
using System.Collections.Generic;
using System.IO;
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
            string body = Json.Serialize(payload);
            DebugLog.Write("api", "POST " + path + " body " + DebugLog.Body(body));

            var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            return await SendAsync(request, bearerToken).ConfigureAwait(false);
        }

        public static async Task<Dictionary<string, object>> GetAsync(string path, string bearerToken = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path.TrimStart('/'));
            return await SendAsync(request, bearerToken).ConfigureAwait(false);
        }

        public static async Task<Dictionary<string, object>> PutAsync(string path, object payload, string bearerToken = null)
        {
            string body = Json.Serialize(payload);
            DebugLog.Write("api", "PUT " + path + " body " + DebugLog.Body(body));

            var request = new HttpRequestMessage(HttpMethod.Put, path.TrimStart('/'))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            return await SendAsync(request, bearerToken).ConfigureAwait(false);
        }

        /// <summary>
        /// POST a file (multipart/form-data) plus string fields, authenticated with a bearer
        /// token — used by the tracker-sessions upload, which identifies the signed-in employee
        /// via their Sanctum token.
        /// </summary>
        public static async Task<Dictionary<string, object>> PostFileAsync(
            string path,
            string fileFieldName,
            string filePath,
            string contentType,
            IDictionary<string, string> fields,
            string bearerToken = null)
        {
            if (!File.Exists(filePath))
            {
                throw new ApiException("File to upload was not found: " + filePath);
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

                DebugLog.Write("api", "POST " + path + " multipart " + fileFieldName + "="
                    + Path.GetFileName(filePath) + " (" + fileStream.Length + " bytes)"
                    + (fields != null ? " fields " + string.Join(", ", DescribeFields(fields)) : string.Empty));

                var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/')) { Content = form };
                return await SendAsync(request, bearerToken).ConfigureAwait(false);
            }
        }

        private static async Task<Dictionary<string, object>> SendAsync(HttpRequestMessage request, string bearerToken)
        {
            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            Uri absolute = request.RequestUri.IsAbsoluteUri
                ? request.RequestUri
                : new Uri(Http.BaseAddress, request.RequestUri);
            string label = request.Method + " " + absolute;

            DebugLog.Write("api", "→ " + label + "  auth=" + DebugLog.Fingerprint(bearerToken));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            HttpResponseMessage response;
            try
            {
                response = await Http.SendAsync(request).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                DebugLog.Write("api", "✗ " + label + " timed out after " + stopwatch.ElapsedMilliseconds + " ms");
                throw new ApiException("The server took too long to respond. Check your connection and try again.", ex);
            }
            catch (HttpRequestException ex)
            {
                DebugLog.Write("api", "✗ " + label + " could not be reached after " + stopwatch.ElapsedMilliseconds + " ms");
                DebugLog.Exception("api", ex);
                throw new ApiException("We can't reach the RPV server right now. Check your connection and try again.", ex);
            }

            string body;
            using (response)
            {
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                DebugLog.Write("api", "← " + (int)response.StatusCode + " " + response.StatusCode
                    + " in " + stopwatch.ElapsedMilliseconds + " ms  " + label);
                DebugLog.Write("api", "   body " + DebugLog.Body(body));

                if (!response.IsSuccessStatusCode)
                {
                    throw new ApiException(DescribeFailure((int)response.StatusCode, body));
                }
            }

            return Parse(body);
        }

        private static string[] DescribeFields(IDictionary<string, string> fields)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, string> field in fields)
            {
                parts.Add(field.Key + "=" + field.Value);
            }
            return parts.ToArray();
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

        /// <summary>Reads a JSON array of objects — JavaScriptSerializer hands these back as
        /// object[], each element already a Dictionary&lt;string, object&gt;.</summary>
        public static List<Dictionary<string, object>> ReadList(Dictionary<string, object> map, string key)
        {
            var result = new List<Dictionary<string, object>>();

            object value;
            if (map == null || !map.TryGetValue(key, out value))
            {
                return result;
            }

            var items = value as System.Collections.IEnumerable;
            if (items == null || value is string)
            {
                return result;
            }

            foreach (object item in items)
            {
                var itemMap = item as Dictionary<string, object>;
                if (itemMap != null)
                {
                    result.Add(itemMap);
                }
            }
            return result;
        }

        public static bool ReadBool(Dictionary<string, object> map, string key, bool fallback = false)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value) && value != null)
            {
                if (value is bool)
                {
                    return (bool)value;
                }

                string text = value.ToString();
                bool parsed;
                if (bool.TryParse(text, out parsed))
                {
                    return parsed;
                }

                // A tinyint column that isn't cast to bool on the server arrives as 1/0 (or
                // "1"/"0"), which bool.TryParse rejects outright — treating that as false is
                // how an eligible employee silently loses a flag like can_request_ot.
                int number;
                if (int.TryParse(text, out number))
                {
                    return number != 0;
                }
            }
            return fallback;
        }
    }
}
