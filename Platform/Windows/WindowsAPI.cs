using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenGSServer.Platform.Windows
{
    class WindowsAPI
    {
        [DllImport("kernel32.dll")]
        extern static int QueryPerformanceCounter(ref long x);

        [DllImport("kernel32.dll")]
        extern static int QueryPerformanceFrequency(ref long x);


    }
}
