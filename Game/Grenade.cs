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

    class ClusterGranade : AbstractGrenade
    {
        int child = 3;

        public ClusterGranade()
        {


        }

        public int Child { get => child; set => child = value; }

        public override void Hit()
        {
            throw new NotImplementedException();
        }
    }

    class ChildClusterGrenade : AbstractGrenade
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
