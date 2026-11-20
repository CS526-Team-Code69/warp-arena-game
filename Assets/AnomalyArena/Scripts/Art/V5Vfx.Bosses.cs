using UnityEngine;

namespace AnomalyArena
{
    public sealed partial class V5Vfx
    {
        // ───── The Tyrant's blows (Enemy.Tyrant.cs, V2TyrantGroundMarks) ─────

        // Dust and rock pale enough to read against the desert's sand (the debris picture is dark, so its tint is near white).
        static readonly Color TyrantHeat = new(1f, .6f, .25f), RockDust = new(.93f, .87f, .77f, .5f), RockChip = new(1f, .9f, .74f),
            CrackInk = new(.12f, .08f, .06f, .85f);

        /// <summary>Tail Sweep, every frame of the sweep: the arc the tail crossed since the last frame burns. Fire clings to the outer part
        /// of the tail's path and drifts the way it swings, a hot slash follows the tail, the tip scrapes up dust and flings sparks ahead of
        /// it, and a scorch line is left on the ground.</summary>
        public void TyrantSweepTrail(Vector3 centre, Vector3 from, Vector3 to, float reach)
        {
            centre = Query.Flat(centre);
            Vector3 a = FlatDir(from, Vector3.forward), b = FlatDir(to, a);
            float turned = Vector3.SignedAngle(a, b, Vector3.up), sign = turned < 0f ? -1f : 1f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(turned) / 5f), 1, 12);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 d = Quaternion.Euler(0f, turned * i / steps, 0f) * a;
                Vector3 swing = Vector3.Cross(Vector3.up, d) * sign;
                Vector3 tip = centre + d * reach;
                float angle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float r = reach * R(.6f, 1f);
                Vector3 at = centre + d * r;
                fireball.Emit(new Vector3(at.x, R(.3f, .6f), at.z), swing * R(1f, 2.5f) + Vector3.up * R(.6f, 1.4f), R(1.2f, 1.7f) * (r / reach),
                    R(.2f, .3f), new Color(1f, .8f, .55f, .9f), R(0f, 360f));
                slash.Emit(OnGround(centre + d * reach * R(.55f, .85f), .3f), Vector3.zero, reach * R(.65f, .9f), R(.12f, .18f),
                    new Color(1f, .72f, .38f, .8f), angle);
                spark.Emit(new Vector3(tip.x, .55f, tip.z), swing * R(7f, 13f) + d * R(-1f, 4f) + Vector3.up * R(1f, 4f), R(.07f, .12f), R(.18f, .36f),
                    new Color(1f, .78f, .42f));
                if (i % 2 == 0 || steps == 1)
                    smokeLow.Emit(OnGround(centre + d * reach * R(.75f, 1f), .35f), d * R(1.5f, 3f) + swing * R(1f, 2.5f), R(1.2f, 2f), R(.4f, .7f),
                        RockDust, R(0f, 360f));
                if (i % 3 == 0 || steps == 1)
                    decal.Emit(OnGround(centre + d * reach * R(.78f, .95f), .045f), Vector3.zero, R(1.1f, 1.7f), 2.6f, new Color(.1f, .07f, .05f, .45f), R(0f, 360f));
            }
        }

        /// <summary>Ground Pound: the shockwave reaching one band of its rectangle (length along facing, width across). The ground breaks
        /// across the band: a line of light, rock thrown up along it in a wall of dust, cracks under a hot glow and sparks; the first band,
        /// under the pound itself, also flashes, rings and shakes the view harder.</summary>
        public void TyrantPoundBand(Vector3 centre, Vector3 facing, float length, float width, bool first)
        {
            Vector3 f = FlatDir(facing, Vector3.forward), side = Vector3.Cross(Vector3.up, f);
            float angle = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, size = Mathf.Max(width, length);
            slash.Emit(OnGround(centre, .25f), Vector3.zero, width * 1.05f, .22f, new Color(1f, .7f, .35f, .95f), angle + 90f);
            groundGlow.Emit(OnGround(centre), Vector3.zero, size * 1.6f, .45f, WithAlpha(TyrantHeat, .65f));
            crack.Emit(OnGround(centre, .05f), Vector3.zero, size * 1.15f, 3.4f, CrackInk, angle + R(-25f, 25f));
            int n = Count(Mathf.Clamp(width * 2.2f, 6f, 14f));
            for (int i = 0; i < n; i++)
            {
                float across = (i + .5f) / n - .5f;
                Vector3 ground = OnGround(centre + side * (across * width) + f * (R(-.4f, .4f) * length), .3f);
                if (i % 2 == 0)
                    fireball.Emit(ground + Vector3.up * .3f, f * R(.5f, 1.5f) + Vector3.up * R(1.5f, 3f), R(1.1f, 1.7f), R(.3f, .45f), new Color(1f, .8f, .55f, .95f), R(0f, 360f));
                debris.Emit(ground, f * R(.5f, 2.5f) + side * (across * R(0f, 2f)) + Vector3.up * R(5f, 9f), R(.35f, .6f), R(.6f, 1f), RockChip, R(0f, 360f));
                cloud.Emit(ground + Vector3.up * .4f, f * R(.6f, 1.6f) + Vector3.up * R(.8f, 1.8f), R(1.4f, 2.2f), R(.55f, .9f), RockDust, R(0f, 360f));
                if (i % 2 == 0) spark.Emit(ground, f * R(1f, 3f) + side * R(-1.5f, 1.5f) + Vector3.up * R(4f, 8f), R(.08f, .13f), R(.2f, .4f), Bright(TyrantHeat, .3f));
            }

            var gm = GameManager.Instance;
            if (gm) gm.Shake(first ? .22f : .07f);
            if (!first) return;
            flash.Emit(OnGround(centre, .7f), Vector3.zero, width * 1.5f, .1f, new Color(1f, .9f, .75f));
            ringLayer.Emit(OnGround(centre, .09f), Vector3.zero, width * 1.7f, .3f, WithAlpha(Bright(TyrantHeat, .3f), .9f), R(0f, 360f));
            softRing.Emit(OnGround(centre, .085f), Vector3.zero, width * 2.3f, .36f, new Color(.85f, .68f, .5f, .32f));
        }

        /// <summary>The Rampage Charge's landing, the heaviest blow: a white flash, a bright ring at the slam's exact reach with a wider
        /// one and a soft shock behind it, the ground cracking open under a hot glow, a ring of dust rolling out, rock, sparks and embers
        /// thrown up, and a shake.</summary>
        public void TyrantSlam(Vector3 at, float radius)
        {
            radius = Mathf.Max(.5f, radius);
            Vector3 ground = OnGround(at, .4f);
            flash.Emit(OnGround(at, 1f), Vector3.zero, radius * 1.6f, .12f, new Color(1f, .92f, .8f));
            star.Emit(OnGround(at, .9f), Vector3.zero, radius * 1.3f, .16f, Bright(TyrantHeat, .5f), R(0f, 90f));
            ringLayer.Emit(OnGround(at, .09f), Vector3.zero, radius * 2f, .32f, WithAlpha(Bright(TyrantHeat, .35f), 1f), R(0f, 360f));
            ringLayer.Emit(OnGround(at, .095f), Vector3.zero, radius * 2.7f, .5f, WithAlpha(TyrantHeat, .45f), R(0f, 360f));
            softRing.Emit(OnGround(at, .085f), Vector3.zero, radius * 3f, .45f, new Color(.85f, .7f, .55f, .35f));
            groundGlow.Emit(OnGround(at), Vector3.zero, radius * 2.4f, .6f, WithAlpha(TyrantHeat, .55f));
            crack.Emit(OnGround(at, .05f), Vector3.zero, radius * 2.2f, 4.5f, CrackInk, R(0f, 360f));
            decal.Emit(OnGround(at, .045f), Vector3.zero, radius * 1.9f, 4f, new Color(.08f, .06f, .05f, .55f), R(0f, 360f));
            int n = Count(Mathf.Clamp(radius * 7f, 16f, 36f));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 360f / n + R(-6f, 6f), 0f) * Vector3.forward;
                smokeLow.Emit(ground + d * radius * .3f, d * radius * R(2.4f, 3.6f) + Vector3.up * R(0f, .8f), radius * R(.45f, .7f), R(.55f, .95f),
                    RockDust, R(0f, 360f));
                if (i % 2 == 0)
                    debris.Emit(ground + d * radius * .2f, (d * R(.6f, 1.3f) + Vector3.up * R(1f, 1.8f)) * radius * R(1.6f, 2.6f), R(.2f, .42f), R(.55f, .95f),
                        RockChip, R(0f, 360f));
                if (i % 3 == 0) spark.Emit(ground, (d + Vector3.up * R(.4f, 1.2f)) * radius * R(3f, 5.5f), R(.09f, .15f), R(.25f, .5f), Bright(TyrantHeat, .3f));
            }

            int embers = Count(8);
            for (int i = 0; i < embers; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius * .8f;
                ember.Emit(ground + new Vector3(o.x, 0f, o.y), new Vector3(o.x * .3f, R(2f, 3.5f), o.y * .3f), R(.08f, .16f), R(.6f, 1.1f), TyrantHeat);
            }

            var gm = GameManager.Instance;
            if (gm) gm.Shake(Mathf.Clamp(radius * .08f, .2f, .36f));
        }

        /// <summary>The Star Throw's meteor striking: the fire of an explosion, then what the rock does to the ground: a crater's cracks, a
        /// ring at the exact reach with a wider one and a soft shock, rock flung out and a skirt of dust.</summary>
        public void TyrantMeteorImpact(Vector3 at, float radius)
        {
            radius = Mathf.Max(.5f, radius);
            Explosion(at, radius);
            crack.Emit(OnGround(at, .05f), Vector3.zero, radius * 2.4f, 5f, CrackInk, R(0f, 360f));
            ringLayer.Emit(OnGround(at, .095f), Vector3.zero, radius * 2.8f, .55f, WithAlpha(TyrantHeat, .5f), R(0f, 360f));
            softRing.Emit(OnGround(at, .085f), Vector3.zero, radius * 3.2f, .5f, new Color(1f, .7f, .45f, .35f));
            int n = Count(Mathf.Clamp(radius * 4f, 10f, 22f));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                debris.Emit(OnGround(at, .5f), (d * R(.8f, 1.4f) + Vector3.up * R(1.2f, 2.2f)) * radius * R(1.5f, 2.6f), R(.25f, .5f), R(.7f, 1.1f),
                    new Color(.4f, .34f, .3f), R(0f, 360f));
                smokeLow.Emit(OnGround(at, .4f) + d * radius * .5f, d * radius * R(1.6f, 2.8f), radius * R(.4f, .65f), R(.6f, 1f),
                    new Color(.45f, .4f, .35f, .45f), R(0f, 360f));
            }
        }

        // ───── A boss's change of phase (V5BossPhaseFx) ─────

        Layer hexShell;

        /// <summary>A shield's hex shell around a body, flashed when a hit glances off it.</summary>
        Layer HexShell => hexShell ??= Make("HexShell", Mat("hex", true, 1.6f, 0f, 1.2f),
            new LayerShape { mode = ParticleSystemRenderMode.Billboard, size = Curve(.94f, 1.06f), fade = Fade(0f, .15f), order = 5, max = 24 });

        /// <summary>Gathering, every frame until the burst (t from 0 to 1): motes and streaks drawn in from a wide ring to the chest, the
        /// ground under the boss glowing brighter and wider, a slow swirl under it, dust pulled in, and light swelling at its chest.</summary>
        public void PhaseGather(Vector3 feet, Vector3 chest, float bodyRadius, float t, Color tint)
        {
            t = Mathf.Clamp01(t);
            groundGlow.Emit(OnGround(feet), Vector3.zero, bodyRadius * 2.2f + 4f * t, .12f, WithAlpha(tint, .2f + .4f * t));
            float rate = Mathf.Lerp(45f, 130f, t);
            for (int k = 0; k < 3; k++)
            {
                if (!Chance(rate / 3f)) continue;
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                Vector3 from = feet + d * (bodyRadius + R(3f, 6f)) + Vector3.up * R(.2f, 2.4f);
                float life = R(.3f, .5f);
                ember.Emit(from, (chest - from) / life, R(.12f, .22f), life, Bright(tint, .3f));
                if (Random.value < .5f) spark.Emit(from, (chest - from) / life, R(.06f, .1f), life, Bright(tint, .5f));
            }

            if (Chance(6f + 6f * t)) twirl.Emit(OnGround(feet, .1f), Vector3.zero, bodyRadius * 2f + 3f, R(.3f, .45f), WithAlpha(tint, .25f + .45f * t), R(0f, 360f));
            if (Chance(6f + 10f * t))
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                smokeLow.Emit(OnGround(feet + d * (bodyRadius + R(2f, 4.5f)), .3f), -d * R(2f, 4f), R(1f, 1.6f), R(.4f, .6f), WithAlpha(RockDust, .3f), R(0f, 360f));
            }

            if (Chance(14f + 24f * t))
                energy.Emit(chest, Vector3.zero, (1.8f + 3.2f * t) * Mathf.Max(1f, bodyRadius * .5f), R(.22f, .32f), WithAlpha(Bright(tint, .2f), .9f), R(0f, 360f));
        }

        /// <summary>The burst: a flash and a star at the chest, a bright ring out to the reach that throws the player back with a wider one
        /// and a soft shock behind it, a glow on the ground, cracks under the boss, a ring of dust, rock and sparks thrown out, and embers
        /// rising off it.</summary>
        public void PhaseBurst(Vector3 feet, Vector3 chest, float bodyRadius, float reach, Color tint)
        {
            Vector3 ground = OnGround(feet, .4f);
            flash.Emit(chest, Vector3.zero, reach * 1.4f, .16f, WithAlpha(Bright(tint, .6f), 1f));
            star.Emit(chest, Vector3.zero, reach * 1.3f, .24f, Bright(tint, .55f), R(0f, 90f));
            energy.Emit(chest, Vector3.up * .3f, reach * 1.6f, .6f, tint, R(0f, 360f));
            ringLayer.Emit(OnGround(feet, .09f), Vector3.zero, reach * 2f, .4f, WithAlpha(Bright(tint, .4f), 1f), R(0f, 360f));
            ringLayer.Emit(OnGround(feet, .095f), Vector3.zero, reach * 3f, .65f, WithAlpha(tint, .5f), R(0f, 360f));
            softRing.Emit(OnGround(feet, .085f), Vector3.zero, reach * 3.4f, .55f, WithAlpha(tint, .4f));
            groundGlow.Emit(OnGround(feet), Vector3.zero, reach * 2.6f, .6f, WithAlpha(tint, .6f));
            crack.Emit(OnGround(feet, .05f), Vector3.zero, bodyRadius * 2f + 4f, 4.5f, CrackInk, R(0f, 360f));
            int n = Count(28);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 360f / n + R(-5f, 5f), 0f) * Vector3.forward;
                smokeLow.Emit(ground + d * bodyRadius, d * R(7f, 11f) + Vector3.up * R(0f, .8f), R(1.2f, 2.1f), R(.5f, .85f), RockDust, R(0f, 360f));
                spark.Emit(chest, (d + Vector3.up * R(-.15f, .6f)) * R(10f, 18f), R(.09f, .15f), R(.25f, .5f), Bright(tint, .45f));
                if (i % 2 == 0) debris.Emit(ground + d * bodyRadius * .8f, (d * R(.8f, 1.4f) + Vector3.up * R(.8f, 1.6f)) * R(4f, 7f), R(.18f, .36f), R(.6f, 1f), RockChip, R(0f, 360f));
            }

            int embers = Count(16);
            for (int i = 0; i < embers; i++)
            {
                Vector3 o = Random.insideUnitSphere * bodyRadius;
                ember.Emit(chest + o, new Vector3(o.x, 0f, o.z) * 2f + Vector3.up * R(2f, 4.5f), R(.1f, .2f), R(.8f, 1.4f), tint);
            }
        }

        /// <summary>A hit turned away during a change: the hex shell flashes around the body, with a glint where the hit glanced off.</summary>
        public void PhaseGuard(Vector3 chest, float bodyRadius, float height, Color tint)
        {
            float size = Mathf.Max(bodyRadius * 2.6f, height * 1.15f);
            HexShell.Emit(chest, Vector3.zero, size, .22f, WithAlpha(Bright(tint, .55f), .55f), R(0f, 360f));
            glint.Emit(chest + Random.onUnitSphere * size * .42f, Vector3.zero, R(.5f, .8f), .22f, Bright(tint, .7f), R(0f, 90f));
        }

        /// <summary>A later phase's air, every frame: a few embers rising off the boss and a faint glow under it; strength 1 in the second
        /// phase, more in a third and while a change runs after its burst.</summary>
        public void PhaseAura(Vector3 feet, float bodyRadius, float height, Color tint, float strength)
        {
            if (Chance(9f * strength))
            {
                Vector2 o = Random.insideUnitCircle * bodyRadius * .9f;
                ember.Emit(feet + new Vector3(o.x, R(.3f, height), o.y), new Vector3(R(-.3f, .3f), R(1.2f, 2.4f), R(-.3f, .3f)), R(.07f, .14f), R(.6f, 1.1f), tint);
            }

            if (Chance(3f * strength)) groundGlow.Emit(OnGround(feet), Vector3.zero, bodyRadius * 2.6f, .6f, WithAlpha(tint, Mathf.Min(.4f, .14f * strength)));
        }
    }
}
