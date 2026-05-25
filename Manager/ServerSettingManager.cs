using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;

namespace OpenGSServer
{
    public static class ServerSettingManager
    {
        public static string defaultServerSettingName = "DefaultServerSetting.json";
        public static string defaultServerSettingPath = "";

        public static DefaultServerSetting CurrentSettings { get; private set; } = new();

        public static void LoadSettingsFromFile()
        {
            if (string.IsNullOrWhiteSpace(defaultServerSettingPath) || !File.Exists(defaultServerSettingPath))
            {
                CurrentSettings = new DefaultServerSetting();
                return;
            }

            try
            {
                var json = JObject.Parse(File.ReadAllText(defaultServerSettingPath, Encoding.UTF8));
                CurrentSettings = new DefaultServerSetting
                {
                    DefaultDMKillCondition = json["DM"]?["DefaultConditionKill"]?.Value<int>() ?? CurrentSettings.DefaultDMKillCondition,
                    DefaultTDMKillCondition = json["TDM"]?["DefaultConditionKill"]?.Value<int>() ?? CurrentSettings.DefaultTDMKillCondition,
                    DefaultMissionCapacity = json["Mission"]?["DefaultCapacity"]?.Value<int>() ?? CurrentSettings.DefaultMissionCapacity
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                CurrentSettings = new DefaultServerSetting();
            }
        }

        public static void SaveSettingsToFile()
        {
            if (string.IsNullOrWhiteSpace(defaultServerSettingPath))
            {
                defaultServerSettingPath = defaultServerSettingName;
            }

            File.WriteAllText(defaultServerSettingPath, CurrentSettings.ToJson().ToString(), Encoding.UTF8);
        }

        public static void ResetServerSettings()
        {
            CurrentSettings = new DefaultServerSetting();
        }
    }
}
