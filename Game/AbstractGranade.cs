




using System;
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

    public enum GrenadeState
    {
        Idle,
        Armed,
        Exploded,
        Disposed
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
        private float fuseTime = 3.0f;
        private float elapsedTime;
        private float explosionRadius = 2.5f;
        private GrenadeState state = GrenadeState.Idle;

        public event Action<AbstractGrenade>? Armed;
        public event Action<AbstractGrenade>? Exploded;
        public event Action<AbstractGrenade>? ChildGrenadeCreated;

        public int StoppingPower { get => stoppingPower; set => stoppingPower = value; }
        public int Angle { get => angle; set => angle = value; }
        public int Speed { get => speed; set => speed = value; }
        public int ExplosionDamage { get => explosionDamage; set => explosionDamage = value; }
        public int Damage { get => damage; set => damage = value; }
        public string OwnerId { get => ownerId; set => ownerId = value; }
        public GranadeType GranadeType { get => granadeType; set => granadeType = value; }
        public float LifeTime { get => lifeTime; set => lifeTime = value; }
        public float FuseTime { get => fuseTime; set => fuseTime = Math.Max(0f, value); }
        public float ExplosionRadius { get => explosionRadius; set => explosionRadius = Math.Max(0f, value); }
        public GrenadeState State => state;
        public float ElapsedTime => elapsedTime;
        public float RemainingFuseTime => Math.Max(0f, FuseTime - elapsedTime);
        public bool AutoExplodeOnFuseTimeout { get; set; } = true;
        public bool IsArmed => state == GrenadeState.Armed;
        public bool IsExploded => state == GrenadeState.Exploded;
        public bool IsDisposed => state == GrenadeState.Disposed;

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
        public void Arm()
        {
            if (state != GrenadeState.Idle)
            {
                return;
            }

            state = GrenadeState.Armed;
            elapsedTime = 0f;
            OnArmed();
            Armed?.Invoke(this);
        }

        public bool Disarm()
        {
            if (state != GrenadeState.Armed)
            {
                return false;
            }

            state = GrenadeState.Idle;
            elapsedTime = 0f;
            return true;
        }

        public void ForceExplode()
        {
            if (state == GrenadeState.Exploded || state == GrenadeState.Disposed)
            {
                return;
            }

            state = GrenadeState.Exploded;
            OnExploded();
            Exploded?.Invoke(this);
        }

        public bool UpdateCountdown(float deltaSeconds)
        {
            if (!IsArmed || deltaSeconds <= 0f)
            {
                return false;
            }

            elapsedTime += deltaSeconds;

            if (elapsedTime >= FuseTime)
            {
                // Reached or passed fuse time; caller (GameScene/network) may notify clients.
                elapsedTime = FuseTime;
                return true;
            }

            return false;
        }

        public void DisposeGrenade()
        {
            state = GrenadeState.Disposed;
        }

        // Apply state from client-provided JSON without invoking server-side explosion logic.
        public void ApplyState(Newtonsoft.Json.Linq.JObject json)
        {
            if (json == null) return;

            if (json.TryGetValue("PosX", out var px))
            {
                if (float.TryParse(px.ToString(), out var fx)) Posx = fx;
            }
            if (json.TryGetValue("PosY", out var py))
            {
                if (float.TryParse(py.ToString(), out var fy)) Posy = fy;
            }

            if (json.TryGetValue("ownerId", out var oid))
            {
                OwnerId = oid.ToString();
            }

            if (json.TryGetValue("fuseTime", out var ft))
            {
                if (float.TryParse(ft.ToString(), out var f)) FuseTime = f;
            }

            if (json.TryGetValue("remainingFuseTime", out var rft))
            {
                if (float.TryParse(rft.ToString(), out var rf))
                {
                    elapsedTime = Math.Max(0f, FuseTime - rf);
                }
            }

            if (json.TryGetValue("explosionRadius", out var er))
            {
                if (float.TryParse(er.ToString(), out var fr)) ExplosionRadius = fr;
            }

            if (json.TryGetValue("damage", out var dmg))
            {
                if (int.TryParse(dmg.ToString(), out var di)) Damage = di;
            }

            if (json.TryGetValue("explosionDamage", out var ed))
            {
                if (int.TryParse(ed.ToString(), out var ei)) ExplosionDamage = ei;
            }

            if (json.TryGetValue("state", out var st))
            {
                var s = st.ToString();
                if (Enum.TryParse<GrenadeState>(s, true, out var parsed))
                {
                    state = parsed;
                }
            }
        }

        // Explicitly set state without invoking explosion side-effects.
        public void SetState(GrenadeState toState, float elapsed = 0f)
        {
            state = toState;
            elapsedTime = Math.Max(0f, elapsed);
        }

        protected virtual void OnArmed()
        {
        }

        protected virtual void OnExploded()
        {
            Hit();
        }

        protected bool NotifyChildCreated(AbstractGrenade child)
        {
            if (child == null)
            {
                return false;
            }

            ChildGrenadeCreated?.Invoke(child);
            return true;
        }

        public abstract void Hit();

        public override Newtonsoft.Json.Linq.JObject ToJSon()
        {
            var json = base.ToJSon() ?? new JObject();
            json["stoppingPower"] = StoppingPower;
            json["angle"] = Angle;
            json["speed"] = Speed;
            json["damage"] = Damage;
            json["explosionDamage"] = ExplosionDamage;
            json["ownerId"] = OwnerId;
            json["granadeType"] = GranadeType.ToString();
            json["lifeTime"] = LifeTime;
            json["fuseTime"] = FuseTime;
            json["remainingFuseTime"] = RemainingFuseTime;
            json["explosionRadius"] = ExplosionRadius;
            json["state"] = State.ToString();
            return json;
        }
    }
}
