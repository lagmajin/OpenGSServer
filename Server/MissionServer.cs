using System;

namespace OpenGSServer
{
    public class MissionRUDPServer
    {
        public bool IsListening { get; private set; }
        public DateTime? StartedAtUtc { get; private set; }
        public int ListenCount { get; private set; }

        public void Listen()
        {
            if (IsListening)
            {
                ConsoleWrite.WriteMessage("[MISSION] MissionRUDPServer is already listening", ConsoleColor.Yellow);
                return;
            }

            IsListening = true;
            StartedAtUtc = DateTime.UtcNow;
            ListenCount++;
            ConsoleWrite.WriteMessage("[MISSION] MissionRUDPServer.Listen() called", ConsoleColor.Cyan);
        }

        public void Shutdown()
        {
            if (!IsListening)
            {
                return;
            }

            IsListening = false;
            ConsoleWrite.WriteMessage("[MISSION] MissionRUDPServer shutdown", ConsoleColor.Yellow);
        }
    }
}
