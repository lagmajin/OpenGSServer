﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public enum eResultType
    {
        LoginSucceeful,
        LoginFaild,


    }
    abstract public class AbstractResult
    {
        public static readonly string messageTypeString="MessageType";

        //protected abstract  string Message();
    }
}
