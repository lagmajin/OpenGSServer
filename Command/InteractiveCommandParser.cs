using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenGSServer
{
    /// <summary>Parses and dispatches commands entered in the running server console.</summary>
    public sealed class InteractiveCommandParser
    {
        private readonly Dictionary<string, CommandDefinition> _commands;
        private readonly IReadOnlyList<CommandDefinition> _definitions;

        public InteractiveCommandParser()
        {
            _definitions = new[]
            {
                Define("addplayer", "<id> <password> <displayName>", "Create a player account.", 3, 3, a => CommandExecutor.CreatePlayer(a[0], a[1], a[2])),
                Define("addguild", "<guildName>", "Create a guild.", 1, 1, a => CommandExecutor.CreateGuild(a[0])),
                Define("guildlist", "", "List all guilds.", 0, 0, _ => CommandExecutor.ListGuilds()),
                Define("guildremove", "<guildName>", "Remove a guild.", 1, 1, a => CommandExecutor.RemoveGuild(a[0])),
                Define("guildaddmember", "<guildName> <memberId> [role]", "Add a guild member.", 2, 3, a => CommandExecutor.AddGuildMember(a[0], a[1], Optional(a, 2, "Member"))),
                Define("guildjoin", "<guildName> <memberId> [role]", "Join a guild.", 2, 3, a => CommandExecutor.JoinGuild(a[0], a[1], Optional(a, 2, "Member"))),
                Define("guildleave", "<guildName> <memberId>", "Remove a member from a guild.", 2, 2, a => CommandExecutor.LeaveGuild(a[0], a[1])),
                Define("guildinvite", "<guildName> <memberId> [inviterId]", "Invite a guild member.", 2, 3, a => CommandExecutor.InviteGuildMember(a[0], a[1], Optional(a, 2, "System"))),
                Define("guildkick", "<guildName> <memberId> [kickerId]", "Kick a guild member.", 2, 3, a => CommandExecutor.KickGuildMember(a[0], a[1], Optional(a, 2, "System"))),
                Define("guildremovemember", "<guildName> <memberId>", "Remove a guild member.", 2, 2, a => CommandExecutor.RemoveGuildMember(a[0], a[1])),
                Define("guildrole", "<guildName> <memberId> <role>", "Change a guild member role.", 3, 3, a => CommandExecutor.SetGuildMemberRole(a[0], a[1], a[2])),
                Define("guildexp", "<guildName> <exp>", "Add guild experience.", 2, 2, AddGuildExperience),
                Define("guildchat", "<guildName> <senderId> <message...>", "Broadcast guild chat.", 3, null, a => CommandExecutor.BroadcastGuildChat(a[0], a[1], string.Join(" ", a.Skip(2)))),
                Define("addwaitroom", "<roomName>", "Create a wait room.", 1, 1, a => CommandExecutor.CreateWaitRoom(a[0])),
                Define("creatematch", "<roomName>", "Create a match room.", 1, 1, a => CommandExecutor.CreateMatchRoom(a[0])),
                Define("addplayertomatch", "<matchId> <playerId>", "Add a player to a match.", 2, 2, a => CommandExecutor.AddPlayerToMatch(a[0], a[1])),
                Define("startmatch", "<matchId>", "Start a match.", 1, 1, a => CommandExecutor.StartMatch(a[0])),
                Define("playerinfo", "<playerId>", "Show player information.", 1, 1, a => CommandExecutor.PlayerInfo(a[0])),
                Define("guildinfo", "<guildName>", "Show guild information.", 1, 1, a => CommandExecutor.GuildInfo(a[0])),
                Define("lobbyinfo", "", "Show lobby information.", 0, 0, _ => CommandExecutor.LobbyInfo()),
                Define("matchserverinfo", "", "Show match server information.", 0, 0, _ => CommandExecutor.MatchServerInfo()),
                Define("listrooms", "", "List wait rooms.", 0, 0, _ => CommandExecutor.ListWaitRooms()),
                Define("listmatches", "", "List match rooms.", 0, 0, _ => CommandExecutor.ListMatches()),
                Define("listplayers", "", "List connected players.", 0, 0, _ => CommandExecutor.ListPlayers()),
                Define("banip", "<ipAddress>", "Ban an IP address.", 1, 1, a => CommandExecutor.BanIp(a[0])),
                Define("unbanip", "<ipAddress>", "Remove an IP ban.", 1, 1, a => CommandExecutor.UnbanIp(a[0])),
                Define("listban", "", "List banned IP addresses.", 0, 0, _ => CommandExecutor.ListBannedIps()),
                Define("status", "", "Show server status.", 0, 0, _ => CommandExecutor.ServerStatus())
            };

            _commands = _definitions.ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);
        }

        public void Execute(string input)
        {
            if (!TryTokenize(input, out var tokens, out var error) || tokens.Count == 0)
            {
                if (!string.IsNullOrEmpty(error)) ConsoleWrite.WriteMessage($"[ERR] {error}", ConsoleColor.Red);
                return;
            }

            var commandName = tokens[0];
            if (commandName == "?" || commandName.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                ShowHelp(tokens.Skip(1).FirstOrDefault());
                return;
            }

            if (!_commands.TryGetValue(commandName, out var command))
            {
                var suggestion = FindSuggestion(commandName);
                ConsoleWrite.WriteMessage($"[ERR] Unknown command: {commandName}.{suggestion}", ConsoleColor.Red);
                return;
            }

            var arguments = tokens.Skip(1).ToArray();
            if (arguments.Length < command.MinimumArguments || (command.MaximumArguments.HasValue && arguments.Length > command.MaximumArguments.Value))
            {
                ConsoleWrite.WriteMessage($"[ERR] Usage: {command.Usage}", ConsoleColor.Red);
                return;
            }

            command.Execute(arguments);
        }

        private void ShowHelp(string? commandName)
        {
            if (!string.IsNullOrWhiteSpace(commandName))
            {
                if (_commands.TryGetValue(commandName, out var command))
                    ConsoleWrite.WriteMessage($"{command.Usage} — {command.Description}", ConsoleColor.White);
                else
                    ConsoleWrite.WriteMessage($"[ERR] Unknown command: {commandName}", ConsoleColor.Red);
                return;
            }

            ConsoleWrite.WriteMessage("=== OpenGS Server Console Commands ===", ConsoleColor.Cyan);
            ConsoleWrite.WriteMessage("Use quotes for values containing spaces; escape quotes with a backslash. Type: help <command>", ConsoleColor.Gray);
            foreach (var command in _definitions.OrderBy(command => command.Name))
                ConsoleWrite.WriteMessage($"{command.Usage,-52} {command.Description}", ConsoleColor.White);
            ConsoleWrite.WriteMessage("help [command], ? [command]                              Show command help.", ConsoleColor.White);
        }

        private string FindSuggestion(string input)
        {
            var closest = _definitions.OrderBy(command => EditDistance(input, command.Name)).FirstOrDefault();
            return closest != null && EditDistance(input, closest.Name) <= 3 ? $" Did you mean '{closest.Name}'?" : string.Empty;
        }

        private static CommandDefinition Define(string name, string arguments, string description, int minimum, int? maximum, Action<IReadOnlyList<string>> execute) =>
            new(name, string.IsNullOrEmpty(arguments) ? name : $"{name} {arguments}", description, minimum, maximum, execute);

        private static string Optional(IReadOnlyList<string> arguments, int index, string fallback) => arguments.Count > index ? arguments[index] : fallback;

        private static void AddGuildExperience(IReadOnlyList<string> arguments)
        {
            if (!long.TryParse(arguments[1], out var experience))
            {
                ConsoleWrite.WriteMessage("[ERR] Usage: guildexp <guildName> <exp> (exp must be an integer)", ConsoleColor.Red);
                return;
            }
            CommandExecutor.AddGuildExp(arguments[0], experience);
        }

        internal static bool TryTokenize(string input, out List<string> tokens, out string error)
        {
            tokens = new List<string>(); error = string.Empty;
            var current = new StringBuilder(); char quote = '\0'; var tokenStarted = false;
            for (var index = 0; index < input.Length; index++)
            {
                var character = input[index];
                if (character == '\\' && index + 1 < input.Length)
                {
                    var next = input[++index];
                    current.Append(next is 'n' ? '\n' : next is 't' ? '\t' : next);
                    tokenStarted = true;
                    continue;
                }
                if (quote != '\0')
                {
                    if (character == quote) quote = '\0'; else current.Append(character);
                    continue;
                }
                if (character is '\'' or '"') { quote = character; tokenStarted = true; continue; }
                if (char.IsWhiteSpace(character)) { if (tokenStarted) { tokens.Add(current.ToString()); current.Clear(); tokenStarted = false; } continue; }
                current.Append(character);
                tokenStarted = true;
            }
            if (quote != '\0') { error = "Unterminated quoted argument."; return false; }
            if (tokenStarted) tokens.Add(current.ToString());
            return true;
        }

        private static int EditDistance(string left, string right)
        {
            var previous = Enumerable.Range(0, right.Length + 1).ToArray();
            for (var i = 1; i <= left.Length; i++)
            {
                var current = new int[right.Length + 1]; current[0] = i;
                for (var j = 1; j <= right.Length; j++) current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (char.ToLowerInvariant(left[i - 1]) == char.ToLowerInvariant(right[j - 1]) ? 0 : 1));
                previous = current;
            }
            return previous[right.Length];
        }

        private sealed record CommandDefinition(string Name, string Usage, string Description, int MinimumArguments, int? MaximumArguments, Action<IReadOnlyList<string>> Execute);
    }
}
