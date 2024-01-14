using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public abstract class AbstractStageItem : AbstractGameObject
    {
        private int msec = 30000;

        public AbstractStageItem(float x, float y) : base(x, y)
        {

        }
    }


    public class InstantStagePowerItem : AbstractStageItem
    {
        public InstantStagePowerItem(float x, float y) : base(x, y)
        {

        }

    }

    public class InstantStageDefenceItem : AbstractStageItem
    {
        public InstantStageDefenceItem(float x, float y) : base(x, y)
        {

        }

    }


    public class InstantStageGranadeItem : AbstractStageItem
    {
        public InstantStageGranadeItem(float x, float y) : base(x, y)
        {

        }

    }

    public class InstantFlameThrower : AbstractStageItem
    {
        public InstantFlameThrower(float x, float y) : base(x, y)
        {

        }
    }


}
