using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace OpenGSServer
{
    /// <summary>
    /// Options accepted when the server process starts.
    /// Keep these separate from <see cref="CommandParser"/>, which handles commands
    /// entered after the server has started.
    /// </summary>
    public sealed class ServerStartupOptions
    {
        [Option("lobby-port", Default = 60000, HelpText = "TCP port used by the lobby server (1-65535).")]
        public int LobbyPort { get; set; }

        [Option("match-tcp-port", Default = 60001, HelpText = "TCP port used by the match server (1-65535).")]
        public int MatchTcpPort { get; set; }

        [Option("match-udp-port", Default = 63000, HelpText = "UDP port used by the match server (1-65535).")]
        public int MatchUdpPort { get; set; }

        [Option("management-port", Default = 50020, HelpText = "TCP port used by the management server (1-65535).")]
        public int ManagementPort { get; set; }

        [Option("no-console", HelpText = "Run without reading interactive console commands.")]
        public bool NoConsole { get; set; }

        [Option('v', "version", HelpText = "Show version information and exit.")]
        public bool ShowVersion { get; set; }

        public bool TryValidate(out string error)
        {
            foreach (var (name, port) in GetPorts())
            {
                if (port is < 1 or > 65535)
                {
                    error = $"{name} must be between 1 and 65535 (received {port}).";
                    return false;
                }
            }

            var tcpPorts = new HashSet<int>();
            foreach (var (name, port) in new[]
                     {
                         ("--lobby-port", LobbyPort),
                         ("--match-tcp-port", MatchTcpPort),
                         ("--management-port", ManagementPort)
                     })
            {
                if (!tcpPorts.Add(port))
                {
                    error = $"{name} duplicates another TCP listener port ({port}).";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static string BuildHelpText<T>(ParserResult<T> result)
        {
            return HelpText.AutoBuild(result, helpText =>
            {
                helpText.Heading = "OpenGS Server";
                helpText.Copyright = string.Empty;
                helpText.AdditionalNewLineAfterOption = false;
                return HelpText.DefaultParsingErrorsHandler(result, helpText);
            });
        }

        private IEnumerable<(string Name, int Port)> GetPorts()
        {
            yield return ("--lobby-port", LobbyPort);
            yield return ("--match-tcp-port", MatchTcpPort);
            yield return ("--match-udp-port", MatchUdpPort);
            yield return ("--management-port", ManagementPort);
        }
    }
}
