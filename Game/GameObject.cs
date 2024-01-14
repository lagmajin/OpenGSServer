using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public enum eGameObjectType
    {
        Grenade,
        Character,
        FieldItem,

    }

    public abstract class AbstractGameObject
    {
        String name;
        float posx = 0;
        float posy = 0;
        String id = Guid.NewGuid().ToString("N");
        bool updated = false;
        int frame = 0;
        DateTime time = DateTime.Now;

        public string Name { get => name; set => name = value; }
        public float Posx { get => posx; set => posx = value; }
        public float Posy { get => posy; set => posy = value; }
        public string Id { get => id; set => id = value; }
        public bool Updated { get => updated; }

        public AbstractGameObject(float x,float y)
        {
            Posx = x;posy = y;

        }
        public virtual void Created()
        {

        }
        public virtual void update()
        {


        }

        public virtual JObject ToJSon()
        {


            return null;
        }

    }



    public class Character : AbstractGameObject
    {

        public Character(float x,float y) : base(x, y)
        {

        }

        
    }

     class FieldItem : AbstractGameObject
    {
        public FieldItem(float x, float y, float time = 0.0f) : base(x, y)
        {

        }
    }

    class NormalGranade : AbstractGameObject
    {

        

 
        public NormalGranade(float x,float y,float time=0.0f):base(x,y)
        {

        }
    }






}
