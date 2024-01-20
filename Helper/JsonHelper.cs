
using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public static class JsonHelper
    {
        public static string? GetStringOrNull(this IDictionary<string,JToken> dic,in string name)
        {
            string? result;

            if(dic.TryGetValue(name,out var jToken))
            {

                return jToken.ToString();
            }
          


            return null;
        }

        public static string GetValueString(this IDictionary<string,JToken?> t,String name)
        {
            string result = "";

            return result;
        }

        public static string GetValueOrDefaultString(this IDictionary<string, JToken?> dic,string name,string defalut="")
        {
            string result = "";
            result = dic.TryGetValue(name, out var token) ? token.ToString() : defalut;


            

            return result;
        }

        
        public static int GetValueDefaultInt(this IDictionary<string, JToken?> dic, string name, int defalut)
        {
            //int result = 0;
            
            if(dic.TryGetValue(name,out var token))
            {
                var temp=token.ToString();

                if(Int32.TryParse(temp,out var val))
                {
                    return val;
                }
                else
                {
                    return defalut;
                }

            }
            else
            {
                return defalut;
            }

        }


        public static float GetValueDefaultFloat(this IDictionary<string, JToken?> dic, string name, float defalut)
        {
            //int result = 0;

            if (dic.TryGetValue(name, out var token))
            {
                var temp = token.ToString();

                if (float.TryParse(temp, out var val))
                {
                    return val;
                }
                else
                {
                    return defalut;
                }

            }
            else
            {
                return defalut;
            }

        }


    }

}


