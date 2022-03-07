using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public abstract class AbstractBullet:AbstractGameObject
    {
        int damage = 0;

        private int angle = 0;
        private int speed = 0;
        private int power = 0;
        private int stoppingPower = 0;

        public int Damage { get => damage; set => damage = value; }
        public int Angle { get => angle; set => angle = value; }
        public int Speed { get => speed; set => speed = value; }
        public int Power { get => power; set => power = value; }
        public int StoppingPower { get => stoppingPower; set => stoppingPower = value; }
    }

    public class Bullet :AbstractBullet
    {



        Bullet(int angle, int speed=1)
        {

        }

    }
}
