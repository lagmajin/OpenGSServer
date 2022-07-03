using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public interface IAbstractDBRankingData
    {

    }
    public class AbstractDBRankingData : IAbstractDBRankingData
    {

    }

    public class DBTDMKillRankingData
    {
        public int Kill { get; set; }


        public int Rank { get; }

        public DBTDMKillRankingData()
        {

        }

    }

    public class DBTSuvKillRankingData
    {
        public int Kill { get; set; }
        public int Rank { get; }

        public DBTSuvKillRankingData()
        {

        }

    }
    public class DBCTFReturnRankingData
    {
        public int Kill { get; set; }

        public int Rank { get; }
    }
}
