using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public abstract class AbstractFieldItem : AbstractGameObject
    {
        private int msec = 30000;

        public AbstractFieldItem(float x, float y) : base(x, y)
        {

        }

        public override void Update()
        {

        }
        public void PowerUp()
        {

        }



    }


    public class InstantStagePowerUpItem : AbstractFieldItem
    {
        public InstantStagePowerUpItem(float x, float y) : base(x, y)
        {

        }



    }

    public class InstantStageDefenceItem : AbstractFieldItem
    {
        public InstantStageDefenceItem(float x, float y) : base(x, y)
        {

        }

    }


    public class InstantStageGranadeItem : AbstractFieldItem
    {
        public InstantStageGranadeItem(float x, float y) : base(x, y)
        {

        }

    }

    public class InstantFlameThrower : AbstractFieldItem
    {
        public InstantFlameThrower(float x, float y) : base(x, y)
        {

        }
    }


}
