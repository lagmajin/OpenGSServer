using OpenGSCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public static class MakeGameObject
    {
        public static AbstractGameObject MakeFieldItem(float x,float y)
        {

            //var result = ;

            return null;
        }

        public static AbstractBullet MakeBullet(in Guid id, EBulletType type)
        {
            AbstractBullet result = null;

            switch (type)
            {

                case EBulletType.Normal:

                    break;
                case EBulletType.Poison:
                    break;

                    break;
                    //case 
            }


            return result;
        }


    }

}


    

