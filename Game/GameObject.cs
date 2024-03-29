﻿using Newtonsoft.Json.Linq;
using OpenGSCore;
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

        public string Name { get; set; }
        public float Posx { get; set; }
        public float Posy { get; set; }
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

        
    }


    class NormalGranade : AbstractGameObject
    {

        

 
        public NormalGranade(float x,float y,float time=0.0f):base(x,y)
        {

        }
    }






}
