namespace OpenGSServer
{
    public class PowerGrenade : AbstractGrenade
    {
        public PowerGrenade(float x, float y) : base(x, y)
        {
            Damage = 110;
            ExplosionDamage = 160;
            ExplosionRadius = 4.5f;
            FuseTime = 2.5f;
            StoppingPower = 85;
        }

        public override void Hit() { }
    }

    public class ClusterGrenade : AbstractGrenade
    {
        public int ChildCount { get; set; } = 3;

        public ClusterGrenade(float x, float y) : base(x, y)
        {
            FuseTime = 2.0f;
            ExplosionRadius = 3.0f;
        }

        public override void Hit() { }
    }

    public class ChildClusterGrenade : AbstractGrenade
    {
        public ChildClusterGrenade(float x, float y) : base(x, y)
        {
            Damage = 60;
            ExplosionDamage = 90;
            ExplosionRadius = 2.0f;
            FuseTime = 1.0f;
        }

        public override void Hit() { }
    }
}
