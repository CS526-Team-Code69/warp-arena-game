using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public sealed partial class V5Vfx
    {
        static readonly Color FireTint = new(1f, .62f, .24f), PoisonTint = new(.52f, .95f, .25f), FrostTint = new(.68f, .9f, 1f), ArcTint = new(1f, .88f, .42f), VoidTint = new(.72f, .42f, 1f), LightTint = new(1f, .93f, .62f), EarthTint = new(.86f, .72f, .52f);

        public Material QuadAdditive { get; private set; }

        public Material SpriteAdditive { get; private set; }
        Material trailMaterial;
        readonly Dictionary<string, Sprite> sprites = new();

        void BuildBodyLayers()
        {
            QuadAdditive = Mat("glow", true, 2.4f, 0f, 0f);
            SpriteAdditive = Mat("glow", true, 2.4f, 0f, 0f);
            SpriteAdditive.SetFloat("_UseSprite", 1f);
            trailMaterial = Mat("softline", true, 2f, 0f, 0f);
        }

        public Material TrailMaterial => trailMaterial;

        public Sprite Sprite(string name, float pixelsPerUnit = 100f)
        {
            if (sprites.TryGetValue(name, out var s)) return s;
            var t = Tex(name);
            s = t ? UnityEngine.Sprite.Create(t, new Rect(0f, 0f, t.width, t.height), new Vector2(.5f, .5f), pixelsPerUnit, 0, SpriteMeshType.FullRect) : null;
            sprites[name] = s;
            return s;
        }

        static bool Chance(float perSecond) => Random.value < perSecond * Time.deltaTime * Quality;

        public static Color ElementTint(ElementKind element) => element switch
        {
            ElementKind.Fire => FireTint,
            ElementKind.Poison => PoisonTint,
            ElementKind.Frost => FrostTint,
            ElementKind.Lightning => ArcTint,
            ElementKind.Earth => EarthTint,
            ElementKind.Light => LightTint,
            ElementKind.Void => VoidTint,
            _ => new Color(1f, .9f, .68f)
        };

        public void Status(Combatant body, ElementStatus status, float r, Vector3 chest)
        {
            if (status.Fire > .3f)
            {
                float heat = Mathf.Clamp01(status.Fire / 8f);
                if (Chance(Mathf.Lerp(9f, 20f, heat)))
                {
                    Vector2 o = Random.insideUnitCircle * r * .55f;
                    smallFlame.Emit(chest + new Vector3(o.x, R(-.1f, .4f), o.y), Vector3.up * R(.2f, .7f), r * R(2.1f, 3f) * Mathf.Lerp(.85f, 1.3f, heat), R(.4f, .62f), Color.white);
                }
                if (Chance(4f + 6f * heat)) ember.Emit(chest + Random.insideUnitSphere * r * .6f, new Vector3(R(-.6f, .6f), R(1.5f, 3f), R(-.6f, .6f)), R(.05f, .1f), R(.4f, .8f), FireTint);
            }
            if (status.Poison > .5f)
            {
                float dose = Mathf.Clamp01(status.Poison / 10f);
                if (Chance(Mathf.Lerp(3f, 7f, dose)))
                    cloud.Emit(chest + Random.insideUnitSphere * r * .5f, new Vector3(R(-.4f, .4f), R(.3f, .8f), R(-.4f, .4f)), r * R(1.5f, 2.3f), R(.7f, 1.1f), WithAlpha(PoisonTint, .3f + .25f * dose), R(0f, 360f));
                if (Chance(Mathf.Lerp(2f, 6f, dose)))
                    bubble.Emit(chest + Random.insideUnitSphere * r * .75f, Vector3.up * R(.8f, 1.8f), R(.12f, .3f), R(.4f, .8f), Bright(PoisonTint, .2f));
            }
            if (status.Frozen)
            {
                if (Chance(5f)) glint.Emit(chest + Random.insideUnitSphere * r * 1.1f, Vector3.zero, R(.25f, .55f), R(.2f, .4f), FrostTint);
            }
            else if (status.Frost > .05f)
            {
                float chill = status.Chill01;
                if (Chance(3f + 9f * chill)) glint.Emit(chest + Random.insideUnitSphere * r * .9f, Vector3.down * R(.2f, .6f), R(.12f, .3f), R(.25f, .5f), FrostTint);
                if (Chance(1.5f + 3f * chill)) cloud.Emit(chest + Random.insideUnitSphere * r * .4f, Vector3.down * .3f, r * R(1.4f, 2f), R(.5f, .8f), WithAlpha(FrostTint, .16f + .2f * chill), R(0f, 360f));
            }
            if (status.Paralysed || status.Lightning > .5f)
            {
                float charge = status.Paralysed ? 1f : Mathf.Clamp01(status.Lightning / 6f);
                if (Chance(Mathf.Lerp(5f, 18f, charge)))
                    elec.Emit(chest + Random.insideUnitSphere * r * .55f, Vector3.zero, r * R(1.7f, 2.6f) * Mathf.Lerp(.85f, 1.35f, charge), R(.05f, .1f), ArcTint, R(0f, 360f));
                if (status.Twitching && Chance(22f))
                {
                    Vector3 a = chest + Random.insideUnitSphere * r * 1.1f, b = chest + Random.insideUnitSphere * r * 1.1f;
                    Bolt(a, b, ArcTint, r * .8f, .06f);
                }
            }
        }

        public void FireField(Vector3 centre, float radius, float strength = 1f)
        {
            float rate = Mathf.Clamp(radius * radius * 4.6f, 9f, 34f) * strength;
            int emitted = 0;
            while (emitted < 3 && Chance(rate))
            {
                emitted++;
                Vector2 o = Random.insideUnitCircle * radius * .92f;
                float size = R(.85f, 1.6f) * Mathf.Clamp(radius * .55f, .7f, 1.3f);

                flame.Emit(new Vector3(centre.x + o.x, size * .3f, centre.z + o.y), Vector3.up * R(.1f, .4f), size, R(.5f, .85f), new Color(1f, R(.62f, 1f), R(.45f, .9f), R(.75f, 1f)));
            }
            if (Chance(rate * .22f)) ember.Emit(new Vector3(centre.x, .6f, centre.z) + new Vector3(R(-radius, radius), 0f, R(-radius, radius)) * .8f, new Vector3(R(-.5f, .5f), R(2f, 4f), R(-.5f, .5f)), R(.05f, .11f), R(.5f, 1f), FireTint);
            if (Chance(rate * .07f)) smoke.Emit(new Vector3(centre.x, 1.6f, centre.z) + new Vector3(R(-radius, radius), 0f, R(-radius, radius)) * .6f, Vector3.up * R(.6f, 1.2f), R(1.2f, 2f), R(1f, 1.6f), new Color(.22f, .2f, .19f, .34f), R(0f, 360f));
            if (Chance(5f)) groundGlow.Emit(OnGround(centre), Vector3.zero, radius * 2.9f, .34f, new Color(1f, .46f, .13f, .32f * strength));
        }

        public void PoisonField(Vector3 centre, float radius, float strength = 1f)
        {
            float rate = Mathf.Clamp(radius * radius * 1.8f, 4f, 13f) * strength;
            int emitted = 0;
            while (emitted < 3 && Chance(rate))
            {
                emitted++;
                Vector2 o = Random.insideUnitCircle * radius * .78f;
                cloud.Emit(new Vector3(centre.x + o.x, R(.4f, 1f), centre.z + o.y), new Vector3(o.x, 0f, o.y) * .18f + Vector3.up * R(.05f, .3f), Mathf.Clamp(radius * R(.8f, 1.15f), 1.2f, 6f), R(1.1f, 1.7f), new Color(.46f, .88f, .2f, .3f * strength), R(0f, 360f));
            }
            if (Chance(rate * .55f))
            {
                Vector2 o = Random.insideUnitCircle * radius;
                bubble.Emit(new Vector3(centre.x + o.x, .5f, centre.z + o.y), Vector3.up * R(.5f, 1.4f), R(.14f, .34f), R(.5f, .95f), Bright(PoisonTint, .25f));
            }
            if (Chance(4f)) groundGlow.Emit(OnGround(centre), Vector3.zero, radius * 2.6f, .4f, new Color(.28f, .8f, .1f, .24f * strength));
        }

        public void FrostField(Vector3 centre, float radius, float strength = 1f)
        {
            float rate = Mathf.Clamp(radius * radius * 2f, 4f, 16f) * strength;
            if (Chance(rate))
            {
                Vector2 o = Random.insideUnitCircle * radius * .85f;
                cloud.Emit(new Vector3(centre.x + o.x, R(.3f, .8f), centre.z + o.y), Vector3.zero, Mathf.Clamp(radius * R(.7f, 1.05f), 1f, 5f), R(.9f, 1.4f), new Color(.78f, .92f, 1f, .2f * strength), R(0f, 360f));
            }
            if (Chance(rate * 1.4f))
            {
                Vector2 o = Random.insideUnitCircle * radius;
                glint.Emit(new Vector3(centre.x + o.x, R(.6f, 2f), centre.z + o.y), new Vector3(R(-.3f, .3f), -R(.8f, 1.6f), R(-.3f, .3f)), R(.12f, .3f), R(.4f, .8f), FrostTint);
            }
        }

        public void FrostMark(Vector3 at, float radius, float life, float alpha = .8f)
        {
            frostDecal.Emit(OnGround(at, .05f), Vector3.zero, radius * 2.2f, life, new Color(.74f, .93f, 1f, alpha), R(0f, 360f));
        }

        public void Radiation(Vector3 centre, float radius, float fade)
        {
            float rate = Mathf.Clamp(radius * radius * 1.7f, 4f, 12f);
            if (Chance(rate))
            {
                Vector2 o = Random.insideUnitCircle * radius * .8f;
                cloud.Emit(new Vector3(centre.x + o.x, R(.3f, .8f), centre.z + o.y), Vector3.up * R(.1f, .4f), Mathf.Clamp(radius * R(.7f, 1.05f), 1.2f, 5f), R(.9f, 1.4f), new Color(.82f, .95f, .22f, .22f * fade), R(0f, 360f));
            }
            if (Chance(rate * 1.1f))
            {
                Vector2 o = Random.insideUnitCircle * radius;
                ember.Emit(new Vector3(centre.x + o.x, .3f, centre.z + o.y), Vector3.up * R(.8f, 2f), R(.05f, .1f), R(.5f, 1f), new Color(.85f, 1f, .3f));
            }
            if (Chance(5f)) groundGlow.Emit(OnGround(centre), Vector3.zero, radius * 2.6f, .34f, new Color(.6f, .85f, .1f, .24f * fade));
        }

        public void ChantMotes(Vector3 centre, float radius)
        {
            if (!Chance(Mathf.Clamp(radius * 1.6f, 3f, 10f))) return;
            Vector2 o = Random.insideUnitCircle * radius;
            glint.Emit(new Vector3(centre.x + o.x, .3f, centre.z + o.y), Vector3.up * R(.8f, 1.6f), R(.14f, .3f), R(.6f, 1.1f), new Color(.8f, .58f, 1f));
        }

        public void HotShell(Vector3 at, float heat)
        {
            if (Chance(8f + 14f * heat)) ember.Emit(new Vector3(at.x, .35f, at.z), new Vector3(R(-.5f, .5f), R(1f, 2.4f), R(-.5f, .5f)), R(.05f, .09f), R(.25f, .5f), FireTint);
            if (Chance(2f + 8f * heat)) smallFlame.Emit(new Vector3(at.x, .45f, at.z), Vector3.up * .3f, R(.9f, 1.4f) * (.7f + heat * .6f), R(.3f, .45f), Color.white);
            if (Chance(6f)) groundGlow.Emit(OnGround(at), Vector3.zero, 1.8f + heat * 1.4f, .22f, new Color(1f, .45f, .12f, .25f + .3f * heat));
        }

        public struct ProjectileLook
        {
            public string sprite;
            public float length, width;
            public bool additive, flat, spin;
            public Color tint, trail;
            public float trailWidth, trailTime;
        }

        public static ProjectileLook LookOf(AttackActor a)
        {
            var w = a.weapon;
            Color element = ElementTint(a.element);
            bool infused = a.element != ElementKind.None;
            var tracer = new ProjectileLook { sprite = "tracer", length = 1.25f, width = .3f, additive = true, flat = true, tint = infused ? element : new Color(1f, .86f, .55f), trail = infused ? element : new Color(1f, .7f, .3f), trailWidth = .1f, trailTime = .07f };
            if (w == null) return tracer;
            switch (w.form)
            {
                case WeaponForm.Rifle:
                    if (w.variant == 0) { tracer.length = 2.8f; tracer.width = .52f; tracer.tint = infused ? element : new Color(.68f, .9f, 1f); tracer.trail = infused ? element : new Color(.4f, .75f, 1f); tracer.trailWidth = .2f; tracer.trailTime = .12f; }
                    return tracer;
                case WeaponForm.Minigun:
                    tracer.length = 1.5f; tracer.width = .3f; tracer.trailTime = .05f; tracer.trailWidth = .08f;
                    return tracer;
                case WeaponForm.ScatterCannon:
                    tracer.length = 1.1f; tracer.width = .28f; tracer.tint = infused ? element : new Color(1f, .72f, .35f);
                    return tracer;
                case WeaponForm.Railgun:
                    tracer.length = 3.6f; tracer.width = .62f; tracer.tint = infused ? element : new Color(.55f, .78f, 1f); tracer.trail = infused ? element : new Color(.35f, .55f, 1f); tracer.trailWidth = .26f; tracer.trailTime = .22f;
                    return tracer;
                case WeaponForm.SentryDrone:
                    tracer.length = .95f; tracer.width = .2f; tracer.tint = infused ? element : new Color(.6f, .95f, 1f); tracer.trail = infused ? element : new Color(.4f, .85f, 1f); tracer.trailWidth = .06f; tracer.trailTime = .05f;
                    return tracer;
                case WeaponForm.SwarmLauncher:
                    tracer.length = 1.05f; tracer.width = .32f; tracer.tint = new Color(1f, .78f, .42f); tracer.trail = new Color(1f, .5f, .2f); tracer.trailWidth = .14f; tracer.trailTime = .24f;
                    return tracer;
                case WeaponForm.Knife:
                    return new ProjectileLook { sprite = "blade", length = 1.5f, width = .42f, additive = false, flat = true, spin = true, tint = Color.white, trail = infused ? element : new Color(.6f, .88f, 1f), trailWidth = .22f, trailTime = .1f };
                case WeaponForm.CryoCannon:
                    return new ProjectileLook { sprite = "iceshard", length = 1.5f, width = .56f, additive = false, flat = true, tint = Color.white, trail = FrostTint, trailWidth = .16f, trailTime = .14f };
                case WeaponForm.SporeLauncher:
                    if (w.variant == 1) { tracer.length = 1f; tracer.width = .22f; tracer.tint = PoisonTint; tracer.trail = PoisonTint; tracer.trailWidth = .07f; return tracer; }
                    return new ProjectileLook { sprite = "spore", length = .8f, width = .8f, additive = false, flat = false, tint = Color.white, trail = PoisonTint, trailWidth = .14f, trailTime = .16f };
                case WeaponForm.Flamethrower:
                    return new ProjectileLook { sprite = "orb", length = 1.3f, width = 1.3f, additive = true, flat = false, tint = new Color(1f, .6f, .22f), trail = FireTint, trailWidth = .32f, trailTime = .22f };
                default:
                    return tracer;
            }
        }

        public void ProjectileTick(AttackActor a)
        {
            var w = a.weapon;
            if (w == null) return;
            Vector3 at = a.transform.position;
            switch (w.form)
            {
                case WeaponForm.SwarmLauncher:
                    if (Chance(26f)) smokeLow.Emit(at, Vector3.up * .2f, R(.4f, .75f), R(.32f, .55f), new Color(.62f, .6f, .58f, .36f), R(0f, 360f));
                    break;
                case WeaponForm.Flamethrower:
                    if (Chance(46f)) fireball.Emit(at + Random.insideUnitSphere * .18f, Vector3.up * .2f, R(.7f, 1.1f), R(.22f, .4f), new Color(1f, .78f, .55f, .95f), R(0f, 360f));
                    if (Chance(14f)) ember.Emit(at, Random.insideUnitSphere * 1.5f, R(.05f, .1f), R(.25f, .5f), FireTint);
                    break;
                case WeaponForm.CryoCannon:
                    if (Chance(16f)) glint.Emit(at, Random.insideUnitSphere * .4f, R(.14f, .3f), R(.2f, .34f), FrostTint);
                    break;
                case WeaponForm.SporeLauncher:
                    if (w.variant == 0 && Chance(14f)) cloud.Emit(at, Vector3.zero, R(.5f, .8f), R(.3f, .5f), WithAlpha(PoisonTint, .3f), R(0f, 360f));
                    break;
            }
        }

        public void BuffMotes(Vector3 feet, float radius, Color c, float perSecond, float rise = 1.4f, float size = .22f)
        {
            if (!Chance(perSecond)) return;
            Vector2 o = Random.insideUnitCircle * radius;
            ember.Emit(new Vector3(feet.x + o.x, R(.2f, .9f), feet.z + o.y), Vector3.up * R(rise * .6f, rise), size * R(.5f, 1f), R(.4f, .9f), c);
        }

        public void OverchargeTick(Vector3 chest, float r)
        {
            if (Chance(9f)) elec.Emit(chest + Random.insideUnitSphere * r * .7f, Vector3.zero, R(1.2f, 1.9f), R(.05f, .09f), new Color(1f, .78f, .35f), R(0f, 360f));
            if (Chance(14f)) ember.Emit(chest + Random.insideUnitSphere * r, Random.insideUnitSphere * 1.6f, R(.05f, .1f), R(.2f, .4f), new Color(1f, .75f, .3f));
        }

        public void VoidSmoke(Vector3 at, float strength = 1f)
        {
            if (Chance(20f * strength)) smoke.Emit(at + Random.insideUnitSphere * .35f, Random.insideUnitSphere * .3f, R(.9f, 1.5f), R(.35f, .6f), new Color(.1f, .04f, .2f, .75f), R(0f, 360f));
            if (Chance(16f * strength)) ember.Emit(at + Random.insideUnitSphere * .6f, Vector3.up * R(.6f, 1.6f), R(.05f, .11f), R(.3f, .6f), VoidTint);
        }

        public void Blink(Vector3 from, Vector3 to)
        {
            Color c = new(.55f, .9f, 1f);
            Vector3 a = new(from.x, Query.CastHeight, from.z), b = new(to.x, Query.CastHeight, to.z);
            flash.Emit(a, Vector3.zero, 2.2f, .14f, c);
            flash.Emit(b, Vector3.zero, 2.8f, .16f, c);
            star.Emit(b, Vector3.zero, 3.6f, .16f, c, R(0f, 90f));
            ringLayer.Emit(OnGround(to, .09f), Vector3.zero, 3.4f, .22f, WithAlpha(c, .8f));
            Vector3 line = b - a;
            int n = Count(Mathf.Clamp(line.magnitude * 2f, 6f, 18f));
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, Random.value) + Random.insideUnitSphere * .4f;
                spark.Emit(p, line.normalized * R(4f, 10f), R(.05f, .1f), R(.14f, .34f), c);
            }
        }

        public void Shockwave(Vector3 at, float radius)
        {
            Color c = new(.82f, .92f, 1f);
            ringLayer.Emit(OnGround(at, .09f), Vector3.zero, radius * 2f, .32f, WithAlpha(c, 1f));
            softRing.Emit(OnGround(at, .085f), Vector3.zero, radius * 2.05f, .3f, WithAlpha(c, .38f));
            flash.Emit(new Vector3(at.x, Query.CastHeight, at.z), Vector3.zero, 3.2f, .1f, c);
            int n = Count(Mathf.Clamp(radius * 6f, 12f, 32f));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 360f / n + R(-6f, 6f), 0f) * Vector3.forward;
                smokeLow.Emit(OnGround(at, .4f) + d * radius * .25f, d * radius * R(1.8f, 3f), R(.8f, 1.5f), R(.3f, .55f), new Color(.68f, .64f, .58f, .34f), R(0f, 360f));
                if (i % 2 == 0) spark.Emit(OnGround(at, .6f), d * radius * R(3f, 5f), R(.06f, .1f), R(.12f, .25f), c);
            }
        }

        public void FrostNova(Vector3 at, float radius)
        {
            ringLayer.Emit(OnGround(at, .09f), Vector3.zero, radius * 2f, .34f, WithAlpha(FrostTint, 1f));
            softRing.Emit(OnGround(at, .085f), Vector3.zero, radius * 2.05f, .36f, WithAlpha(FrostTint, .4f));
            FrostMark(at, radius, 2.4f, .85f);
            flash.Emit(new Vector3(at.x, Query.CastHeight, at.z), Vector3.zero, radius * 1.2f, .12f, FrostTint);
            int n = Count(Mathf.Clamp(radius * 6f, 12f, 30f));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                cloud.Emit(OnGround(at, .6f) + d * radius * .2f, d * radius * R(1.2f, 2.4f), R(1f, 1.9f), R(.35f, .6f), new Color(.8f, .94f, 1f, .32f), R(0f, 360f));
                glint.Emit(OnGround(at, .7f), d * radius * R(1.5f, 3.2f), R(.2f, .45f), R(.2f, .45f), FrostTint);
            }
        }

        public void PoisonBurst(Vector3 at, float radius)
        {
            ringLayer.Emit(OnGround(at, .09f), Vector3.zero, radius * 2f, .3f, WithAlpha(PoisonTint, .85f));
            int n = Count(Mathf.Clamp(radius * 3f, 7f, 18f));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                cloud.Emit(OnGround(at, .7f) + d * radius * .2f, d * radius * R(1f, 2.1f), R(1f, 2f), R(.5f, .9f), new Color(.5f, .9f, .22f, .38f), R(0f, 360f));
                bubble.Emit(OnGround(at, .6f) + d * radius * R(.1f, .7f), Vector3.up * R(.8f, 1.6f), R(.14f, .34f), R(.4f, .8f), Bright(PoisonTint, .2f));
            }
        }

        public void HealGlow(Vector3 at)
        {
            Color c = LightTint;
            flash.Emit(new Vector3(at.x, Query.CastHeight, at.z), Vector3.zero, 2.6f, .22f, c);
            ringLayer.Emit(OnGround(at, .09f), Vector3.zero, 4f, .4f, WithAlpha(c, .85f));
            int n = Count(16);
            for (int i = 0; i < n; i++)
            {
                Vector2 o = Random.insideUnitCircle * 1.8f;
                ember.Emit(new Vector3(at.x + o.x, R(.1f, .8f), at.z + o.y), Vector3.up * R(2f, 4f), R(.08f, .18f), R(.5f, .95f), c);
            }
        }

        public void LightningStrike(Vector3 at, Color c)
        {
            float height = 12f;
            Vector3 top = at + Vector3.up * height + Vector3.back * 3.5f;
            Bolt(top, new Vector3(at.x, .2f, at.z), c, 1.2f, .11f);
            Bolt(top + Vector3.right * .6f, new Vector3(at.x, .2f, at.z), c, .7f, .07f);
            flash.Emit(new Vector3(at.x, 1f, at.z), Vector3.zero, 3f, .1f, c);
            star.Emit(new Vector3(at.x, 1f, at.z), Vector3.zero, 3.4f, .12f, c, R(0f, 90f));
            groundGlow.Emit(OnGround(at), Vector3.zero, 4.2f, .22f, WithAlpha(c, .75f));
            Scorch(at, 1f, 2.4f);
            int n = Count(9);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                spark.Emit(new Vector3(at.x, .5f, at.z), (d + Vector3.up * R(.1f, .6f)) * R(6f, 13f), R(.05f, .09f), R(.12f, .28f), c);
            }
        }

        public void Death(Vector3 at, float radius, Color body)
        {
            Color c = Color.Lerp(body, new Color(.45f, .1f, .12f), .5f);
            flash.Emit(at, Vector3.zero, radius * 1.8f, .08f, Bright(c, .25f));
            int n = Count(5);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                smokeLow.Emit(at + d * radius * .3f, d * R(1f, 2.6f) + Vector3.up * R(0f, 1f), radius * R(1f, 1.7f), R(.25f, .5f), new Color(c.r * .55f, c.g * .5f, c.b * .55f, .5f), R(0f, 360f));
                spark.Emit(at, (d + Vector3.up * R(.2f, .9f)) * R(4f, 9f), R(.05f, .09f), R(.12f, .3f), Bright(c, .4f));
            }
        }

        public void Swallow(Vector3 at, float size)
        {
            Vector3 air = new(at.x, Query.CastHeight, at.z);
            flash.Emit(air, Vector3.zero, size * 1.8f, .14f, VoidTint);
            twirl.Emit(OnGround(at, .1f), Vector3.zero, size * 2.6f, .35f, WithAlpha(VoidTint, .9f), R(0f, 360f));
            int n = Count(10);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                smoke.Emit(air + d * size * R(.6f, 1.2f), -d * size * R(1f, 2f), size * R(.5f, .9f), R(.25f, .45f), new Color(.1f, .04f, .18f, .7f), R(0f, 360f));
                ember.Emit(air + d * size * R(.8f, 1.6f), -d * size * R(2f, 4f), R(.05f, .11f), R(.2f, .36f), VoidTint);
            }
        }
    }
}
