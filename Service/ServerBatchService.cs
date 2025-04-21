using System;
using System.Collections.Generic;
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
            File.WriteAllText(path, port.ToString());
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
