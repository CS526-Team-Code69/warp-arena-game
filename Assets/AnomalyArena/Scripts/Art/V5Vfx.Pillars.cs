using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The V5 layer's share of the Nash Prophet's void pillars. V2VoidPillarFx draws their shapes (the circle, its clock, the
    /// column); these recipes add what only particles can: sparks drawn into a warning circle, the burst of the instant a pillar comes
    /// down, streaks and arcs on a standing one (its rising motes are PillarTick's), and the last motes of one that is over. Apart from
    /// the strike's flash and shock, which pass and are gone, everything stays inside the circle: its edge is the whole of the danger.</summary>
    public sealed partial class V5Vfx
    {
        static readonly Color PillarHot = new(.93f, .82f, 1f), PillarDeep = new(.5f, .22f, 1f), PillarSmoke = new(.08f, .03f, .16f, .7f);

        /// <summary>A warning circle, each frame: sparks swept along the ground from its edge toward its middle and the void's swirl
        /// turning inside it, more of both as the warning runs out (warned from 0 to 1).</summary>
        public void PillarGather(Vector3 centre, float radius, float warned)
        {
            if (Chance(Mathf.Lerp(8f, 30f, warned)))
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 d = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 at = centre + d * radius * R(.75f, .98f) + Vector3.up * R(.15f, .45f);
                Vector3 inward = -d * radius * R(1.6f, 2.6f) + new Vector3(-d.z, 0f, d.x) * radius * R(.5f, 1f);
                spark.Emit(at, inward + Vector3.up * R(0f, 1.5f), R(.05f, .09f), R(.25f, .42f), Bright(VoidTint, .25f * warned));
            }
            if (Chance(Mathf.Lerp(1.5f, 5f, warned)))
                twirl.Emit(OnGround(centre, .085f), Vector3.zero, radius * R(1.1f, 1.7f), R(.3f, .45f), WithAlpha(PillarDeep, .3f + .4f * warned), R(0f, 360f));
        }

        /// <summary>The instant a pillar comes down: a white-violet flash and star at its foot, a burst of the void's energy, light
        /// thrown on the ground, a soft shock running out past the circle, sparks off its edge, motes shot up the column, dark smoke
        /// rolling out at the foot, and a scorch that outlasts the pillar by a moment. life is how long the pillar will stand.</summary>
        public void PillarStrike(Vector3 centre, float radius, float life)
        {
            Vector3 foot = new(centre.x, Mathf.Min(1f, radius * .4f), centre.z);
            flash.Emit(foot, Vector3.zero, radius * 2.2f, .13f, WithAlpha(PillarHot, .8f));
            star.Emit(foot, Vector3.zero, radius * 2.2f, .16f, PillarHot, R(0f, 90f));
            energy.Emit(foot + Vector3.up * radius * .2f, Vector3.up * .6f, radius * 2.2f, R(.42f, .5f), VoidTint, R(0f, 360f));
            groundGlow.Emit(OnGround(centre), Vector3.zero, radius * 3.2f, .34f, WithAlpha(PillarDeep, .6f));
            softRing.Emit(OnGround(centre, .08f), Vector3.zero, radius * 3.2f, .34f, WithAlpha(PillarDeep, .45f));
            Scorch(centre, radius * .8f, Mathf.Max(0f, life) + 1.5f);
            int n = Count(Mathf.Clamp(radius * 6f, 8f, 16f));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 360f / n + R(-10f, 10f), 0f) * Vector3.forward;
                spark.Emit(centre + d * radius * .9f + Vector3.up * .3f, (d * R(.6f, 1.4f) + Vector3.up * R(.3f, 1.1f)) * radius * R(2.5f, 4.5f), R(.07f, .12f), R(.2f, .42f), Bright(VoidTint, .35f));
                if (i % 2 == 0) ember.Emit(centre + d * radius * R(.2f, .9f) + Vector3.up * .3f, Vector3.up * R(5f, 11f), R(.08f, .16f), R(.35f, .7f), PillarHot);
                if (i % 3 == 0) smoke.Emit(foot + d * radius * .5f, d * radius * R(.5f, 1.1f) + Vector3.up * R(.2f, .8f), radius * R(.5f, .8f), R(.4f, .7f), PillarSmoke, R(0f, 360f));
            }
        }

        /// <summary>A standing pillar, each frame, beside PillarTick's motes: streaks of light running up its wall at its exact radius,
        /// now and then an arc across it, and the light it throws on the ground.</summary>
        public void PillarClimb(Vector3 centre, float radius, float height)
        {
            if (Chance(20f))
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 d = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                spark.Emit(centre + d * radius * R(.9f, 1f) + Vector3.up * R(.1f, height * .25f), Vector3.up * R(9f, 16f) + new Vector3(-d.z, 0f, d.x) * R(1f, 3f), R(.06f, .1f), R(.25f, .5f), Bright(VoidTint, .3f));
            }
            if (Chance(2.5f))
                elec.Emit(centre + Vector3.up * R(.6f, height * .4f) + Random.insideUnitSphere * radius * .15f, Vector3.zero, radius * R(1.1f, 1.5f), R(.06f, .1f), PillarHot, R(0f, 360f));
            if (Chance(3f)) groundGlow.Emit(OnGround(centre), Vector3.zero, radius * 2.8f, .3f, WithAlpha(PillarDeep, .35f));
        }

        /// <summary>A pillar that is over: a few slow motes left hanging where it stood and a breath of dark smoke, while its column
        /// thins out.</summary>
        public void PillarFade(Vector3 centre, float radius, float height)
        {
            int n = Count(8);
            for (int i = 0; i < n; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius * .8f;
                ember.Emit(new Vector3(centre.x + o.x, R(.3f, height * .5f), centre.z + o.y), Vector3.up * R(.6f, 1.8f), R(.06f, .13f), R(.5f, .9f), VoidTint);
            }
            smoke.Emit(new Vector3(centre.x, 1f, centre.z), Vector3.up * .6f, radius * 1.2f, R(.4f, .55f), WithAlpha(PillarSmoke, .4f), R(0f, 360f));
        }
    }
}
