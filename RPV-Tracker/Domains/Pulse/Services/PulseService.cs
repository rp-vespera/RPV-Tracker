using System.Collections.Generic;
using System.Threading.Tasks;
using RPV_Tracker.Domains.Pulse.Models;
using RPV_Tracker.Infrastructure;

namespace RPV_Tracker.Domains.Pulse.Services
{
    /// <summary>
    /// Reads the signed-in person's tasks and performance from the Pulse / Habit service.
    /// Endpoints mirror what the web client calls: shared/me and me/performance.
    /// </summary>
    internal static class PulseService
    {
        /// <summary>GET /api/shared/me → the person's task list (empty list if none).</summary>
        public static async Task<List<PulseTask>> GetMyTasksAsync()
        {
            SharedMeResponse response = await PulseApiClient.GetAsync<SharedMeResponse>("shared/me").ConfigureAwait(false);
            return response != null && response.reminders != null
                ? response.reminders
                : new List<PulseTask>();
        }

        /// <summary>GET /api/me/performance → the person's score and overdue breakdown.</summary>
        public static async Task<Performance> GetMyPerformanceAsync()
        {
            return await PulseApiClient.GetAsync<Performance>("me/performance").ConfigureAwait(false);
        }
    }
}
