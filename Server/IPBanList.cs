using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenGSServer
{
    public class IPBanList
    {
        private List<string> ipList_=new List<string>();


        public void LoadFromFile(in string fileFullPath)
        {
            using (var sr = new StreamReader(fileFullPath, Encoding.GetEncoding("Shift_JIS")))
            {

            }
        }

        public void WriteToFile(in string fileFullPath)
        {
            using (var sr = new StreamWriter(fileFullPath))
            {

            }
        
        }
        public void Add(in string ip)
        {
            ipList_.Add(ip);

        }

        public void AddList(in List<string> ip)
        {

        }

        public void RemoveAll()
        {
            ipList_.Clear();

        }

        public List<string> IpList()
        {

            return ipList_;
        }


    }
}
