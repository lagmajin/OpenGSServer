using System;
using System.Diagnostics;
using System.Threading;



namespace OpenGSServer
{
    static class Cpu
    {
        public static string ArchtectureName()
        {
            string result= System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();


            return result;
        }
 
        public static string CpuCoreCount()
        {
            return "";
        }

        public static string CpuVender()
        {
            return "";
        }

    }
}
