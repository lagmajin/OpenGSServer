using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class GuildManager
    {
        private readonly GuildDatabaseManager _database = GuildDatabaseManager.GetInstance();
        private readonly object _syncRoot = new();

        public static GuildManager Instance { get; } = new();

        public bool Exist(in string guildName)
        {
            return _database.ExistGuild(guildName);
        }

        public bool CreateNewGuild(in string guildName, string leaderId, string guildShortName = null)
        {
            if (string.IsNullOrWhiteSpace(guildName) || string.IsNullOrWhiteSpace(leaderId))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _database.CreateNewGuild(guildName, leaderId, guildShortName);
            }
        }

        public bool RemoveGuild(in string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _database.RemoveGuild(guildName);
            }
        }

        public void RemoveAllGuild()
        {
            lock (_syncRoot)
            {
                _database.RemoveAllGuild();
            }
        }

        public bool AddGuildMember(in string memberId, in string guildName, string role = "Member")
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _database.AddGuildMember(memberId, guildName, role);
            }
        }

        public int AddGuildMembers(IEnumerable<string> memberIds, in string guildName)
        {
            if (memberIds == null)
            {
                return 0;
            }

            lock (_syncRoot)
            {
                return _database.AddGuildMembers(memberIds, guildName);
            }
        }

        public DBGuild? FindGuild(in string guildName)
        {
            return _database.FindGuildByName(guildName);
        }

        public IReadOnlyList<DBGuildMember> GetGuildMembers(in string guildName)
        {
            return _database.GetGuildMember(guildName);
        }

        /// <summary>
        /// ギルドチャットを配信
        /// </summary>
        public void BroadcastGuildChat(string guildName, string senderId, string message)
        {
            var members = GetGuildMembers(guildName);
            if (members.Count == 0) return;

            var chatJson = new JObject
            {
                ["MessageType"] = NetworkingConstants.MessageType.Chat,
                ["ChatType"] = "Guild",
                ["GuildName"] = guildName,
                ["SenderID"] = senderId,
                ["Message"] = message,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            var lobbyManager = LobbyServerManager.Instance;
            foreach (var member in members)
            {
                // ロビーにいるメンバーにのみ送信（実際にはセッション管理経由）
                lobbyManager.SendToPlayer(member.Id, chatJson);
            }

            ConsoleWrite.WriteMessage($"[GUILD] {guildName}: <{senderId}> {message}", ConsoleColor.Cyan);
        }

        /// <summary>
        /// ギルドの経験値を加算
        /// </summary>
        public void AddGuildExp(string guildName, long exp)
        {
            lock (_syncRoot)
            {
                var guild = _database.FindGuildByName(guildName);
                if (guild == null) return;

                guild.Experience += exp;
                
                // 簡易的なレベルアップロジック (1000 * Level)
                long nextLevelExp = guild.Level * 1000;
                while (guild.Experience >= nextLevelExp)
                {
                    guild.Experience -= nextLevelExp;
                    guild.Level++;
                    nextLevelExp = guild.Level * 1000;
                    ConsoleWrite.WriteMessage($"[GUILD] {guildName} leveled up to {guild.Level}!", ConsoleColor.Yellow);
                }

                // 本来はデータベースへの更新が必要
                // _database.UpdateGuild(guild); // 追加が必要かもしれない
            }
        }
    }
}
