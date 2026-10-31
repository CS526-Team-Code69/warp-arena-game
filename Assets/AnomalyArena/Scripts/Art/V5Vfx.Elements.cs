using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public sealed partial class V5Vfx
    {

        readonly Dictionary<(Component body, ElementKind element), float> nextTouch = new();

        bool Throttled(Component body, ElementKind element, float interval)
        {
            var key = (body, element);
            if (nextTouch.TryGetValue(key, out float at) && Time.unscaledTime < at) return true;
            if (nextTouch.Count > 512) nextTouch.Clear();
            nextTouch[key] = Time.unscaledTime + interval;
            return false;
        }

        public void ElementTouch(Combatant body, ElementKind element)
        {
            if (!body || Throttled(body, element, .16f)) return;
            float r = Mathf.Max(.4f, body.radius);
            Vector3 at = Query.AtCastHeight(body.Position) + Vector3.up * r * .35f;
            switch (element)
            {
                case ElementKind.Fire:
                    smallFlame.Emit(at, Vector3.up * .6f, r * R(2.1f, 2.9f), R(.28f, .42f), Color.white);
                    for (int i = 0; i < Count(4); i++) ember.Emit(at, new Vector3(R(-2f, 2f), R(1.5f, 3.5f), R(-2f, 2f)), R(.05f, .1f), R(.25f, .5f), FireTint);
                    break;
                case ElementKind.Poison:
                    cloud.Emit(at, Vector3.up * .3f, r * R(1.6f, 2.3f), R(.45f, .7f), WithAlpha(PoisonTint, .42f), R(0f, 360f));
                    for (int i = 0; i < Count(3); i++) bubble.Emit(at + Random.insideUnitSphere * r * .6f, Vector3.up * R(.8f, 1.6f), R(.12f, .26f), R(.35f, .6f), Bright(PoisonTint, .2f));
                    break;
                case ElementKind.Frost:
                    for (int i = 0; i < Count(5); i++)
                    {
                        Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                        glint.Emit(at + d * r * .4f, (d + Vector3.up * R(0f, .6f)) * R(1.5f, 3.5f), R(.16f, .34f), R(.18f, .34f), FrostTint);
                    }
                    cloud.Emit(at, Vector3.zero, r * R(1.4f, 2f), R(.3f, .45f), WithAlpha(FrostTint, .3f), R(0f, 360f));
                    break;
                case ElementKind.Lightning:
                    elec.Emit(at, Vector3.zero, r * R(2f, 2.8f), R(.06f, .1f), ArcTint, R(0f, 360f));
                    Bolt(at + Random.insideUnitSphere * r * 1.2f, at + Random.insideUnitSphere * r * 1.2f, ArcTint, r * .9f, .06f);
                    break;
            }
        }

        public void WielderPay(Combatant body, ElementKind element)
        {
            if (!body || element is not (ElementKind.Earth or ElementKind.Light or ElementKind.Void) || Throttled(body, element, .22f)) return;
            Color c = ElementTint(element);
            Vector3 at = Query.AtCastHeight(body.Position);
            int n = Count(5);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                ember.Emit(at + d * R(1.1f, 1.9f), -d * R(3f, 5f) + Vector3.up * R(0f, 1f), R(.07f, .14f), R(.22f, .38f), c);
            }
            if (element == ElementKind.Earth) groundGlow.Emit(OnGround(at), Vector3.zero, 3f, .22f, WithAlpha(c, .4f));
        }

        public void AffixAura(Enemy enemy, ElementKind element)
        {
            float r = Mathf.Max(.4f, enemy.radius);
            Vector3 at = Query.AtCastHeight(enemy.Position);
            switch (element)
            {
                case ElementKind.Fire:
                    if (Chance(6f)) smallFlame.Emit(at + Random.insideUnitSphere * r * .55f, Vector3.up * R(.2f, .6f), r * R(1.3f, 1.9f), R(.3f, .5f), Color.white);
                    if (Chance(5f)) ember.Emit(at, new Vector3(R(-.6f, .6f), R(1.5f, 3f), R(-.6f, .6f)), R(.05f, .09f), R(.4f, .7f), FireTint);
                    break;
                case ElementKind.Poison:
                    if (Chance(4f)) bubble.Emit(at + Random.insideUnitSphere * r * .7f, Vector3.up * R(.6f, 1.3f), R(.12f, .25f), R(.45f, .75f), Bright(PoisonTint, .2f));
                    if (Chance(2f)) cloud.Emit(at, Vector3.up * .3f, r * R(1.5f, 2.2f), R(.6f, .9f), WithAlpha(PoisonTint, .22f), R(0f, 360f));
                    break;
                case ElementKind.Frost:
                    if (Chance(7f)) glint.Emit(at + Random.insideUnitSphere * r * .9f, Vector3.down * R(.2f, .6f), R(.14f, .3f), R(.3f, .55f), FrostTint);
                    break;
                case ElementKind.Lightning:
                    if (Chance(5f)) elec.Emit(at + Random.insideUnitSphere * r * .5f, Vector3.zero, r * R(1.4f, 2.1f), R(.05f, .09f), ArcTint, R(0f, 360f));
                    break;
                case ElementKind.Earth:
                    if (Chance(3f)) smokeLow.Emit(OnGround(enemy.Position, .3f) + Random.insideUnitSphere * r * .6f, Vector3.up * R(.2f, .5f), r * R(1f, 1.5f), R(.4f, .7f), new Color(.66f, .56f, .42f, .34f), R(0f, 360f));
                    break;
                case ElementKind.Light:
                    if (Chance(6f)) ember.Emit(OnGround(enemy.Position, .3f) + Random.insideUnitSphere * r, Vector3.up * R(1.2f, 2.4f), R(.06f, .12f), R(.4f, .8f), LightTint);
                    break;
                case ElementKind.Void:
                    if (Chance(5f)) smoke.Emit(at + Random.insideUnitSphere * r * .4f, Vector3.up * R(.2f, .5f), r * R(1.2f, 1.8f), R(.35f, .6f), new Color(.14f, .05f, .28f, .6f), R(0f, 360f));
                    if (Chance(4f)) ember.Emit(at, Vector3.up * R(.6f, 1.4f), R(.05f, .09f), R(.3f, .55f), VoidTint);
                    break;
            }
        }

        public void ChargeDust(Enemy enemy)
        {
            Vector3 feet = OnGround(enemy.Position, .3f);
            if (Chance(30f)) smokeLow.Emit(feet + Random.insideUnitSphere * .4f, Random.insideUnitSphere * 1.2f + Vector3.up * .5f, R(.7f, 1.3f), R(.3f, .55f), new Color(.6f, .55f, .47f, .42f), R(0f, 360f));
            if (enemy.Affix != ElementKind.None && Chance(16f)) ember.Emit(feet, Random.insideUnitSphere * 2f + Vector3.up, R(.06f, .11f), R(.25f, .45f), ElementTint(enemy.Affix));
        }

        public void WardLink(Vector3 from, Vector3 to, float radius)
        {
            Color c = new(1f, .84f, .5f);
            Vector3 a = Query.AtCastHeight(from), b = Query.AtCastHeight(to);
            Bolt(a, b, c, .5f, .16f);
            flash.Emit(b, Vector3.zero, Mathf.Max(1f, radius * 2.6f), .14f, WithAlpha(c, .6f));
            ringLayer.Emit(OnGround(to, .09f), Vector3.zero, Mathf.Max(1.2f, radius * 2.8f), .3f, WithAlpha(c, .7f));
        }

        public void CloakPuff(Vector3 at, float radius)
        {
            Vector3 air = Query.AtCastHeight(at);
            for (int i = 0; i < Count(4); i++)
                smoke.Emit(air + Random.insideUnitSphere * radius * .5f, Random.insideUnitSphere * 1.2f, radius * R(1.5f, 2.3f), R(.3f, .5f), new Color(.1f, .05f, .2f, .6f), R(0f, 360f));
            for (int i = 0; i < Count(4); i++) ember.Emit(air, Random.insideUnitSphere * 3f, R(.05f, .09f), R(.2f, .4f), VoidTint);
        }

        public void SpawnPuff(Enemy enemy)
        {
            if (!enemy) return;
            float r = Mathf.Max(.5f, enemy.radius);
            Vector3 at = enemy.Position;
            ringLayer.Emit(OnGround(at, .09f), Vector3.zero, r * 3.4f, .32f, new Color(.72f, .42f, 1f, .75f));
            smoke.Emit(OnGround(at, .9f), Vector3.up * .8f, r * 2.6f, R(.35f, .5f), new Color(.14f, .06f, .26f, .7f), R(0f, 360f));
            groundGlow.Emit(OnGround(at), Vector3.zero, r * 4f, .3f, new Color(.5f, .22f, 1f, .5f));
            for (int i = 0; i < Count(4); i++) ember.Emit(OnGround(at, .4f) + Random.insideUnitSphere * r * .6f, Vector3.up * R(1.5f, 3.5f), R(.05f, .1f), R(.3f, .55f), VoidTint);
        }

        WaveManager subscribedWaves;
        WorldMap subscribedWorld;
        readonly Dictionary<Enemy, bool> cloaks = new();
        readonly Dictionary<Enemy, int> bossPhases = new();

        void HoleFed(BlackHole hole)
        {
            if (!On || !hole) return;
            ringLayer.Emit(OnGround(hole.Centre, .09f), Vector3.zero, hole.KillRadius * 2.1f, .3f, new Color(.85f, .55f, 1f, .9f));
            int n = Count(10);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                ember.Emit(OnGround(hole.Centre, .5f) + d * hole.KillRadius * R(1.1f, 1.7f), -d * hole.KillRadius * R(2f, 4f) + new Vector3(-d.z, 0f, d.x) * R(1f, 3f), R(.07f, .14f), R(.2f, .36f), VoidTint);
            }
        }

        void TickWeaponAndEnemies()
        {
            var gm = GameManager.Instance;
            if (!gm || !Art.Enabled) return;
            if (gm.waves && subscribedWaves != gm.waves)
            {
                subscribedWaves = gm.waves;
                gm.waves.EnemySpawned += enemy =>
                {
                    bossPhases.Remove(enemy);
                    cloaks.Remove(enemy);
                    if (On) SpawnPuff(enemy);
                };
            }
            var world = WorldMap.Active;
            if (world && subscribedWorld != world)
            {
                subscribedWorld = world;
                world.HoleFed += HoleFed;
            }
            TickHeldWeapon(gm);
            if (!gm.IsCombatActive || !gm.waves) return;
            foreach (var enemy in gm.waves.Alive)
            {
                if (!enemy || !enemy.IsAlive || !enemy.definition) continue;
                if (enemy.Charging) ChargeDust(enemy);
                if (enemy.IsBoss)
                {

                    if (bossPhases.TryGetValue(enemy, out int phase) && phase != enemy.BossPhase)
                    {
                        if (enemy.definition.kind == EnemyKind.Hollow) VoidExplosion(enemy.Position, 6f);
                        else GroundSlam(enemy.Position, 7f, new Color(1f, .55f, .25f));
                        gm.Shake(.3f);
                    }
                    bossPhases[enemy] = enemy.BossPhase;
                }
                if (enemy.definition.kind == EnemyKind.Shade)
                {
                    bool cloaked = enemy.Cloaked;
                    if (cloaks.TryGetValue(enemy, out bool was) && was != cloaked) CloakPuff(enemy.Position, enemy.radius);
                    cloaks[enemy] = cloaked;
                }
            }
        }

        void TickHeldWeapon(GameManager gm)
        {
            var weapons = gm.runWeapons;
            var player = gm.player;
            var w = weapons ? weapons.Active : null;
            if (w == null || !player || !player.IsAlive || !gm.IsCombatActive) return;
            Vector3 aim = FlatDir(player.AimDirection, Vector3.forward);
            Vector3 muzzle = Query.AtCastHeight(player.Position) + aim * (player.radius + 1f);
            float t = weapons.Charge01;
            if (w.charging)
            {
                Color c = w.form switch
                {
                    WeaponForm.Railgun => new Color(.55f, .72f, 1f),
                    WeaponForm.ScatterCannon => new Color(1f, .62f, .25f),
                    _ => new Color(.6f, .88f, 1f)
                };
                if (w.element != ElementKind.None) c = ElementTint(w.element);
                ChargeGlow(muzzle, t, c);
            }
            else if (w.form == WeaponForm.ArcCoil && w.variant == 1 && t > .02f)
            {

                Vector3 at = Query.AtCastHeight(player.Position);
                if (Chance(Mathf.Lerp(2f, 22f, t))) elec.Emit(at + Random.insideUnitSphere * .9f, Vector3.zero, R(.9f, 1.5f) * Mathf.Lerp(.7f, 1.4f, t), R(.05f, .09f), ArcTint, R(0f, 360f));
                if (t >= 1f && Chance(10f)) Bolt(at + Random.insideUnitSphere * 1.2f, at + Random.insideUnitSphere * 1.2f, ArcTint, .8f, .06f);
            }
            else if (w.form == WeaponForm.Minigun && t > .35f)
            {

                if (Chance(Mathf.Lerp(2f, 16f, t))) smokeLow.Emit(muzzle, Vector3.up * R(.5f, 1.2f) + aim * .3f, R(.4f, .8f), R(.3f, .6f), new Color(.6f, .58f, .56f, .3f * t), R(0f, 360f));
                if (w.variant == 1 && t > .7f && Chance(14f)) ember.Emit(muzzle, Random.insideUnitSphere * 2f + Vector3.up, R(.05f, .09f), R(.2f, .4f), FireTint);
            }
        }

        void ChargeGlow(Vector3 muzzle, float t, Color c)
        {
            if (Chance(Mathf.Lerp(10f, 40f, t)))
            {
                Vector3 d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * .4f;
                float from = R(1.2f, 2.3f);
                spark.Emit(muzzle + d * from, -d * from * 7f, R(.04f, .08f), .13f, Bright(c, .3f));
            }
            flash.Emit(muzzle, Vector3.zero, Mathf.Lerp(.3f, 1.25f, t), .05f, WithAlpha(c, Mathf.Lerp(.3f, .85f, t)));
            if (t >= 1f && Chance(9f)) star.Emit(muzzle, Vector3.zero, R(1.2f, 1.9f), .1f, Bright(c, .4f), R(0f, 90f));
            if (Chance(8f)) groundGlow.Emit(OnGround(muzzle), Vector3.zero, Mathf.Lerp(1f, 2.6f, t), .12f, WithAlpha(c, Mathf.Lerp(.1f, .42f, t)));
        }

        public void Nova(Vector3 at, float range, Color c)
        {
            Vector3 centre = Query.AtCastHeight(at);
            ringLayer.Emit(OnGround(at, .09f), Vector3.zero, range * 2f, .28f, WithAlpha(c, .9f));
            softRing.Emit(OnGround(at, .085f), Vector3.zero, range * 2.05f, .26f, WithAlpha(c, .3f));
            flash.Emit(centre, Vector3.zero, range * .9f, .08f, WithAlpha(c, .8f));
            int n = Count(Mathf.Clamp(range * 2.2f, 7f, 16f));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 360f / n + R(-12f, 12f), 0f) * Vector3.forward;
                Bolt(centre, centre + d * range * R(.7f, 1f), c, 1.1f, R(.1f, .17f));
            }
        }

        public void CaseEject(Vector3 at, Vector3 aim, bool shotgun = false)
        {
            Vector3 side = new Vector3(-aim.z, 0f, aim.x) * (Random.value < .5f ? -1f : 1f);
            casing.Emit(at, side * R(3.5f, 6.5f) + Vector3.up * R(3f, 6f) - aim * R(.5f, 1.8f), shotgun ? .56f : .42f, R(.42f, .66f), shotgun ? new Color(1f, .58f, .48f) : Color.white);
        }

        public void FireKick(RunWeapon w, PlayerController body, Vector3 aim, HitFeelProfile profile)
        {
            var gm = GameManager.Instance;
            float charge = Mathf.Clamp01(w.charge);
            (float kick, float shake, float recoil, float punch, float size) = (w.form, w.variant) switch
            {
                (WeaponForm.Rifle, 0) => (.12f + .4f * charge, .04f + .14f * charge, .2f + .35f * charge, .004f + .012f * charge, 1.9f + 1.6f * charge),
                (WeaponForm.Rifle, _) => (.09f, 0f, .16f, 0f, 1.7f),
                (WeaponForm.ScatterCannon, 0) => (.56f, .24f, .5f, .017f, 0f),                                   
                (WeaponForm.ScatterCannon, _) => (.16f, .05f, .2f, .002f, 1.9f),
                (WeaponForm.RocketLauncher, 0) => (.32f, .1f, .42f, .006f, 2.6f),
                (WeaponForm.Railgun, 0) => (.6f, .3f, .55f, .02f, 3.2f),
                (WeaponForm.Railgun, _) => (.3f, .1f, .3f, .006f, 2.4f),
                (WeaponForm.Minigun, _) => (.07f, .035f, .08f, 0f, 1.6f),
                (WeaponForm.SwarmLauncher, _) => (.12f, .04f, .14f, 0f, 1.7f),
                (WeaponForm.CryoCannon, 0) => (.1f, .02f, .14f, 0f, 1.6f),
                (WeaponForm.ArcCoil, _) => (.08f, .03f, .1f, 0f, 1.4f),
                (WeaponForm.SporeLauncher, _) => (.07f, 0f, .08f, 0f, 1f),
                _ => (profile.fireKick, 0f, profile.fireRecoil, 0f, profile.muzzleFlash * 1.25f),
            };
            body.VisualKick(-aim, recoil, .12f);
            gm.Kick(-aim, kick);
            if (shake > 0f) gm.Shake(shake);
            if (punch > 0f) gm.Punch(punch);
            Color flashColour = w.element == ElementKind.None ? new Color(1f, .82f, .5f) : ElementTint(w.element);
            if (size > 0f) Muzzle(Query.AtCastHeight(body.Position) + aim * (body.radius + .45f), aim, flashColour, size, w.form is WeaponForm.Rifle or WeaponForm.Minigun or WeaponForm.ScatterCannon);
        }

        public void ScatterShot(Vector3 origin, Vector3 dir, float range, float arcDeg, ElementKind element)
        {
            Vector3 d = FlatDir(dir, Vector3.forward);
            Vector3 at = new Vector3(origin.x, Query.CastHeight, origin.z) + d * 1.1f;
            float half = Mathf.Clamp(arcDeg, 10f, 170f) * .5f;
            Color hot = element == ElementKind.None ? new Color(1f, .86f, .56f) : ElementTint(element);

            float roll = ScreenRoll(d);
            blastFlash.Emit(at + d * 1.3f, d * 5f, 5.2f, .09f, Bright(hot, .3f), -roll);
            flash.Emit(at + d * .4f, Vector3.zero, 3.6f, .07f, hot);
            groundGlow.Emit(OnGround(at + d * range * .32f), Vector3.zero, range * 1.5f, .13f, WithAlpha(hot, .6f));
            for (int i = 0; i < Count(6); i++)
            {
                Vector3 v = Quaternion.Euler(0f, R(-half, half) * .55f, 0f) * d;
                fireball.Emit(at + v * R(.1f, .7f), v * R(13f, 22f), R(.9f, 1.5f), R(.09f, .15f), new Color(hot.r, hot.g * R(.8f, 1f), hot.b * R(.6f, 1f)), R(0f, 360f));
            }
            int pellets = Count(20);
            for (int i = 0; i < pellets; i++)
            {

                float spread = R(-1f, 1f);
                Vector3 v = Quaternion.Euler(0f, spread * Mathf.Abs(spread) * .4f * half + spread * .6f * half, 0f) * d;
                float speed = R(46f, 78f);
                float stop = PelletStop(at, v, range * R(.68f, 1.04f), out bool body);
                pellet.Emit(at + v * R(0f, .6f), v * speed, R(.2f, .3f), Mathf.Max(.02f, stop / speed), Bright(hot, .4f));
                if (pelletHits.Count < 64) pelletHits.Add(new PelletHit { when = Time.unscaledTime + stop / speed, at = at + v * stop, along = v, body = body, tint = hot });
            }
            for (int i = 0; i < Count(4); i++)
            {
                Vector3 v = Quaternion.Euler(0f, R(-half, half) * .7f, 0f) * d;
                smokeLow.Emit(at + v * R(.5f, range * .4f), v * R(2f, 5f) + Vector3.up * .3f, R(1.1f, 1.9f), R(.32f, .55f), new Color(.62f, .6f, .56f, .34f), R(0f, 360f));
            }
            CaseEject(at - d * .7f, d, true);
        }

        struct PelletHit
        {
            public float when;
            public Vector3 at, along;
            public bool body;
            public Color tint;
        }

        readonly List<PelletHit> pelletHits = new();

        static float PelletStop(Vector3 from, Vector3 dir, float reach, out bool body)
        {
            float best = reach;
            body = false;
            if (Query.WallBetween(from, from + dir * reach, out var wall)) best = Mathf.Min(best, wall.distance);
            foreach (var target in Combatant.All)
            {
                if (!target || !target.IsAlive || target is PlayerController) continue;
                Vector3 to = target.Position - from;
                to.y = 0f;
                float along = Vector3.Dot(to, dir);
                if (along <= 0f || along >= best) continue;
                if ((to - dir * along).sqrMagnitude > target.radius * target.radius) continue;
                best = along;
                body = true;
            }
            return best;
        }

        void TickPellets()
        {
            for (int i = pelletHits.Count - 1; i >= 0; i--)
            {
                var hit = pelletHits[i];
                if (Time.unscaledTime < hit.when) continue;
                pelletHits.RemoveAt(i);
                if (!On) continue;
                if (hit.body)
                {
                    flash.Emit(hit.at, Vector3.zero, R(.5f, .8f), .05f, Bright(hit.tint, .3f));
                    for (int n = 0; n < 2; n++) spark.Emit(hit.at, (Quaternion.Euler(0f, R(-50f, 50f), 0f) * hit.along * R(7f, 15f)) + Vector3.up * R(0f, 2f), R(.09f, .15f), R(.1f, .2f), Bright(hit.tint, .35f));
                    droplet.Emit(hit.at, (Quaternion.Euler(0f, R(-35f, 35f), 0f) * hit.along * R(3f, 8f)) + Vector3.up * R(2f, 5f), R(.14f, .26f), R(.28f, .5f), Ichor);
                }
                else
                {

                    smokeLow.Emit(new Vector3(hit.at.x, .4f, hit.at.z), Vector3.up * R(.5f, 1.4f) + hit.along * R(.5f, 1.5f), R(.45f, .8f), R(.2f, .36f), new Color(.66f, .6f, .52f, .42f), R(0f, 360f));
                    if (Random.value < .5f) debris.Emit(new Vector3(hit.at.x, .4f, hit.at.z), Vector3.up * R(2f, 5f) + hit.along * R(-1f, 2f), R(.12f, .22f), R(.25f, .45f), new Color(.5f, .46f, .42f), R(0f, 360f));
                    spark.Emit(new Vector3(hit.at.x, .4f, hit.at.z), Vector3.up * R(2f, 5f) + hit.along * R(1f, 5f), R(.07f, .11f), R(.07f, .14f), Bright(hit.tint, .3f));
                }
            }
        }

        static readonly Color Ichor = new(.42f, .05f, .09f);

        void BlastShake(Vector3 at, float radius)
        {
            var gm = GameManager.Instance;
            if (!gm || !gm.player) return;
            float near = 1f - Mathf.Clamp01(Query.Flat(at - gm.player.Position).magnitude / 34f);
            if (near <= 0f) return;
            gm.Shake(Mathf.Clamp(radius * .055f, .05f, .34f) * near);
            gm.Punch(Mathf.Clamp(radius * .0028f, 0f, .016f) * near);
        }

        float footClock;

        void TickFeet()
        {
            var gm = GameManager.Instance;
            if (!On || !gm || !gm.player || gm.player.stamina == null || !gm.player.stamina.Sprinting) return;
            footClock -= Time.deltaTime;
            if (footClock > 0f) return;
            footClock = .07f;
            Vector3 p = gm.player.Position;
            smokeLow.Emit(new Vector3(p.x + R(-.3f, .3f), .25f, p.z + R(-.3f, .3f)), Vector3.up * R(.2f, .6f), R(.6f, 1.05f), R(.28f, .46f), new Color(.62f, .58f, .5f, .32f), R(0f, 360f));
        }

        public void EarthRise(Vector3 at, Quaternion facing, float width)
        {
            Vector3 along = facing * Vector3.right, side = facing * Vector3.forward;
            for (int k = -1; k <= 1; k++)
                crack.Emit(OnGround(at + along * k * width * .34f, .055f), Vector3.zero, R(5.5f, 7f), 5.5f, new Color(.05f, .035f, .025f, .9f), R(0f, 360f));
            int rocks = Count(width * 5f);
            for (int i = 0; i < rocks; i++)
            {
                Vector3 p = at + along * R(-.5f, .5f) * width;
                debris.Emit(new Vector3(p.x, .7f, p.z), side * R(-3.5f, 3.5f) + along * R(-1.5f, 1.5f) + Vector3.up * R(6f, 13f), R(.3f, .75f), R(.55f, 1f), new Color(1f, .82f, .62f), R(0f, 360f));
            }
            int dust = Count(width * 5f);
            for (int i = 0; i < dust; i++)
            {
                Vector3 p = at + along * R(-.55f, .55f) * width;
                float out_ = Random.value < .5f ? -1f : 1f;
                smokeLow.Emit(new Vector3(p.x, .5f, p.z) + side * out_ * .6f, side * out_ * R(1.5f, 4.5f) + Vector3.up * R(.2f, 1.2f), R(1.1f, 2f), R(.5f, .95f), new Color(.6f, .5f, .4f, .55f), R(0f, 360f));
            }
            var gm = GameManager.Instance;
            if (gm)
            {
                gm.Shake(.26f);
                gm.Punch(.014f);
            }
        }

        public void EarthCrumble(Vector3 at, Quaternion facing, float width)
        {
            Vector3 along = facing * Vector3.right;
            int rocks = Count(width * 4f);
            for (int i = 0; i < rocks; i++)
            {
                Vector3 p = at + along * R(-.5f, .5f) * width;
                debris.Emit(new Vector3(p.x, R(.6f, 2f), p.z), Random.insideUnitSphere * 2.5f + Vector3.up * R(1f, 4f), R(.3f, .7f), R(.4f, .75f), new Color(1f, .82f, .62f), R(0f, 360f));
                smokeLow.Emit(new Vector3(p.x, R(.4f, 1.4f), p.z), Random.insideUnitSphere * 1.2f + Vector3.up * R(.2f, .8f), R(1f, 1.8f), R(.45f, .8f), new Color(.6f, .5f, .4f, .5f), R(0f, 360f));
            }
        }

        public Material EffectMaterial(string texture) => Mat(texture, false, 1f, 0f, 0f);

        readonly List<ParticleSystem> allLayers = new();
        bool layersFrozen;

        public int LiveParticles
        {
            get
            {
                int count = 0;
                foreach (var ps in allLayers) if (ps) count += ps.particleCount;
                return count;
            }
        }

        void TickFreeze()
        {
            var gm = GameManager.Instance;
            bool freeze = gm && Time.timeScale <= 0f && gm.RunPhase is not (ArenaUiState.TrialReturn or ArenaUiState.SupplyDrop);
            if (freeze == layersFrozen) return;
            layersFrozen = freeze;
            foreach (var ps in allLayers)
            {
                if (!ps) continue;
                if (freeze) ps.Pause(true);
                else ps.Play(true);
            }
        }
    }
}
