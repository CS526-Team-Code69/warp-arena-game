using UnityEngine;

namespace AnomalyArena
{
    public class RocketStrike : Projectile
    {
        const float DropHeight = 14f;
        const float DiscThickness = 0.01f;

        MissileStrikeEffect cfg;
        Vector3 pos;
        float t;
        Transform rocket, flame, fill;
        Material rangeMat, fillMat;

        public void Launch(MissileStrikeEffect e, IWeaponHolder owner, Vector3 point)
        {
            cfg = e;
            Owner = owner;
            pos = Query.Flat(point);
            transform.position = pos;

            rangeMat = GameManager.Instance.FxMat(new Color(1f, 0.25f, 0.15f, 0.18f));
            fillMat = GameManager.Instance.FxMat(new Color(1f, 0.25f, 0.15f, 0.4f));
            Disc(rangeMat, 0.05f).localScale = DiscScale(cfg.explosionRadius * 2f);
            fill = Disc(fillMat, 0.06f);

            rocket = new GameObject("Rocket").transform;
            rocket.SetParent(transform, false);
            rocket.localRotation = Quaternion.LookRotation(Vector3.back);
            if (Art.On && Art.Set.missile)
            {
                Art.Flat(rocket, Art.Set.missile, 1.6f, Art.OrderProjectile, out _);
                if (Art.Set.flame)
                    flame = Art.Flat(rocket, Art.Set.flame, 1.3f, Art.OrderProjectile, out _,
                        new Vector3(0f, 0f, -1.4f));
            }
            else
            {
                MakeVisual(rocket, PrimitiveType.Cylinder, new Vector3(0.3f, 0.35f, 0.3f),
                    new Color(0.9f, 0.35f, 0.2f)).localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            UpdateVisuals();
        }

        Transform Disc(Material m, float height)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            go.transform.localScale = DiscScale(0f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static Vector3 DiscScale(float diameter) => new Vector3(diameter, DiscThickness, diameter);

        void Update()
        {
            if (flame) flame.localScale = new Vector3(1f, 1f, 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 12f, 0f));
        }

        void FixedUpdate()
        {
            t += Time.fixedDeltaTime;
            if (t >= cfg.strikeDelay)
            {
                Explode();
                return;
            }

            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            float k = cfg.strikeDelay > 0f ? Mathf.Clamp01(t / cfg.strikeDelay) : 1f;
            rocket.localPosition = Vector3.up * (DropHeight * (1f - k));
            fill.localScale = DiscScale(cfg.explosionRadius * 2f * k);
        }

        void Explode()
        {
            Vector3 center = Query.AtCastHeight(pos);
            var art = Art.On ? Art.Set : null;
            if (art && art.explosionFrames != null && art.explosionFrames.Length > 0)
            {
                Fx.Flipbook(art.explosionFrames, center + Vector3.up * 0.5f, cfg.explosionRadius * 1.5f, 0.35f);
                if (art.explosionRing) Fx.SpriteRing(art.explosionRing, center, cfg.explosionRadius, 0.45f);
            }
            else
            {
                Fx.Explosion(center, cfg.explosionRadius);
            }

            foreach (var c in Query.Characters(center, cfg.explosionRadius))
            {
                if (!c.IsAlive) continue;
                Vector3 away = Query.Flat(c.Position - center);
                if (away.sqrMagnitude < 1e-4f) away = Random.insideUnitSphere;
                c.ReceiveDamage(DamageInfo.Attack(cfg.damage, this, away, cfg.knockback, HitKind.Blast));
            }

            Destroy(gameObject);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Destroy(rangeMat);
            Destroy(fillMat);
        }
    }
}