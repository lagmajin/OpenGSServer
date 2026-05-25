using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public class ServerInfoResult : AbstractResult
    {
        public string ServerName { get; set; } = Environment.MachineName;
        public string AssemblyName { get; set; } = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
        public string AssemblyVersion { get; set; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
        public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
        public string TimeZoneDisplayName { get; set; } = TimeZoneInfo.Local.DisplayName;
        public string TimestampUtc { get; set; } = DateTime.UtcNow.ToString("O");

        public ServerInfoResult()
        {
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["MessageType"] = ServerMessageTypes.ServerInfo,
                ["ServerName"] = ServerName,
                ["AssemblyName"] = AssemblyName,
                ["AssemblyVersion"] = AssemblyVersion,
                ["TimeZoneId"] = TimeZoneId,
                ["TimeZoneDisplayName"] = TimeZoneDisplayName,
                ["TimestampUtc"] = TimestampUtc
            };
        }
    }
}
