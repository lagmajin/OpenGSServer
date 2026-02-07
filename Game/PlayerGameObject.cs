using System;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class PlayerGameObject : AbstractGameObject
    {
        public string PlayerId { get; set; }
        public string DisplayName { get; set; }

        public float Rotation { get; set; } = 0f;
        public float VelocityX { get; set; } = 0f;
        public float VelocityY { get; set; } = 0f;

        public int MaxHp { get; private set; } = 100;
        public int Hp { get; private set; } = 100;

        public bool IsAlive => Hp > 0;

        public PlayerGameObject(string playerId, string displayName, float x = 0f, float y = 0f) : base(x, y)
        {
            PlayerId = playerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Id = PlayerId; // use player id as object id by default
            Posx = x;
            Posy = y;
        }

        public override void Update()
        {
            // simple positional update: apply velocity
            Posx += VelocityX;
            Posy += VelocityY;

            // additional per-frame logic can be added here
        }

        public void ApplyDamage(int amount)
        {
            if (amount <= 0) return;

            Hp = Math.Max(0, Hp - amount);
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;

            Hp = Math.Min(MaxHp, Hp + amount);
        }

        public override bool HasChanged()
        {
            return base.HasChanged() || Rotation != 0f || VelocityX != 0f || VelocityY != 0f || !IsAlive;
        }

        public override void SaveSyncState()
        {
            // reset sync state
            lastPosx = Posx;
            lastPosy = Posy;
        }

        public override Newtonsoft.Json.Linq.JObject ToJSon()
        {
            var json = new JObject
            {
                ["Type"] = "Player",
                ["PlayerId"] = PlayerId,
                ["DisplayName"] = DisplayName,
                ["ID"] = Id,
                ["PosX"] = Posx,
                ["PosY"] = Posy,
                ["Rotation"] = Rotation,
                ["VelocityX"] = VelocityX,
                ["VelocityY"] = VelocityY,
                ["Hp"] = Hp,
                ["MaxHp"] = MaxHp,
                ["IsAlive"] = IsAlive
            };

            return json;
        }
    }
}
