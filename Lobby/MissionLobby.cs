using System.Collections.Generic;
using System.Linq;

namespace OpenGSServer
{
    public class MissionLobby
    {
        private readonly List<string> chatLog = new();
        private readonly HashSet<string> users = new();

        public MissionLobby()
        {
        }

        public IReadOnlyList<string> ChatLog => chatLog;
        public IReadOnlyCollection<string> Users => users;

        public void ClearChat()
        {
            chatLog.Clear();
        }

        public void AddNewUser(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            users.Add(playerId);
        }

        public void RemoveUser(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            users.Remove(playerId);
        }

        public void AddChatLine(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                chatLog.Add(message.Trim());
            }
        }
    }
}
