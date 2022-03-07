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
    public class AbstractDBRankingData:IAbstractDBRankingData
    {

    }

    public class DBTDMKillRankingData
    {
        public int rank { get; }

        public DBTDMKillRankingData()
        {

        }

    }

    public class DBTSuvKillRankingData
    {
        public int rank { get; }

        public DBTSuvKillRankingData()
        {

        }

    }
    public class DBCTFReturnRankingData
    {
        public int rank { get; }
    }
}
