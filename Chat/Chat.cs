using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public class Chat
    {
        private string id=Guid.NewGuid().ToString("N");
        private string say;

        public string Id { get => id; set => id = value; }
        public string Say { get => say; set => say = value; }

        public string Color { get => say; set => say = value; }

        public string TimeStamp{ get => say; set => say = value; }

        public Chat(in string say)
        {
            

        }

    }
}
