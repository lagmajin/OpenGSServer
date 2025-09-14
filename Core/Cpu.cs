using System;
using System.Diagnostics;
using System.Threading;

using System.Runtime.InteropServices;

namespace OpenGSServer
{
    static class Cpu
    {
        public static string ArchitectureName()
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
