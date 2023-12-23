using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public static class ServerSettingManager
    {
        public static string defaultServerSettingName = "DefaultServerSetting.json";
        public static string defaultServerSettingPath = "";

        public static void LoadSettingsFromFile()
        {
            var json =new  JObject();

            using(var sr=new StreamReader(defaultServerSettingPath,Encoding.UTF8))
            {
                try
                {
                    json = JObject.Parse(sr.ReadToEnd());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);


                }
            }

        }

        public static void SaveSettingsToFile()
        {
            var json = new DefaultServerSetting();

            using (var sw = new StreamWriter(defaultServerSettingPath, false, Encoding.UTF8))
            {
                sw.Write(json.ToString());
            }



        }

        public static void ResetServerSettings()
        {

        }


        
    }
}
