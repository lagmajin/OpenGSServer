using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public abstract class AbstractBullet : AbstractGameObject
    {
        public int Damage { get; private set; } = 0;
        public int Angle { get; private set; } = 0;
        public int Speed { get; private set; } = 0;
        public int Power { get; private set; } = 0;
        public int StoppingPower { get; set; } = 0;

        public AbstractBullet(float x,float y):base(x,y)
        {

        }

    }

    public class BulletObject : AbstractBullet
    {
        public BulletObject(float x, float y) : base(x, y)
        {

        }

    }
}
