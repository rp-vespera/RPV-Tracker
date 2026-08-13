using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RPV_Tracker.Infrastructure
{
    /// <summary>
    /// Append-only diagnostic log written next to the app's other local state. Exists so a
    /// failure that the UI deliberately swallows — a failed attendance check, an upload that
    /// didn't land, a login response missing the fields we expect — can still be inspected
    /// afterwards instead of vanishing into an empty catch block.
    /// </summary>
    /// <remarks>
    /// Secrets are never written in full: bearer tokens and the Pulse token are reduced to a
    /// fingerprint, and password/token values inside JSON bodies are masked. Everything also
    /// goes to <see cref="Debug"/>, so a debugger attached in Visual Studio sees the same
    /// stream in the Output window without opening the file.
    /// </remarks>
    internal static class DebugLog
    {
        private const int MaxBodyChars = 4000;
        private const long MaxFileBytes = 5L * 1024 * 1024;

        private static readonly object Gate = new object();

        // Values that must never be written out verbatim, matched as JSON string properties.
        private static readonly Regex SecretJson = new Regex(
            "\"(token|access_token|refresh_token|api_token|password|password_confirmation|habit_token)\"\\s*:\\s*\"[^\"]*\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Folder holding the log files.</summary>
        public static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RPV Workforce", "logs");
            }
        }

        /// <summary>Today's log file. One file per day keeps a single session easy to find.</summary>
        public static string FilePath
        {
            get { return Path.Combine(Folder, "rpv-tracker-" + DateTime.Now.ToString("yyyyMMdd") + ".log"); }
        }

        public static void Write(string category, string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  [" + category + "]  " + message;

            Debug.WriteLine(line);

            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Folder);

                    string path = FilePath;
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length > MaxFileBytes)
                    {
                        // A runaway log must not fill the profile. Start over rather than
                        // rotating — a day's worth of recent lines is what's useful here.
                        File.WriteAllText(path, DateTime.Now.ToString("HH:mm:ss.fff")
                            + "  [log]  Previous content dropped (over " + (MaxFileBytes / 1024 / 1024) + " MB)."
                            + Environment.NewLine);
                    }

                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
                // Logging is diagnostic only — a locked or unwritable file must never affect
                // the behaviour of the thing being diagnosed.
            }
        }

        public static void Exception(string category, Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            Write(category, ex.GetType().Name + ": " + ex.Message);

            for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                Write(category, "  caused by " + inner.GetType().Name + ": " + inner.Message);
            }
        }

        /// <summary>Masks secret values inside a JSON body and clips it to a loggable length.</summary>
        public static string Body(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return "(empty)";
            }

            string masked = SecretJson.Replace(body, "\"$1\":\"***\"");
            return masked.Length > MaxBodyChars
                ? masked.Substring(0, MaxBodyChars) + "… (" + masked.Length + " chars total)"
                : masked;
        }

        /// <summary>
        /// Enough of a token to confirm it is present and to tell two tokens apart, without
        /// writing anything that could be replayed.
        /// </summary>
        public static string Fingerprint(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                return "(none)";
            }
            if (secret.Length <= 8)
            {
                return "(" + secret.Length + " chars)";
            }
            return secret.Substring(0, 4) + "…" + secret.Substring(secret.Length - 4)
                + " (" + secret.Length + " chars)";
        }

        /// <summary>
        /// The keys of a parsed JSON object, with each value's shape — the quickest way to see
        /// whether a field we expected (an employee id, say) actually came back, and whether
        /// it arrived at the top level or nested inside another object.
        /// </summary>
        public static string DescribeShape(IDictionary<string, object> map)
        {
            if (map == null)
            {
                return "(null)";
            }
            if (map.Count == 0)
            {
                return "(no keys)";
            }

            var parts = new List<string>();
            foreach (KeyValuePair<string, object> pair in map)
            {
                parts.Add(pair.Key + ":" + Shape(pair.Value));
            }
            return string.Join(", ", parts.ToArray());
        }

        private static string Shape(object value)
        {
            if (value == null)
            {
                return "null";
            }

            var nested = value as IDictionary<string, object>;
            if (nested != null)
            {
                return "object{" + nested.Count + "}";
            }

            var list = value as ArrayList;
            if (list != null)
            {
                return "array[" + list.Count + "]";
            }

            return value.GetType().Name;
        }
    }
}
