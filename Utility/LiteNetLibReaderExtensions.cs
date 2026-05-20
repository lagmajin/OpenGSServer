using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLib.Utils
{
    public static class LiteNetLibReaderExtensions
    {
        public static void Recycle(this NetDataReader reader)
        {
            // LiteNetLib 0.9.5 系では NetDataReader に Recycle/Reset がないため no-op
        }
    }
}
