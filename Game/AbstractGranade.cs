


namespace OpenGSServer
{
    public enum eGranadeType : int
    {
        Normal = 0,
        Power,
        Cluster,
        Fire,
        Magnet,
        Unknown
    }

    public class GranadeType
    {
        eGranadeType type_ = eGranadeType.Unknown;

        GranadeType(eGranadeType type=eGranadeType.Unknown)
        {
            type = type_;



        }

        public string ToString()
        {
            string result=new string("");

            switch(type_)
            {
                case eGranadeType.Normal:

                    result = "Normal";

                    break;

                case eGranadeType.Fire:

                    result = "Fire";

                    break;

                case eGranadeType.Power:
                    result = "Power";
                    break;


                case eGranadeType.Cluster:
                    result = "Cluster";
                    break;

                default:
                    result = "Unknown";

                    break;

            }

            return result;
        }

        public bool FromStr(in string str)
        {
            switch(str)
            {
                case "Normal":
                    type_ = eGranadeType.Normal;
                    break;


                case "Fire":
                    type_ = eGranadeType.Fire;
                    break;

                case "Power":
                    type_ = eGranadeType.Power;
                    break;

                case "Cluster":
                    type_ = eGranadeType.Cluster;
                    break;

                default:
                    type_ = eGranadeType.Unknown;
                    break;

            }



            return false;
        }

        public override bool Equals(object obj)
        {
            return obj is GranadeType type &&
                   type_ == type.type_;
        }

        public static bool operator==(GranadeType a,GranadeType b)
        {

            return true;
        }

        public static bool operator !=(GranadeType a, GranadeType b)
        {

            return !(a == b);
        }

    }

    public abstract class AbstractGrenade :AbstractGameObject
    {
        

        int stoppingPower = 0;
        int angle = 0;
        int speed = 0;

        int damage = 0;
        int explosionDamage = 0;

        public int StoppingPower { get => stoppingPower; set => stoppingPower = value; }
        public int Angle { get => angle; set => angle = value; }
        public int Speed { get => speed; set => speed = value; }
        public int ExplosionDamage { get => explosionDamage; set => explosionDamage = value; }
        public int Damage { get => damage; set => damage = value; }

        protected AbstractGrenade()
        {

        }

        abstract public void Hit();
    }
}
