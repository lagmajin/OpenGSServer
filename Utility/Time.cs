using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer.Utility
{
    class Time
    {
        static public int totalSec(int h,int m,int sec)
        {


            return h * 3600 + m * 60 + sec;
        }


    }
}
