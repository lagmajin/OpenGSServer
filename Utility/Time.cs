using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer.Utility
{
    class Time
    {
        public static int TotalSec(int h, int m, int sec)
        {


            return h * 3600 + m * 60 + sec;
        }


    }


    public class Ping
    {
        public static int CalcPing(int m1, int m2)
        {
            return Math.Abs(m1 - m2);
        }

    }
}
