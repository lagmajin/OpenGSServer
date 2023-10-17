using System;
using System.Collections.Generic;


namespace OpenGSServer
{
    public static class Template
    {
        //ランダムに部屋名を返すクラス
        public static string RandomRoomName()
        {
            var list = new List<string>
            {
                "One Shot One Kill",
                "Live!Live!Live!"
            };


            return list[new Random().Next(0, list.Count)];

        }

    }
}
