using System;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public static class ManagementServerEvent
    {
        public static DateTime? LastAdminLoginRequestUtc { get; private set; }
        public static DateTime? LastAdminLogoutRequestUtc { get; private set; }
        public static DateTime? LastUpdateRequestUtc { get; private set; }
        public static int AdminLoginRequestCount { get; private set; }
        public static int AdminLogoutRequestCount { get; private set; }
        public static int UpdateRequestCount { get; private set; }

        public static void ProcessAdminLoginRequest()
        {
            LastAdminLoginRequestUtc = DateTime.UtcNow;
            AdminLoginRequestCount++;
            ConsoleWrite.WriteMessage("[ManagementEvent] Admin login request received", ConsoleColor.Cyan);
        }

        public static void ProcessAdminLogoutRequest()
        {
            LastAdminLogoutRequestUtc = DateTime.UtcNow;
            AdminLogoutRequestCount++;
            ConsoleWrite.WriteMessage("[ManagementEvent] Admin logout request received", ConsoleColor.Cyan);
        }

        public static void ProcessUpdateRequest()
        {
            try
            {
                LastUpdateRequestUtc = DateTime.UtcNow;
                UpdateRequestCount++;
                ServerInfoDatabaseManager.Instance?.UpdateDatabase();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ManagementEvent] Update request failed: {ex.Message}", ConsoleColor.Red);
            }
        }
    }
}
