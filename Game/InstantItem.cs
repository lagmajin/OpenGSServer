using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    

    public abstract class InstantItem
    {

        public abstract void  Use();
    }

    public class PowerGranadePack : InstantItem
    {
        public override void Use()
        {
            
        }
    }

    public class FireGranadePack : InstantItem
    {

        public override void Use()
        {
            
        }
    }

    class ClusterGranadePack : InstantItem
    {
        public override void Use()
        {
            
        }
    }

    class MineGranadePack : InstantItem
    {
        public override void Use()
        {
           
        }
    }

}
