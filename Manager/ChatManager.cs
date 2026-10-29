using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenGSCore;
using CoreChat = OpenGSCore.Chat;

#nullable enable

namespace OpenGSServer
{
    public class ChatManager
    {
        private List<CoreChat> log = new();
        private readonly int maxLogSize;

        public List<CoreChat> Log { get => log; }

        public ChatManager(int maxLogSize = 1000)
        {
            this.maxLogSize = maxLogSize;
        }

        public void AddChat(CoreChat chat)
        {
            Log.Add(chat);
            // ログサイズ上限を超えたら古いものを削除
            if (Log.Count > maxLogSize)
            {
                Log.RemoveAt(0);
            }
        }

        public CoreChat NewChat(string playerId, string playerName, string message, ChatType type = ChatType.All)
        {
            var chat = new CoreChat(playerId, playerName, message, type);
            AddChat(chat);
            return chat;
        }

        public List<CoreChat> AllChat()
        {
            return Log.Where(c => c.Type == ChatType.All).ToList();
        }

        public List<CoreChat> TeamChat()
        {
            return Log.Where(c => c.Type == ChatType.Team).ToList();
        }

        public List<CoreChat> GetChatsByPlayer(string playerName)
        {
            return Log.Where(c => c.PlayerName == playerName).ToList();
        }

        public List<CoreChat> GetChatsByPlayerId(string playerId)
        {
            return Log.Where(c => string.Equals(c.PlayerId, playerId, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public CoreChat? GetChatById(string chatId)
        {
            return Log.FirstOrDefault(c => c.Id == chatId);
        }

        public List<CoreChat> GetRecentChats(int count)
        {
            return Log.TakeLast(count).ToList();
        }

        public void Clear()
        {
            Log.Clear();
        }
    }
}
