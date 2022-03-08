
using LiteDB;




namespace OpenGSServer
{
    public class GuildDatabaseManager:IAbstractDatabaseManager
    {
        private LiteDatabase db;

        static GuildDatabaseManager instance = new GuildDatabaseManager();


        public void Connect()
        {

        }


        public void Disconnect()
        {

        }
        

    }



}
