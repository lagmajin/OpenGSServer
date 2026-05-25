using Newtonsoft.Json.Linq;
using System;
using System.Reflection;

namespace OpenGSServer
{
    public class ServerInfoManager
    {
        public string ServerAssemblyVersion { get; private set; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
        public string ServerAssemblyName { get; private set; } = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
        public string ServerTimeZone { get; private set; } = TimeZoneInfo.Local.Id;

        private ServerInfoManager()
        {
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["ServerAssemblyName"] = ServerAssemblyName,
                ["ServerAssemblyVersion"] = ServerAssemblyVersion,
                ["ServerTimeZone"] = ServerTimeZone,
                ["TimestampUtc"] = DateTime.UtcNow.ToString("O")
            };
        }
    }
}
