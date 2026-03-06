



using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommandLine;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    /// <summary>
    /// コンソールコマンドラインオプション
    /// </summary>
    public class CommandOptions
    {
        [Option('h', "help", Required = false, HelpText = "ヘルプを表示します")]
        public bool Help { get; set; }

        [Option('c', "command", Required = false, HelpText = "実行するコマンド")]
        public string Command { get; set; }
    }

    /// <summary>
    /// コマンド解析・実行エンジン
    /// </summary>
    public static class CommandParser
    {
        /// <summary>
        /// コマンド文字列を解析して実行します
        /// </summary>
        public static void Parse(in string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                return;
            }

            var tokens = Tokenize(args);
            if (tokens.Length == 0)
            {
                return;
            }

            var command = tokens[0].ToLower();
            var parameters = tokens.Skip(1).ToList();

            ExecuteCommand(command, parameters);
        }

        /// <summary>
        /// 引用符付き引数を考慮してトークン化
        /// 例: addplayer u1 p1 "Player One"
        /// </summary>
        private static string[] Tokenize(string input)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;
            var quoteChar = '\0';

            for (var i = 0; i < input.Length; i++)
            {
                var ch = input[i];

                if (inQuotes)
                {
                    if (ch == '\\' && i + 1 < input.Length)
                    {
                        var next = input[i + 1];
                        if (next == quoteChar || next == '\\')
                        {
                            current.Append(next);
                            i++;
                            continue;
                        }
                    }

                    if (ch == quoteChar)
                    {
                        inQuotes = false;
                        quoteChar = '\0';
                    }
                    else
                    {
                        current.Append(ch);
                    }

                    continue;
                }

                if (char.IsWhiteSpace(ch))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }

                    continue;
                }

                if (ch == '"' || ch == '\'')
                {
                    inQuotes = true;
                    quoteChar = ch;
                    continue;
                }

                current.Append(ch);
            }

            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
            }

            return tokens.ToArray();
        }

        /// <summary>
        /// コマンドを実行します
        /// </summary>
        private static void ExecuteCommand(string command, List<string> parameters)
        {
            switch (command)
            {
                case "help":
                case "?":
                    CommandExecutor.ShowHelp();
                    break;

                case "addplayer":
                    if (parameters.Count >= 3)
                    {
                        CommandExecutor.CreatePlayer(parameters[0], parameters[1], parameters[2]);
                    }
                    else
                    {
                        ConsoleWrite.WriteMessage("[ERR] Usage: addplayer <id> <password> <displayName>", ConsoleColor.Red);
                    }
                    break;

                case "addguild":
                    if (parameters.Count >= 1)
                    {
                        CommandExecutor.CreateGuild(parameters[0]);
                    }
                    else
                    {
                        ConsoleWrite.WriteMessage("[ERR] Usage: addguild <guildName>", ConsoleColor.Red);
                    }
                    break;

                case "addwaitroom":
                    if (parameters.Count >= 1)
                    {
                        CommandExecutor.CreateWaitRoom(parameters[0]);
                    }
                    else
                    {
                        ConsoleWrite.WriteMessage("[ERR] Usage: addwaitroom <roomName>", ConsoleColor.Red);
                    }
                    break;

                case "playerinfo":
                    if (parameters.Count >= 1)
                    {
                        CommandExecutor.PlayerInfo(parameters[0]);
                    }
                    else
                    {
                        ConsoleWrite.WriteMessage("[ERR] Usage: playerinfo <playerId>", ConsoleColor.Red);
                    }
                    break;

                case "guildinfo":
                    if (parameters.Count >= 1)
                    {
                        CommandExecutor.GuildInfo(parameters[0]);
                    }
                    else
                    {
                        ConsoleWrite.WriteMessage("[ERR] Usage: guildinfo <guildName>", ConsoleColor.Red);
                    }
                    break;

                case "lobbyinfo":
                    CommandExecutor.LobbyInfo();
                    break;

                case "matchserverinfo":
                    CommandExecutor.MatchServerInfo();
                    break;

                case "listrooms":
                    CommandExecutor.ListWaitRooms();
                    break;

                case "listplayers":
                    CommandExecutor.ListPlayers();
                    break;

                case "banip":
                    if (parameters.Count >= 1)
                    {
                        CommandExecutor.BanIp(parameters[0]);
                    }
                    else
                    {
                        ConsoleWrite.WriteMessage("[ERR] Usage: banip <ipAddress>", ConsoleColor.Red);
                    }
                    break;

                case "unbanip":
                    if (parameters.Count >= 1)
                    {
                        CommandExecutor.UnbanIp(parameters[0]);
                    }
                    else
                    {
                        ConsoleWrite.WriteMessage("[ERR] Usage: unbanip <ipAddress>", ConsoleColor.Red);
                    }
                    break;

                case "listban":
                    CommandExecutor.ListBannedIps();
                    break;

                case "status":
                    CommandExecutor.ServerStatus();
                    break;

                default:
                    ConsoleWrite.WriteMessage($"[ERR] Unknown command: {command}", ConsoleColor.Red);
                    break;
            }
        }
    }

    /// <summary>
    /// コマンド実行処理
    /// </summary>
    public static class CommandExecutor
    {
        /// <summary>
        /// ヘルプを表示
        /// </summary>
        public static void ShowHelp()
        {
            ConsoleWrite.WriteMessage("=== OpenGS Server Console Commands ===", ConsoleColor.Cyan);
            ConsoleWrite.WriteMessage("addplayer <id> <password> <displayName> - Create new player account", ConsoleColor.White);
            ConsoleWrite.WriteMessage("addguild <guildName> - Create new guild", ConsoleColor.White);
            ConsoleWrite.WriteMessage("addwaitroom <roomName> - Create new wait room", ConsoleColor.White);
            ConsoleWrite.WriteMessage("playerinfo <playerId> - Show player information", ConsoleColor.White);
            ConsoleWrite.WriteMessage("guildinfo <guildName> - Show guild information", ConsoleColor.White);
            ConsoleWrite.WriteMessage("lobbyinfo - Show lobby information", ConsoleColor.White);
            ConsoleWrite.WriteMessage("matchserverinfo - Show match server information", ConsoleColor.White);
            ConsoleWrite.WriteMessage("listrooms - List all wait rooms", ConsoleColor.White);
            ConsoleWrite.WriteMessage("listplayers - List all connected players", ConsoleColor.White);
            ConsoleWrite.WriteMessage("banip <ipAddress> - Block incoming connections from the IP", ConsoleColor.White);
            ConsoleWrite.WriteMessage("unbanip <ipAddress> - Remove the IP from blacklist", ConsoleColor.White);
            ConsoleWrite.WriteMessage("listban - Display currently banned IP addresses", ConsoleColor.White);
            ConsoleWrite.WriteMessage("status - Show quick server runtime summary", ConsoleColor.White);
            ConsoleWrite.WriteMessage("help, ? - Show this help message", ConsoleColor.White);
            ConsoleWrite.WriteMessage("====================================", ConsoleColor.Cyan);
        }

        /// <summary>
        /// 新規プレイヤーを作成
        /// </summary>
        public static void CreatePlayer(string id, string password, string displayName)
        {
            try
            {
                var accountManager = AccountManager.GetInstance();
                accountManager.CreateNewAccount(id, password, displayName);
                ConsoleWrite.WriteMessage($"[OK] Player '{displayName}' (ID: {id}) created successfully", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to create player: {ex.Message}", ConsoleColor.Red);
            }
        }

        public static void CreateGuild(string guildName)
        {
            try
            {
                var manager = GuildManager.Instance;
                if (manager.CreateNewGuild(guildName, "System"))
                {
                    ConsoleWrite.WriteMessage($"[OK] Guild '{guildName}' created successfully", ConsoleColor.Green);
                }
                else
                {
                    ConsoleWrite.WriteMessage($"[ERR] Guild '{guildName}' already exists or name is invalid", ConsoleColor.Red);
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to create guild: {ex.Message}", ConsoleColor.Red);
            }
        }

        public static void BanIp(string ipAddress)
        {
            if (BlackList.Instance.AddIp(ipAddress))
            {
                ConsoleWrite.WriteMessage($"[OK] {ipAddress} added to blacklist", ConsoleColor.Green);
            }
            else
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to add IP '{ipAddress}'. Check the format or duplication.", ConsoleColor.Red);
            }
        }

        public static void UnbanIp(string ipAddress)
        {
            if (BlackList.Instance.RemoveIp(ipAddress))
            {
                ConsoleWrite.WriteMessage($"[OK] {ipAddress} removed from blacklist", ConsoleColor.Green);
            }
            else
            {
                ConsoleWrite.WriteMessage($"[ERR] IP '{ipAddress}' is not currently banned", ConsoleColor.Yellow);
            }
        }

        public static void ListBannedIps()
        {
            var entries = BlackList.Instance.GetEntries();

            if (entries.Count == 0)
            {
                ConsoleWrite.WriteMessage("[INFO] No banned IP addresses", ConsoleColor.Cyan);
                return;
            }

            ConsoleWrite.WriteMessage("=== Banned IP Addresses ===", ConsoleColor.Cyan);
            foreach (var entry in entries)
            {
                ConsoleWrite.WriteMessage(entry, ConsoleColor.White);
            }
        }

        /// <summary>
        /// ウェイトルームを作成
        /// </summary>
        public static void CreateWaitRoom(string roomName)
        {
            try
            {
                var roomManager = WaitRoomManager.Instance();
                roomManager.CreateNewWaitRoom(roomName);
                ConsoleWrite.WriteMessage($"[OK] Wait room '{roomName}' created successfully", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to create wait room: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// プレイヤー情報を表示
        /// </summary>
        public static void PlayerInfo(string playerId)
        {
            try
            {
                var accountManager = AccountDatabaseManager.GetInstance();
                // プレイヤー情報の取得と表示（実装は DBManager に委譲）
                ConsoleWrite.WriteMessage($"[INFO] Player ID: {playerId}", ConsoleColor.Cyan);
                ConsoleWrite.WriteMessage($"[INFO] Status: Online", ConsoleColor.Cyan);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to get player info: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// ギルド情報を表示
        /// </summary>
        public static void GuildInfo(string guildName)
        {
            try
            {
                var manager = GuildManager.Instance;
                var guild = manager.FindGuild(guildName);

                if (guild == null)
                {
                    ConsoleWrite.WriteMessage($"[ERR] Guild '{guildName}' not found", ConsoleColor.Red);
                    return;
                }

                var members = manager.GetGuildMembers(guildName);

                ConsoleWrite.WriteMessage($"[INFO] Guild: {guild.GuildName}", ConsoleColor.Cyan);
                if (!string.IsNullOrWhiteSpace(guild.GuildShortName))
                {
                    ConsoleWrite.WriteMessage($"[INFO] Short Name: {guild.GuildShortName}", ConsoleColor.Cyan);
                }
                ConsoleWrite.WriteMessage($"[INFO] Created: {guild.CreationTime}", ConsoleColor.Cyan);
                ConsoleWrite.WriteMessage($"[INFO] Members: {members.Count}", ConsoleColor.Cyan);
                foreach (var member in members)
                {
                    ConsoleWrite.WriteMessage($" - {member.Id} (joined {member.TimeStamp})", ConsoleColor.White);
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to get guild info: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// ロビー情報を表示
        /// </summary>
        public static void LobbyInfo()
        {
            try
            {
                var roomManager = WaitRoomManager.Instance();
                var roomInfo = roomManager.RoomInfo();
                
                ConsoleWrite.WriteMessage("[INFO] === Lobby Information ===", ConsoleColor.Cyan);
                ConsoleWrite.WriteMessage($"[INFO] {roomInfo.ToString()}", ConsoleColor.Cyan);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to get lobby info: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// マッチサーバー情報を表示
        /// </summary>
        public static void MatchServerInfo()
        {
            try
            {
                ConsoleWrite.WriteMessage("[INFO] === Match Server Information ===", ConsoleColor.Cyan);
                ConsoleWrite.WriteMessage("[INFO] Status: Running", ConsoleColor.Cyan);
                ConsoleWrite.WriteMessage("[INFO] Active Matches: 0", ConsoleColor.Cyan);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to get match server info: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// 全ウェイトルームを列挙
        /// </summary>
        public static void ListWaitRooms()
        {
            try
            {
                var roomManager = WaitRoomManager.Instance();
                var roomInfo = roomManager.RoomInfo();

                ConsoleWrite.WriteMessage("[INFO] === Wait Rooms ===", ConsoleColor.Cyan);
                ConsoleWrite.WriteMessage($"[INFO] {roomInfo.ToString()}", ConsoleColor.Cyan);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to list rooms: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// 接続中のプレイヤーを列挙
        /// </summary>
        public static void ListPlayers()
        {
            try
            {
                ConsoleWrite.WriteMessage("[INFO] === Connected Players ===", ConsoleColor.Cyan);
                ConsoleWrite.WriteMessage("[INFO] Total: 0", ConsoleColor.Cyan);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to list players: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// サーバー稼働状態のサマリーを表示
        /// </summary>
        public static void ServerStatus()
        {
            try
            {
                var lobby = LobbyServerManager.Instance;
                var management = ManagementServer.Instance;
                var loggedInUserCount = AccountManager.GetInstance().GetLoggedInUserCount();

                ConsoleWrite.WriteMessage("[INFO] === Server Status ===", ConsoleColor.Cyan);
                ConsoleWrite.WriteMessage(
                    $"[INFO] Lobby TCP: {(lobby.IsTcpServerRunning ? "Running" : "Stopped")} Port={lobby.TcpPort?.ToString() ?? "N/A"}",
                    ConsoleColor.White);
                ConsoleWrite.WriteMessage(
                    $"[INFO] Management Sessions: {management.GetConnectedClientCount()}",
                    ConsoleColor.White);
                ConsoleWrite.WriteMessage($"[INFO] Logged-in Users: {loggedInUserCount}", ConsoleColor.White);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Failed to get server status: {ex.Message}", ConsoleColor.Red);
            }
        }
    }


}
