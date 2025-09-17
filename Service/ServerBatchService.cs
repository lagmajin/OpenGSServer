using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace OpenGSServer
{
    internal class ServerBatchService
    {
        public void OnStart()
        {
            //string path = Path.Combine(UnityPaths.PersistentDataPath, "local_server.txt");
            //File.WriteAllText(path, port.ToString());
        }

        public void WriteLocalPortToFile(int port)
        {
            string path = Path.Combine(UnityPaths.PersistentDataPath, "local_server.txt");


            try
            {
                File.WriteAllText(path, port.ToString());
            }
            catch (Exception ex)
            {
                // ここでは例外を無視して先に進む
                // 必要ならログだけ出す
                Console.Write("error");
            }
        }

        public void OnStop()
        {
            string path = Path.Combine(UnityPaths.PersistentDataPath, "local_server.txt");
            if (File.Exists(path))
            {

                File.Delete(path);
            }


        }
     }
}
