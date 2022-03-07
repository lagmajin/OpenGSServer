using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public static class JsonUtility
    {
        public static string? TakeValueToString(in JObject json,in string name)
        {
            string? str=null;

            if(json.TryGetValue(name,out var value))
            {
               str= value.ToString();

            }



            return str;
        }

    }
}
