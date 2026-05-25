using System;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public static class ManagementServerEvent
    {
        public static void ProcessAdminLoginRequest()
        {
            ConsoleWrite.WriteMessage("[ManagementEvent] Admin login request received", ConsoleColor.Cyan);
        }

        public static void ProcessAdminLogoutRequest()
        {
            ConsoleWrite.WriteMessage("[ManagementEvent] Admin logout request received", ConsoleColor.Cyan);
        }

        public static void ProcessUpdateRequest()
        {
            try
            {
                ServerInfoDatabaseManager.Instance?.UpdateDatabase();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ManagementEvent] Update request failed: {ex.Message}", ConsoleColor.Red);
            }
        }
    }
}
