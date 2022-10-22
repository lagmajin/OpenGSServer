using System;





namespace OpenGSServer
{


    public class PowerGrenade : AbstractGrenade
    {

        public PowerGrenade()
        {


        }

        public override void Hit()
        {
            throw new NotImplementedException();
        }
    }

    public class ClusterGrenade : AbstractGrenade
    {
        int child = 3;

        public ClusterGrenade()
        {


        }

        public int Child { get => child; set => child = value; }

        public override void Hit()
        {
            throw new NotImplementedException();
        }
    }

    public class ChildClusterGrenade : AbstractGrenade
    {
        public ChildClusterGrenade()
        {


        }

        public override void Hit()
        {
            //throw new NotImplementedException();
        }
    }
}
