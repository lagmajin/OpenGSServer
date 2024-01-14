using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public static class MakeGameObject
    {
        public static AbstractGameObject CreateFieldItem(float x,float y)
        {

            var result = new FieldItem(x,y);

            return result;
        }

        public static BulletGameObject CreateBulletGameObject(float x,float y)
        {


            return null;
        }

    }


    
}
