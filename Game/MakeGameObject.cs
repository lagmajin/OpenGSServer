using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    public class MakeGameObject
    {
        public static AbstractGameObject CreateFieldItem()
        {
            var result = new FieldItem();

            return result;
        }

    }
}
