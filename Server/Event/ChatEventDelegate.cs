using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    internal class ChatEventDelegate
    {

        public static void OneOnOneChat(in ClientSession session, in IDictionary<string, JToken> dic)
        {

            if (dic.TryGetValue("", out var chatMessageToken))
            {
                var chatMessage = chatMessageToken.ToString();



            }

        }

        public static void LobbyChatRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            if (dic.TryGetValue("ChatMessage", out var chatMessageToken))
            {
                var chatMessage = chatMessageToken.ToString();



            }



            if (dic.TryGetValue("LocalTimeStamp", out var timeStampToken))
            {
                var timeStamp = "";



            }
        }

        public static void WaitRoomChatRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {


        }

        public static void MatchRoomChatRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {

        }

    }
}
