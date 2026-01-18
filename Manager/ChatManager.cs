using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenGSCore;

namespace OpenGSServer
{
    public class ChatManager
    {
        private List<Chat> log = new();
        private readonly int maxLogSize;

        public List<Chat> Log { get => log; }

        public ChatManager(int maxLogSize = 1000)
        {
            this.maxLogSize = maxLogSize;
        }

        public void AddChat(Chat chat)
        {
            Log.Add(chat);
            // ログサイズ上限を超えたら古いものを削除
            if (Log.Count > maxLogSize)
            {
                Log.RemoveAt(0);
            }
        }

        public Chat NewChat(string playerId, string playerName, string message, ChatType type = ChatType.All)
        {
            var chat = new Chat(playerId, playerName, message, type);
            AddChat(chat);
            return chat;
        }

        public List<Chat> AllChat()
        {
            return Log.Where(c => c.Type == ChatType.All).ToList();
        }

        public List<Chat> TeamChat()
        {
            return Log.Where(c => c.Type == ChatType.Team).ToList();
        }

        public List<Chat> GetChatsByPlayer(string playerName)
        {
            return Log.Where(c => c.PlayerName == playerName).ToList();
        }

        public List<Chat> GetChatsByPlayerId(string playerId)
        {
            return Log.Where(c => c.PlayerId == playerId).ToList();
        }

        public Chat? GetChatById(string chatId)
        {
            return Log.FirstOrDefault(c => c.Id == chatId);
        }

        public List<Chat> GetRecentChats(int count)
        {
            return Log.TakeLast(count).ToList();
        }

        public void Clear()
        {
            Log.Clear();
        }
    }
}
