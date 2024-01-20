using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class ServerInfoManager
    {
        private string serverAssemblyVersion;

        private ServerInfoManager()
        {
            var asm=System.Reflection.Assembly.GetExecutingAssembly();

            //asm.get

            Console.WriteLine(asm.GetName());


        }

        public JObject ToJson()
        {
            var result=new JObject();


            return result;
        }


    }
}

