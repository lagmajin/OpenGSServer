using System;
using System.Runtime.InteropServices;

namespace OpenGSServer.Platform.Windows
{
    class WindowsAPI
    {
        [DllImport("kernel32.dll")]
        extern static int QueryPerformanceCounter(ref long x);

        [DllImport("kernel32.dll")]
        extern static int QueryPerformanceFrequency(ref long x);

        public static long PerformanceCounter()
        {
            long result = 0;
            QueryPerformanceCounter(ref result);
            return result;
        }

        public static long PerformanceFrequency()
        {
            long result = 0;
            QueryPerformanceFrequency(ref result);
            return result;
        }
    }
}
