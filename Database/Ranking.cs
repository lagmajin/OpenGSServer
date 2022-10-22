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
        public string DisplayName { get; set; } = "";

        public int Kill { get; set; } = 0;


        public int Rank { get; } = 0;

        public DBTDMKillRankingData(int kill=0)
        {
            Kill = kill;


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
