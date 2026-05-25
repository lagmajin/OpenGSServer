using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class DefaultServerSetting
    {
        public int DefaultDMKillCondition { get; set; } = 20;
        public int DefaultTDMKillCondition { get; set; } = 20;
        public int DefaultMissionCapacity { get; set; } = 4;

        public DefaultServerSetting()
        {
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["DM"] = new JObject
                {
                    ["DefaultConditionKill"] = DefaultDMKillCondition,
                    ["MatchDefaultTime"] = 300
                },
                ["TDM"] = new JObject
                {
                    ["DefaultConditionKill"] = DefaultTDMKillCondition,
                    ["MatchDefaultTime"] = 300
                },
                ["Mission"] = new JObject
                {
                    ["DefaultCapacity"] = DefaultMissionCapacity
                }
            };
        }
    }
}
