using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    public class Bullet : Projectile
    {
        public struct Shot
        {
            public float damage;

            public float knockback;

            public float speed;
            public float range;

            public float radius;

            public bool pierce;

            public float visualScale;
        }

        public const float DefaultRadius = 0.25f;

        Shot shot;
        Vector3 pos, dir;
        float traveled;

        readonly HashSet<Combatant> struck = new HashSet<Combatant>();

        public void Launch(Shot s, IWeaponHolder owner, Vector3 start, Vector3 direction)
        {
            shot = s;
            Owner = owner;
            pos = Query.AtCastHeight(start);
            dir = Query.Flat(direction).normalized;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(dir);
            if (Art.On && Art.Set.bullet)
            {
                Art.Flat(transform, Art.Set.bullet, 1.3f * s.visualScale, Art.OrderProjectile, out _,
                    new Vector3(0f, 0f, -0.4f * s.visualScale));
            }
            else
            {
                MakeVisual(transform, PrimitiveType.Sphere, Vector3.one * (0.3f * s.visualScale),
                    new Color(1f, 0.85f, 0.3f));
                AddTrail(new Color(1f, 0.85f, 0.3f, 0.8f), 0.15f * s.visualScale);
            }
        }

        void FixedUpdate()
        {
            Vector3 next = pos + dir * (shot.speed * Time.fixedDeltaTime);

            bool hitWall = Query.WallBetween(pos, next, out var wall);
            if (hitWall) next = wall.point;

            foreach (var c in Query.CharactersAlong(pos, next, shot.radius))
            {
                if ((IWeaponHolder)c == Owner || !c.IsAlive || !struck.Add(c)) continue;
                c.ReceiveDamage(DamageInfo.Attack(shot.damage, this, dir, shot.knockback,
                    shot.pierce ? HitKind.Pierce : HitKind.Bullet));
                if (shot.pierce) continue;
                Destroy(gameObject);
                return;
            }

            traveled += Vector3.Distance(pos, next);
            pos = next;
            transform.position = pos;
            if (hitWall || traveled >= shot.range) Destroy(gameObject);
        }
    }
}