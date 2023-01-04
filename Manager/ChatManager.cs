using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public class ChatManager
    {
        private List<Chat> log=new();

        public List<Chat> Log { get => log;}

        public ChatManager()
        {


        }

        public void AddChat(Chat chat)
        {
            Log.Add(chat);
        }

        public void Clear()
        {
            Log.Clear();
        }

        public void NewChat()
        {

        }

        public void AllChat()
        {

        }


    }
}
