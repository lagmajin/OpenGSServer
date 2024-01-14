using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace OpenGSServer
{


    public class GameScene
    {
        private List<AbstractGameObject> objects = new();

        void AddBullet(BulletObject bullet)
        {
            objects.Add(bullet);
        }

        void AddCharacter(Character character)
        {
            objects.Add(character);
        }

        void AddFieldItem(FieldItem item)
        {
            objects.Add(item);
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

                obj.update();


            }
        }



        JObject ToJson()
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

    }


}
