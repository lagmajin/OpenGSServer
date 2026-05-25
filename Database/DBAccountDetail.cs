using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class DBAccountDetail
    {
        public string AccountId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Level { get; set; } = 1;
        public long Exp { get; set; } = 0;
        public int TotalKill { get; set; } = 0;
        public int Death { get; set; } = 0;

        public JObject ToJson()
        {
            return new JObject
            {
                ["AccountId"] = AccountId,
                ["DisplayName"] = DisplayName,
                ["Level"] = Level,
                ["Exp"] = Exp,
                ["TotalKill"] = TotalKill,
                ["Death"] = Death
            };
        }
    }
}
