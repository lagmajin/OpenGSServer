using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class DefaultServerSetting
    {
        public int DefaultDMKillCondition { get; set; }
        public int DefaultTDMKillCondition { get; set; }

       public DefaultServerSetting()
        {

        }


        public JObject ToJson()
        {
            var result = new JObject();
            result["DM"]["DefaultConditionKill"] = 20;
            result["DM"]["MatchDefaultTime"] = 300;

            result["TDM"]["DefaultConditionKill"] = 20;
            
            return result;
        }


    }
}
