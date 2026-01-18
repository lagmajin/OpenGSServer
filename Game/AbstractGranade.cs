




using Newtonsoft.Json.Linq;
using OpenGSCore;

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

    /// <summary>
    /// 手榴弾の種類を管理するクラス
    /// </summary>
    public class GranadeType
    {
        private eGranadeType type_ = eGranadeType.Unknown;

        public eGranadeType Type { get => type_; set => type_ = value; }

        public GranadeType()
        {
            type_ = eGranadeType.Unknown;
        }

        public GranadeType(eGranadeType type)
        {
            type_ = type;
        }

        public override string ToString()
        {
            switch(type_)
            {
                case eGranadeType.Normal:
                    return "Normal";
                case eGranadeType.Fire:
                    return "Fire";
                case eGranadeType.Power:
                    return "Power";
                case eGranadeType.Cluster:
                    return "Cluster";
                case eGranadeType.Magnet:
                    return "Magnet";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// 文字列から手榴弾タイプへ変換
        /// </summary>
        public bool FromStr(in string str)
        {
            switch(str.ToLower())
            {
                case "normal":
                    type_ = eGranadeType.Normal;
                    return true;
                case "fire":
                    type_ = eGranadeType.Fire;
                    return true;
                case "power":
                    type_ = eGranadeType.Power;
                    return true;
                case "cluster":
                    type_ = eGranadeType.Cluster;
                    return true;
                case "magnet":
                    type_ = eGranadeType.Magnet;
                    return true;
                default:
                    type_ = eGranadeType.Unknown;
                    return false;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is GranadeType type &&
                   type_ == type.type_;
        }

        public override int GetHashCode()
        {
            return type_.GetHashCode();
        }

        public static bool operator==(GranadeType a, GranadeType b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            return a.type_ == b.type_;
        }

        public static bool operator !=(GranadeType a, GranadeType b)
        {
            return !(a == b);
        }
    }

    /// <summary>
    /// 手榴弾の基本クラス
    /// </summary>
    public abstract class AbstractGrenade : OpenGSCore.AbstractGameObject
    {
        private int stoppingPower = 0;
        private int angle = 0;
        private int speed = 0;
        private int damage = 0;
        private int explosionDamage = 0;
        private GranadeType granadeType = new();
        private string ownerId = "";
        private float lifeTime = 3.0f; // 秒単位

        public int StoppingPower { get => stoppingPower; set => stoppingPower = value; }
        public int Angle { get => angle; set => angle = value; }
        public int Speed { get => speed; set => speed = value; }
        public int ExplosionDamage { get => explosionDamage; set => explosionDamage = value; }
        public int Damage { get => damage; set => damage = value; }
        public string OwnerId { get => ownerId; set => ownerId = value; }
        public GranadeType GranadeType { get => granadeType; set => granadeType = value; }
        public float LifeTime { get => lifeTime; set => lifeTime = value; }

        protected AbstractGrenade()
        {
        }

        protected AbstractGrenade(float x, float y)
        {
            Posx = x;
            Posy = y;
        }

        /// <summary>
        /// 手榴弾が何かにぶつかったときの処理
        /// </summary>
        public abstract void Hit();

        public override Newtonsoft.Json.Linq.JObject ToJSon()
        {
            var json = base.ToJSon();
            json["stoppingPower"] = StoppingPower;
            json["angle"] = Angle;
            json["speed"] = Speed;
            json["damage"] = Damage;
            json["explosionDamage"] = ExplosionDamage;
            json["ownerId"] = OwnerId;
            json["granadeType"] = GranadeType.ToString();
            json["lifeTime"] = LifeTime;
            return json;
        }
    }
}
