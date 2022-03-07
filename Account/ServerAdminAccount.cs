using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;




namespace OpenGSServer
{
    public class ServerAdminAccount
    {
        private string id;
        private string pass;

        public string Id { get => id; set => id = value; }
        public string Pass { get => pass; set => pass = value; }

        public static string encrypt()
        {
            var am = new AesManaged();

            am.KeySize = 256;

            am.BlockSize = 128;


            am.Dispose();

            return "";
        }

        public ServerAdminAccount(in string id,in string pass)
        {


        }


    }



}
