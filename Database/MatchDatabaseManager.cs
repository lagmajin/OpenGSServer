using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;





namespace OpenGSServer
{
    public class MatchDatabaseManager : IDisposable
    {
        private MatchDatabaseManager instance;

        private LiteDatabase db;

        private string matchDatabaseFilename = "match.db";
        private MatchDatabaseManager()
        {

        }

        ~MatchDatabaseManager()
        {
            Dispose(false);
        }

        public void Connect()
        {
            if(db==null)
            {
                db = new LiteDatabase(matchDatabaseFilename);
            }


        }

        public void Disconnect()
        {

        }

        public void AddDMMatchDatabase(in DBMatchDeathMatchDatabaseData data)
        {
            var col= db.GetCollection<DBMatchDeathMatchDatabaseData>("DBDeathMatch");


            //col.Insert(data);

        }

        public void AddSuvMatchDatabase(in DSuvMatchDataData data)
        {
            var col=db.GetCollection<DSuvMatchDataData>("DBSurvivalMatch");

        }

        public void AddTSuvMatchDatabase(in DBTeamDeathMatchData data)
        {
            var col = db.GetCollection<DBTeamDeathMatchData>("DBTeamDeathMatch");


        }

        public void AddCTFMatchDatabase()
        {

        }

        public void HourUpdate()
        {

        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {

                Dispose(true);
                GC.SuppressFinalize(this);
            }


        }

        public void Dispose()
        {
            //throw new NotImplementedException();

            db?.Dispose();
        }
    }
}

