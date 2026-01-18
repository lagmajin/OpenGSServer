using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Utility;

namespace OpenGSServer
{
    class GameSceneUpdateInfo
    {
        public GameSceneUpdateInfo(Time beforeTime,Time currentTime)
        {
            
        }
    }

    public class GameScene
    {
        private List<AbstractGameObject> objects = new();

        void AddBullet(BulletGameObject bullet)
        {
            objects.Add(bullet);
        }

        void AddPlayerCharacter(PlayerGameObject character)
        {
            //objects.Add(character);
        }

        void AddFieldItem(AbstractFieldItem item)
        {
            //objects.Add(item);
        }

        void AddGrenade()
        {

        }

        void AddFlag()
        {


        }

        public void UpdateFrame()
        {
            foreach (var obj in objects)
            {

                obj.Update();


            }
        }



        public JObject ToJson()
        {
            var json = new JObject();

            foreach (var item in objects)
            {
                var temp = new JObject();

                temp["Name"] = item.Name;
                temp["ID"] = item.Id;
                temp["PosX"] = item.Posx;
                temp["PosY"] = item.Posy;

                json.Add(temp);

            }

            return json;
        }

        public JObject AllPlayerDataToJson()
        {
            var json = new JObject();

            return json;
        }



    }


}
