using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    internal class ChatEventDelegate
    {

        public static void ProcessOneOnOneChat(in ClientSession session, in IDictionary<string, JToken> dic)
        {

            if (!dic.ContainsKey("AccountID"))
            {


                return;
            }

            if (dic.ContainsKey("TargetAccountID"))
            {
                return;
            }

            if (dic.ContainsKey("Message"))
            {

                return;
            }

        }

        public static void ProcessLobbyChatRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            if (!dic.ContainsKey("AccountID"))
            {


                return;
            }

            if (dic.ContainsKey("Message"))
            {

                return;
            }

            //LobbyServerManagerV2

        }

        public static void ProcessWaitRoomChatRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            if (!dic.ContainsKey("AccountID"))
            {


                return;
            }

            if (dic.ContainsKey("Message"))
            {

                return;
            }



        }

        public static void ProcessMatchRoomChatRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            if (!dic.ContainsKey("AccountID"))
            {


                return;
            }

            if (dic.ContainsKey("Message"))
            {

                return;
            }

        }

    }
}
