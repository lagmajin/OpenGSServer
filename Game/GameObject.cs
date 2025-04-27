using Newtonsoft.Json.Linq;
using OpenGSCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace OpenGSServer
{
    public enum EGameObjectType
    {
        Grenade,
        Character,
        FieldItem,

    }
    public interface ISyncable
    {
        JObject ToJson();
        bool HasChanged();
        void SaveSyncState();
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

        public string Name { get; set; }
        public float Posx { get; set; }
        public float Posy { get; set; }

        protected float lastPosx, lastPosy;
        public string Id { get; set; }
        public bool Updated { get; }

        public AbstractGameObject(float x,float y)
        {
            Posx = x;posy = y;

        }
        public virtual void OnCreated()
        {

        }

        public virtual void OnDestroy()
        {

        }
        public virtual void Update()
        {


        }

        public void SetPos(float x,float y)
        {

        }

        public virtual bool HasChanged()
        {
            return (Posx != lastPosx || Posy != lastPosy);
        }
        public virtual void Save()
        {

        }

        public virtual JObject ToJSon()
        {


            return null;
        }

    }



    public class PlayerGameObject : AbstractGameObject
    {
        PlayerStatus status;

        public PlayerGameObject(String name,float x,float y) : base(x, y)
        {

        }

        public override JObject ToJSon()
        {
            JObject json = new JObject();


            return json;
        }

    }


    class NormalGranade : AbstractGameObject
    {

        

 
        public NormalGranade(float x,float y,float time=0.0f):base(x,y)
        {

        }

        public override JObject ToJSon()
        {
            JObject json = new JObject();


            return json;

        }
    }






}
