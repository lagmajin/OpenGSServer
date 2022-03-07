using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public abstract class AbstractStageItem : AbstractGameObject
    {
        private int msec = 30000;

        public AbstractStageItem()
        {

        }
    }


    public class InstantStagePowerItem : AbstractStageItem
    {


    }

    public class InstantStageDefenceItem : AbstractStageItem
    {


    }


    public class InstantStageGranadeItem : AbstractStageItem
    { 


    }

    public class InstantFlameThrower : AbstractStageItem
    {

    }


}
