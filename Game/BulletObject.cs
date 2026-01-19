using System;
using System.Numerics;
using OpenGSCore; // OpenGSCoreのAbstractGameObjectを使用

namespace OpenGSServer
{
    /// <summary>
    /// 弾丸の基底クラス
    /// OpenGSCore.AbstractGameObjectを継承（統合版）
    /// </summary>
    public abstract class AbstractBullet : OpenGSCore.AbstractGameObject
    {
        public float Damage { get; protected set; } = 0;
        public float Angle { get; protected set; } = 0;
        public float Speed { get; protected set; } = 0;
        public float StoppingPower { get; set; } = 0;
        public string OwnerId { get; set; } = string.Empty;
        public float Lifetime { get; protected set; } = 10f;
        public bool IsActive { get; set; } = true;

        // 計算プロパティ
        public Vector2 Position => new(Posx, Posy);
        public Vector2 Velocity => new(
            Speed * MathF.Cos(Angle),
            Speed * MathF.Sin(Angle)
        );

        protected AbstractBullet(float x, float y, float speed, float damage, float angle, float stoppingPower)
        {
            // OpenGSCore.AbstractGameObjectのSetPosメソッドを使用
            SetPos(x, y);
            Speed = speed;
            StoppingPower = stoppingPower;
            Damage = damage;
            Angle = angle;
            ObjectType = eGameObjectType.Character; // OpenGSCoreのenum
        }

        public override void Update()
        {
            base.Update(); // OpenGSCoreのUpdate呼び出し
            
            if (!IsActive) return;

            // 位置更新（簡易版）
            var dt = 1f / 60f;
            UpdatePosition(dt);
            UpdateLifetime(dt);
        }

        protected virtual void UpdatePosition(float deltaTime)
        {
            float newX = Posx + Speed * MathF.Cos(Angle) * deltaTime;
            float newY = Posy + Speed * MathF.Sin(Angle) * deltaTime;
            SetPos(newX, newY); // OpenGSCoreのメソッド使用
        }

        protected virtual void UpdateLifetime(float deltaTime)
        {
            Lifetime -= deltaTime;
            if (Lifetime <= 0)
            {
                IsActive = false;
            }
        }

        /// <summary>
        /// 衝突判定（円形）
        /// </summary>
        public virtual bool CheckCollision(Vector2 targetPos, float targetRadius)
        {
            if (!IsActive) return false;

            var distance = Vector2.Distance(Position, targetPos);
            return distance <= (GetCollisionRadius() + targetRadius);
        }

        protected virtual float GetCollisionRadius() => 0.1f;

        public override bool HasChanged()
        {
            return IsActive && base.HasChanged(); // OpenGSCoreのメソッド使用
        }
    }

    /// <summary>
    /// 通常の弾丸
    /// </summary>
    public sealed class BulletGameObject : AbstractBullet
    {
        private const float DefaultSpeed = 20f;
        private const float DefaultDamage = 25f;
        private const float BulletRadius = 0.1f;

        public BulletGameObject(
            float x,
            float y,
            float speed = DefaultSpeed,
            float damage = DefaultDamage,
            float angle = 0f,
            float stoppingPower = 1f)
            : base(x, y, speed, damage, angle, stoppingPower)
        {
        }

        protected override float GetCollisionRadius() => BulletRadius;
    }
}
