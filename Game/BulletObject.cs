using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{


    public abstract class AbstractBullet : AbstractGameObject
    {
        public float Damage { get; private set; } = 0;
        public float Angle { get; private set; } = 0;
        public float Speed { get; private set; } = 0;
        public float StoppingPower { get; set; } = 0;
        //public int Power { get; private set; } = 0;
        public string ownerId { get; set; }

        public AbstractBullet(float x,float y,float speed,float damage,float angle,float stoppingPower):base(x,y)
        {
            Posx = x;
            Posy = y;
            Speed = speed;
            StoppingPower = stoppingPower; 
            Damage = damage;


        }

    }

    public class BulletGameObject : AbstractBullet
    {
        public BulletGameObject(float x, float y,float speed,float damage, float angle,float stoppingPower) : base(x, y,speed,damage,angle,stoppingPower)
        {

        }

    }

    
}
