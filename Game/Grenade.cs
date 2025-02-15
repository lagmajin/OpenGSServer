using System;





namespace OpenGSServer
{


    public class PowerGrenade : AbstractGrenade
    {

        public PowerGrenade(float x, float y):base(x,y)
        {


        }

        public override void Hit()
        {
            //throw new NotImplementedException();
        }
    }

    public class ClusterGrenade : AbstractGrenade
    {
        int child = 3;

        public ClusterGrenade(float x, float y):base(x, y)
        {


        }

        public int Child { get => child; set => child = value; }

        public override void Hit()
        {
            


        }
    }

    public class ChildClusterGrenade : AbstractGrenade
    {
        public ChildClusterGrenade(float x, float y) : base(x, y)
        {


        }

        public override void Hit()
        {
            //throw new NotImplementedException();
        }
    }
}
