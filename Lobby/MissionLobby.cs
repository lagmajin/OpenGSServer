using System.Collections.Generic;
using Newtonsoft.Json.Linq;

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
        public int UserCount => users.Count;

        public void ClearChat()
        {
            chatLog.Clear();
        }

        public void ClearUsers()
        {
            users.Clear();
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

        public bool HasUser(string playerId)
        {
            return !string.IsNullOrWhiteSpace(playerId) && users.Contains(playerId);
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["UserCount"] = UserCount,
                ["Users"] = new JArray(users),
                ["ChatLog"] = new JArray(chatLog)
            };
        }
    }
}
