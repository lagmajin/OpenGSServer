using LiteDB;
using System;

using OpenGSCore;

namespace OpenGSServer
{

    struct DeathMatchKillRanking
    {
        public string AccountID { get; set; }
        public string DisplayName { get; set; }
        public DeathMatchKillRanking(in string AccountID,in string DisplayName,int Kill)
        {
            this.AccountID = AccountID;

            this.DisplayName = DisplayName;
        }

    }

    public class RankingDatabase:IDisposable
    {
        private LiteDatabase rankingDB;

        public static string rankingDBfileName = "ranking.db";

        private string connectionString = "";
        public void ConnectRankingDB()
        {
            rankingDB = new LiteDatabase(rankingDBfileName);

           
        }

        public void AddDMKillRanking(in string AccountID,in string DisplayName,int kill)
        {
            if(rankingDB==null)
            {
                ConnectRankingDB();

            }

            //rankingDB.GetCollection<>

        }

        

        public void AddTDMKillRanking(in string AccountID, in string DisplayName,int kill)
        {
            if (rankingDB == null)
            {

                ConnectRankingDB();
            }


            var col = rankingDB.GetCollection<DBTDMKillRankingData>("");
        }

        public void AddSuvKillRanking(in string AccountID, in string DisplayName)
        {
            if (rankingDB == null)
            {

                ConnectRankingDB();
            }

            var col = rankingDB.GetCollection<DBTDMKillRankingData>("");
        }

        public void AddTSuvKillRanking(in string AccountID, in string DisplayName)
        {
            if (rankingDB == null)
            {
                ConnectRankingDB();

            }
        }

        public void Dispose()
        {
            rankingDB.Dispose();

            //Dispose(true); 

        }
    }
}
