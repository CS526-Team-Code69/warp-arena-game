using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    [DefaultExecutionOrder(1200)]
    public sealed class V4CombatVfx : MonoBehaviour
    {
        [Tooltip("Saved quads sharing one transparent material; colour and texture go through property blocks.")]
        public Renderer[] items;

        [Header("Element states on bodies")] public Texture[] fireFrames;
        public Texture[] poisonFrames;
        public Texture poisonMist, frostShell, frostRing, frostShard;
        public Texture[] lightningFrames;
        public Texture lightningJolt, slowRing;
        [Header("Player buffs")] public Texture auraOvercharge, auraRadiance, auraTimeSlip, auraShield, auraVoid;

        [Header("Rocket Launcher strike")]
        [Tooltip("Falling rocket wrapped in re-entry plasma; the texture points to +X.")]
        public Texture strikeRocket;

        public Texture strikeTarget, strikeFlash;
        [Min(1f)] public float strikeHeight = 16f;

        [Tooltip(
            "How far behind the target, toward the shooter, the rocket starts, so it comes in on a slant like a meteor.")]
        [Min(0f)]
        public float strikeLead = 7f;

        [Min(.1f)] public float strikeRocketLength = 3.4f;
        [Min(0f)] public float strikeShake = .28f;

        [Header("Enemy attacks")]
        [Tooltip("Spitter poison spike; the texture points to +X with the thorn head at its right end.")]
        public Texture poisonSpike;

        [Min(.1f)] public float spikeLength = 1.3f;

        [Tooltip("Spire prism beam; the texture starts at its left edge and fades out at the right.")]
        public Texture prismBeam;

        [Min(.1f)] public float prismBeamWidth = .9f;
        [Tooltip("Seconds the fired Spire laser stays bright after it lands.")] [Min(.02f)] public float laserFlashSeconds = .18f;
        [Header("Special enemies (document 10.2)")]
        [Tooltip("Bloater radiation zone on the ground; reuses the poison mist when empty.")] public Texture radiationGround;
        [Tooltip("Chanter aura ring under every chanter; reuses the Radiance aura when empty.")] public Texture chantAura;
        [Min(1f)] public float framesPerSecond = 12f;

        [Tooltip("Element layers stop beyond this distance from the player so crowds stay readable and cheap.")]
        [Min(1f)]
        public float maxDistance = 26f;

        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static readonly int TextureId = Shader.PropertyToID("_BaseMap");
        MaterialPropertyBlock block;
        int used;
        Material baseMaterial;

        Texture shadowTexture, softGlow, thinRing, shellTexture, sporeTexture, orbTexture, hexTexture;

        const float RingEdge = 1.075f;
        (Texture frostShell, Texture frostRing, Texture slowRing, Texture auraShield) savedTextures;
        bool v5Textures;
        Transform cameraTransform;
        public static V4CombatVfx Active { get; private set; }

        struct Strike
        {
            public int sequence;
            public float total, progress, lastDelay, nextSpark;
            public Vector3 start, target;
            public float radius;
        }

        readonly Dictionary<AttackActor, Strike> strikes = new();
        readonly List<AttackActor> ended = new();

        readonly Dictionary<Enemy, (int sequence, float at)> lasers = new();
        Renderer[] boltBodies;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (items != null && items.Length > 0 && items[0])
            {
                baseMaterial = items[0].sharedMaterial;

                int saved = items.Length;
                System.Array.Resize(ref items, saved + 160);
                for (int i = saved; i < items.Length; i++)
                {
                    items[i] = Instantiate(items[0], items[0].transform.parent);
                    items[i].enabled = false;
                }
            }
        }

        void SwapTextures(bool v5)
        {
            if (v5 == v5Textures) return;
            v5Textures = v5;
            if (v5)
            {
                savedTextures = (frostShell, frostRing, slowRing, auraShield);
                shadowTexture = V5Vfx.Tex("shadow");
                softGlow = V5Vfx.Tex("glow");
                thinRing = V5Vfx.Tex("ring");
                shellTexture = V5Vfx.Tex("shell");
                sporeTexture = V5Vfx.Tex("spore");
                orbTexture = V5Vfx.Tex("orb");
                hexTexture = V5Vfx.Tex("hex");
                frostShell = V5Vfx.Tex("iceshell");
                frostRing = V5Vfx.Tex("frost");
                slowRing = thinRing;
                auraShield = hexTexture;
            }
            else (frostShell, frostRing, slowRing, auraShield) = savedTextures;
        }

        void OnEnable() => Active = this;

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        public static bool IsRocketStrike(AttackActor actor) =>
            actor && actor.gameObject.activeInHierarchy && actor.mode == AttackMode.Blast && actor.weapon != null
            && actor.weapon.form == WeaponForm.RocketLauncher && actor.weapon.variant == 0 && !actor.chainChild;                                                    

        public static bool DrawsRocketStrike(AttackActor actor) => Active && Active.isActiveAndEnabled &&
                                                                   Active.strikeRocket && Art.Enabled &&
                                                                   IsRocketStrike(actor);

        void OnDisable()
        {
            if (items != null)
                foreach (var item in items)
                    if (item)
                        item.enabled = false;
            ShowBoltBodies();
        }

        void LateUpdate()
        {
            used = 0;
            var gm = GameManager.Instance;
            if (items == null || !gm || !gm.cam)
            {
                HideRest();
                ShowBoltBodies();
                return;
            }

            cameraTransform = gm.cam.transform;
            float time = Time.unscaledTime;
            var player = gm.player;
            SwapTextures(V5Vfx.On);
            if (!Art.Enabled)
            {

                DrawTracers(gm, time);
                DrawWeaponMarks(gm, player, time);
                HideRest();
                ShowBoltBodies();
                return;
            }

            DrawBodyShadows(gm);
            DrawStrikes(gm, time);
            DrawHazards(gm, time);
            DrawBolts(gm);
            DrawRadiation(time);
            DrawSpecialEnemies(gm, time);
            DrawTracers(gm, time);
            DrawWeaponMarks(gm, player, time);
            if (player && player.IsAlive) DrawPlayerBuffs(player, gm, time);
            Vector3 centre = player ? player.Position : cameraTransform.position;
            float maxSqr = maxDistance * maxDistance;
            foreach (var body in Combatant.All)
            {
                if (used >= items.Length) break;
                if (!body || body == (Combatant)player || !body.IsAlive || !body.gameObject.activeInHierarchy) continue;
                if (Query.Flat(body.Position - centre).sqrMagnitude > maxSqr) continue;
                var status = ElementStatus.For(body);
                if (status) DrawStatus(body, status, time);
            }

            HideRest();
        }

        void DrawBodyShadows(GameManager gm)
        {
            if (!v5Textures || !shadowTexture) return;
            Vector3 centre = gm.player ? gm.player.Position : cameraTransform.position;
            float maxSqr = maxDistance * maxDistance * 1.6f;
            foreach (var body in Combatant.All)
            {
                if (used >= items.Length) break;
                if (!body || !body.IsAlive || !body.gameObject.activeInHierarchy) continue;
                if (Query.Flat(body.Position - centre).sqrMagnitude > maxSqr) continue;
                if (body is Enemy { Cloaked: true }) continue;
                Flat(shadowTexture, new Vector3(body.Position.x, .03f, body.Position.z), Mathf.Max(1.5f, body.radius * 3.4f), new Color(0f, 0f, 0f, .62f), 0f);
            }

            if (gm.player && gm.player.IsAlive && softGlow)
            {
                Flat(thinRing, new Vector3(gm.player.Position.x, .05f, gm.player.Position.z), 2.5f, new Color(.4f, .85f, 1f, .3f), 0f, true);
            }
        }

        void DrawStatus(Combatant body, ElementStatus status, float time)
        {
            float r = Mathf.Max(.35f, body.radius) * 1.3f;
            Vector3 feet = new Vector3(body.Position.x, .07f, body.Position.z);
            Vector3 chest = Query.AtCastHeight(body.Position) + Vector3.up * r * .35f;
            float phase = (body.GetHashCode() & 255) * .137f;
            if (v5Textures && V5Vfx.On)
            {
                V5Vfx.I.Status(body, status, r, chest);
                if (status.Frozen) Billboard(frostShell, chest, r * 2.9f, new Color(.9f, .97f, 1f, .94f));
                else if (status.Frost > .05f) Flat(frostRing, feet, r * (2.2f + .9f * status.Chill01), new Color(.72f, .93f, 1f, .25f + .55f * status.Chill01), phase * 90f);
                if (status.Paralysed) Flat(slowRing, feet, r * 2.5f, new Color(1f, .88f, .42f, .38f), -time * 40f, true);

                if (status.Shield > 0f) Billboard(auraShield, chest, r * 2.3f, new Color(1f, .86f, .55f, .13f), true);
                return;
            }
            if (status.Frozen)
                Billboard(frostShell, chest, r * 2.7f, new Color(.75f, .95f, 1f, .9f));
            else if (status.Frost > .05f)
                Flat(frostRing, feet, r * (2.2f + .6f * status.Chill01),
                    new Color(.55f, .9f, 1f, .35f + .55f * status.Chill01), time * 20f + phase * 90f);
            if (status.Fire > .3f)
            {
                float size = r * Mathf.Lerp(1.7f, 2.6f, Mathf.Clamp01(status.Fire / 8f));
                Billboard(Frame(fireFrames, time + phase), chest + Vector3.up * size * .12f, size,
                    new Color(1f, 1f, 1f, .92f));
            }

            if (status.Poison > .5f)
            {
                Billboard(poisonMist, chest, r * 2.6f,
                    new Color(.7f, 1f, .5f, .35f + .25f * Mathf.Clamp01(status.Poison / 12f)));
                Billboard(Frame(poisonFrames, time * .7f + phase), chest + Vector3.up * r * .4f, r * 1.9f,
                    new Color(1f, 1f, 1f, .95f));
            }

            if (status.Paralysed)
            {
                Flat(slowRing, feet, r * 2.5f, new Color(1f, .9f, .45f, .75f), -time * 40f);
                Billboard(Frame(lightningFrames, time * 1.4f + phase), chest, r * 2.4f, new Color(1f, .95f, .6f, .9f));
                if (status.Twitching) Billboard(lightningJolt, chest, r * 3.3f, new Color(1f, .95f, .55f, 1f));
            }
            else if (status.Lightning > .5f)
                Billboard(Frame(lightningFrames, time * 1.4f + phase), chest, r * 2f,
                    new Color(1f, .9f, .5f, .3f + .1f * status.Lightning));
        }

        void DrawStrikes(GameManager gm, float time)
        {
            var attackPool = AttackPool.Instance;
            var fx = FxPool.Active;
            if (attackPool != null && attackPool.actors != null && strikeRocket)
                foreach (var actor in attackPool.actors)
                {
                    if (!IsRocketStrike(actor) || actor.delay <= 0f) continue;
                    if (!strikes.TryGetValue(actor, out var strike) || strike.sequence != actor.Sequence)
                    {
                        Vector3 target = Query.Flat(actor.transform.position) + Vector3.up * .07f;
                        Vector3 back = Query.Flat(actor.direction).sqrMagnitude > .001f
                            ? Query.Flat(actor.direction).normalized
                            : Vector3.forward;
                        strike = new Strike
                        {
                            sequence = actor.Sequence, total = Mathf.Max(.05f, actor.delay), target = target,
                            radius = actor.radius,
                            start = target + Vector3.up * strikeHeight - back * strikeLead
                        };
                    }

                    strike.lastDelay = actor.delay;
                    strike.progress = Mathf.Clamp01(1f - actor.delay / strike.total);
                    float fall = strike.progress * strike.progress;
                    Vector3 position = Vector3.Lerp(strike.start, strike.target, fall);
                    Vector3 velocity = strike.target - strike.start;
                    float pulse = .5f + .5f * Mathf.Sin(time * 18f);
                    if (v5Textures)
                    {

                        Flat(thinRing, strike.target, strike.radius * 2f * RingEdge, new Color(1f, .42f, .18f, Mathf.Lerp(.55f, 1f, strike.progress)), 0f, true);
                        Flat(softGlow, strike.target, strike.radius * 2f * Mathf.Lerp(1.4f, .7f, strike.progress), new Color(1f, .3f, .1f, Mathf.Lerp(.08f, .42f, strike.progress) * (.75f + .25f * pulse)), 0f, true);
                        PlaceStretched(strikeRocket, position, ScreenRoll(velocity), strikeRocketLength, strikeRocketLength * .5f, Color.white);
                        V5Vfx.I.RocketTrail(position - velocity.normalized * strikeRocketLength * .4f, -velocity.normalized);
                        strikes[actor] = strike;
                        continue;
                    }
                    Flat(strikeTarget, strike.target, strike.radius * 2f * Mathf.Lerp(1.6f, 1f, strike.progress),
                        new Color(1f, 1f, 1f, Mathf.Lerp(.45f, .9f, strike.progress) * (.75f + .25f * pulse)),
                        time * 60f);
                    PlaceStretched(strikeRocket, position, ScreenRoll(velocity), strikeRocketLength,
                        strikeRocketLength * .5f, Color.white);
                    if (fx && time >= strike.nextSpark)
                    {
                        strike.nextSpark = time + .035f;
                        Vector3 back = -velocity.normalized;
                        fx.Emit(position + back * strikeRocketLength * .45f, new Color(1f, .62f, .2f, .9f), .55f, .35f,
                            velocity: back * 4f + Random.insideUnitSphere * 2.5f);
                    }

                    strikes[actor] = strike;
                }

            ended.Clear();
            foreach (var pair in strikes)
                if (!pair.Key || !pair.Key.gameObject.activeInHierarchy || pair.Key.Sequence != pair.Value.sequence ||
                    pair.Key.delay <= 0f)
                    ended.Add(pair.Key);
            foreach (var actor in ended)
            {
                var strike = strikes[actor];
                strikes.Remove(actor);

                if (strike.lastDelay <= Mathf.Max(.2f, strike.total * .35f)) Impact(gm, fx, strike);
            }
        }

        void DrawHazards(GameManager gm, float time)
        {
            if (!gm.runWeapons) return;
            var hazards = gm.runWeapons.Hazards;
            for (int i = 0; i < hazards.Count && used < items.Length; i++)
            {
                var h = hazards[i];
                float fade = Mathf.Clamp01(h.timeLeft / Mathf.Max(.01f, Mathf.Min(1f, h.life)));
                bool tamed = h.weapon != null && h.weapon.Has(UpgradeAxis.Tame);
                if (v5Textures && V5Vfx.On && h.kind != GroundHazardKind.IcePatch)
                {
                    var fx5 = V5Vfx.I;
                    if (h.kind == GroundHazardKind.ScatterShell)
                    {
                        float heat = 1f - Mathf.Clamp01(h.timeLeft / Mathf.Max(.01f, h.life));

                        Flat(shellTexture, h.position + Vector3.up * .08f, .72f, new Color(1f, 1f - .25f * heat, 1f - .55f * heat, 1f), i * 71f);
                        Flat(thinRing, h.position + Vector3.up * .06f, h.radius * 2f * RingEdge, new Color(1f, .5f, .2f, .25f + .55f * heat), 0f, true);
                        fx5.HotShell(h.position, heat);
                    }
                    else if (h.kind == GroundHazardKind.Snow)
                    {
                        Flat(frostRing, h.position + Vector3.up * .055f, h.radius * 2.2f, tamed ? new Color(.6f, .8f, 1f, .2f * fade) : new Color(.95f, .98f, 1f, .2f * fade), i * 53f);
                        fx5.FrostField(h.position, h.radius, .4f);
                    }
                    else
                    {
                        Billboard(sporeTexture, h.position + Vector3.up * .5f, Mathf.Clamp(h.radius * 1.1f, .6f, 1.1f), new Color(1f, 1f, 1f, fade));
                        fx5.PoisonField(h.position, h.radius * .8f, .3f);
                    }
                    continue;
                }

                if (h.kind == GroundHazardKind.IcePatch)
                    Flat(frostRing, h.position + Vector3.up * .06f, h.radius * 2.4f,
                        tamed ? new Color(.35f, .55f, 1f, .8f * fade) : new Color(.7f, .95f, 1f, .75f * fade), i * 37f);
                else if (h.kind == GroundHazardKind.ScatterShell)
                {

                    float heat = 1f - Mathf.Clamp01(h.timeLeft / Mathf.Max(.01f, h.life));
                    Flat(slowRing, h.position + Vector3.up * .06f, h.radius * 2f, new Color(1f, .5f, .2f, .25f + .5f * heat), time * 90f);
                    Billboard(Frame(fireFrames, time * 1.5f + i * .3f), h.position + Vector3.up * .3f, .45f + .35f * heat, Color.white);
                }
                else if (h.kind == GroundHazardKind.Snow)

                    Flat(frostRing, h.position + Vector3.up * .055f, h.radius * 2.2f,
                        tamed ? new Color(.6f, .8f, 1f, .5f * fade) : new Color(.95f, .98f, 1f, .5f * fade), i * 53f);
                else
                {
                    Flat(poisonMist, h.position + Vector3.up * .06f, h.radius * 2.6f,
                        new Color(.6f, 1f, .45f, .45f * fade), 0f);
                    Billboard(Frame(poisonFrames, time * .5f + i * .3f), h.position + Vector3.up * .45f,
                        h.radius * 1.6f, new Color(1f, 1f, 1f, fade));
                }
            }
        }

        void DrawRadiation(float time)
        {
            var zones = Enemy.RadiationZones;
            if (zones == null) return;
            var ground = radiationGround ? radiationGround : poisonMist;
            for (int i = 0; i < zones.Count && used < items.Length; i++)
            {
                var zone = zones[i];
                float fade = Mathf.Clamp01(zone.timeLeft / Mathf.Max(.01f, Mathf.Min(1f, zone.life)));
                float pulse = .85f + .15f * Mathf.Sin(time * 9f + i);
                if (v5Textures && V5Vfx.On)
                {
                    V5Vfx.I.Radiation(zone.position, zone.radius, fade);
                    Flat(thinRing, zone.position + Vector3.up * .07f, zone.radius * 2f * RingEdge, new Color(.8f, 1f, .25f, .16f * fade), 0f, true);
                    continue;
                }
                Flat(ground, zone.position + Vector3.up * .06f, zone.radius * 2.3f * pulse, new Color(.95f, 1f, .35f, .5f * fade), time * 12f + i * 51f);
                if (slowRing) Flat(slowRing, zone.position + Vector3.up * .07f, zone.radius * 2f, new Color(1f, .9f, .3f, .35f * fade), -time * 30f);
            }
        }

        void DrawSpecialEnemies(GameManager gm, float time)
        {
            if (!gm.waves) return;
            var aura = chantAura ? chantAura : auraRadiance;
            foreach (var enemy in gm.waves.Alive)
            {
                if (!enemy || !enemy.IsAlive || !enemy.gameObject.activeInHierarchy || !enemy.definition || used >= items.Length) continue;
                if (v5Textures && V5Vfx.On)
                {

                    if (enemy.Affix != ElementKind.None)
                    {
                        V5Vfx.I.AffixAura(enemy, enemy.Affix);
                        Color tint = V5Vfx.ElementTint(enemy.Affix);
                        Flat(softGlow, new Vector3(enemy.Position.x, .05f, enemy.Position.z), Mathf.Max(2f, enemy.radius * 4.2f), new Color(tint.r, tint.g, tint.b, .3f), 0f, true);
                    }
                    if (enemy.Chanted) Flat(softGlow, new Vector3(enemy.Position.x, .05f, enemy.Position.z), Mathf.Max(1.8f, enemy.radius * 3.4f), new Color(.6f, .3f, 1f, .32f), 0f, true);
                }
                if (enemy.definition.kind == EnemyKind.Chanter)
                {
                    float pulse = .5f + .5f * Mathf.Sin(time * 3f);
                    if (v5Textures && V5Vfx.On)
                    {
                        V5Vfx.I.ChantMotes(enemy.Position, Enemy.ChantRadius);
                        Flat(thinRing, new Vector3(enemy.Position.x, .07f, enemy.Position.z), Enemy.ChantRadius * 2f * RingEdge, new Color(.72f, .5f, 1f, .14f + .08f * pulse), 0f, true);
                        Flat(softGlow, new Vector3(enemy.Position.x, .06f, enemy.Position.z), Enemy.ChantRadius * 2.1f, new Color(.45f, .22f, .9f, .08f + .05f * pulse), 0f, true);
                        continue;
                    }
                    Flat(aura, new Vector3(enemy.Position.x, .07f, enemy.Position.z), Enemy.ChantRadius * 2f * (.96f + .04f * pulse),
                        new Color(.8f, .55f, 1f, .22f + .12f * pulse), time * 20f);
                }
                else if (enemy.definition.kind == EnemyKind.Spire && prismBeam)
                {
                    bool fired = lasers.TryGetValue(enemy, out var last) && last.sequence == enemy.LaserSequence && time - last.at < laserFlashSeconds;
                    if (!lasers.TryGetValue(enemy, out last) || last.sequence != enemy.LaserSequence)
                    { lasers[enemy] = (enemy.LaserSequence, time); fired = enemy.LaserSequence > 0; }
                    if (!enemy.LaserLocking && !fired) continue;
                    Vector3 direction = Query.Flat(enemy.LaserDirection).sqrMagnitude > .001f ? Query.Flat(enemy.LaserDirection).normalized : Vector3.forward;
                    float length = Mathf.Max(1f, enemy.LaserLength);
                    Vector3 from = Query.AtCastHeight(enemy.Position) + direction * enemy.radius;

                    float width = fired ? prismBeamWidth : prismBeamWidth * .22f;
                    Color color = fired ? new Color(1f, .55f, .35f, .85f) : new Color(1f, .3f, .25f, .5f);
                    PlaceStretched(prismBeam, from + direction * length * .5f, ScreenRoll(direction), length, width, color, v5Textures);
                }
            }
            if (lasers.Count > 64) lasers.Clear();
        }

        void DrawTracers(GameManager gm, float time)
        {
            if (!gm.runWeapons || !prismBeam) return;
            var tracers = gm.runWeapons.Tracers;
            for (int i = 0; i < tracers.Count && used < items.Length; i++)
            {
                var tracer = tracers[i];
                if (time > tracer.until || tracer.drawnByV5) continue;
                Vector3 line = Query.Flat(tracer.to - tracer.from);
                float length = line.magnitude;
                if (length < .05f) continue;
                Vector3 direction = line / length;
                PlaceStretched(prismBeam, tracer.from + direction * length * .5f, ScreenRoll(direction), length, tracer.width * (v5Textures ? 2.2f : 1f), tracer.color, v5Textures);
            }
        }

        void DrawWeaponMarks(GameManager gm, Combatant player, float time)
        {
            var weapons = gm.runWeapons;
            if (!weapons) return;
            if (slowRing)
                foreach (var body in Combatant.All)
                {
                    if (used >= items.Length) break;
                    if (!body || !body.IsAlive || !weapons.IsChargeSlowed(body)) continue;
                    Flat(slowRing, new Vector3(body.Position.x, .075f, body.Position.z), Mathf.Max(.35f, body.radius) * 2.6f,
                        new Color(.55f, .85f, 1f, .7f), -time * 60f);
                }

            if (player && player.IsAlive && weapons.FocusGuardActive)
                Billboard(auraShield, Query.AtCastHeight(player.Position) + Vector3.up * .3f, 2.4f, new Color(.85f, .9f, 1f, .6f));
            if (player && player.IsAlive && weapons.SnowGuardActive)
                Billboard(auraShield, Query.AtCastHeight(player.Position) + Vector3.up * .3f, 2.2f, new Color(.6f, .85f, 1f, .45f));
        }

        void DrawBolts(GameManager gm)
        {
            var bolts = gm.waves ? gm.waves.preparedBolts : null;
            if (bolts == null) return;
            if (boltBodies == null || boltBodies.Length != bolts.Length)
            {
                ShowBoltBodies();
                boltBodies = new Renderer[bolts.Length];
                for (int i = 0; i < bolts.Length; i++)
                    boltBodies[i] = bolts[i] ? bolts[i].GetComponent<Renderer>() : null;
            }

            for (int i = 0; i < bolts.Length; i++)
            {
                var bolt = bolts[i];
                bool drawn = poisonSpike && bolt && bolt.gameObject.activeInHierarchy && bolt.IsPoisonSpike &&
                             used < items.Length;

                bool orb = v5Textures && orbTexture && V5Vfx.On && bolt && bolt.gameObject.activeInHierarchy && !bolt.IsPoisonSpike && used < items.Length;
                if (orb)
                {
                    Color shot = bolt.IsBossShot ? new Color(.82f, .5f, 1f) : bolt.Affix != ElementKind.None ? V5Vfx.ElementTint(bolt.Affix) : new Color(.6f, 1f, .4f);
                    Billboard(orbTexture, bolt.transform.position, 1.15f, shot, true);
                    Billboard(orbTexture, bolt.transform.position, .42f, Color.white, true);
                    V5Vfx.I.ShotMote(bolt.transform.position, shot);
                    drawn = true;
                }
                else if (drawn && v5Textures && bolt.Affix != ElementKind.None)
                {

                    V5Vfx.I.ShotMote(bolt.transform.position, V5Vfx.ElementTint(bolt.Affix));
                    Vector3 direction = bolt.Direction;
                    PlaceStretched(poisonSpike, bolt.transform.position - direction * spikeLength * .24f, ScreenRoll(direction), spikeLength, spikeLength * .375f, Color.white);
                }
                else if (drawn)
                {
                    Vector3 direction = bolt.Direction;

                    PlaceStretched(poisonSpike, bolt.transform.position - direction * spikeLength * .24f,
                        ScreenRoll(direction),
                        spikeLength, spikeLength * .375f, Color.white);
                }

                if (boltBodies[i] && boltBodies[i].enabled == drawn) boltBodies[i].enabled = !drawn;
            }
        }

        void ShowBoltBodies()
        {
            if (boltBodies == null) return;
            foreach (var body in boltBodies)
                if (body && !body.enabled)
                    body.enabled = true;
        }

        void Impact(GameManager gm, FxPool fx, Strike strike)
        {

            if (fx && !V5Vfx.On)
            {
                if (strikeFlash)
                    fx.Emit(strike.target + Vector3.up * 1.1f, Color.white, strike.radius * 2.8f, .34f,
                        texture: strikeFlash, pulse: true);
                if (fx.hitRing)
                    fx.Emit(strike.target, new Color(1f, .55f, .2f, .9f), strike.radius * 2.3f, .38f,
                        texture: fx.hitRing, flat: true, pulse: true);
                for (int i = 0; i < 10; i++)
                {
                    Vector3 direction = Quaternion.Euler(0f, i * 36f + Random.Range(-12f, 12f), 0f) * Vector3.forward;
                    if (fx.hitSparks != null && fx.hitSparks.Length > 0)
                        fx.EmitDirectional(fx.hitSparks[i % fx.hitSparks.Length],
                            strike.target + Vector3.up * .5f + direction * strike.radius * .4f, direction,
                            strike.radius * 1.4f, .22f, new Color(1f, .7f, .3f));
                }
            }

            if (strikeShake > 0f) gm.Shake(strikeShake);
        }

        float ScreenRoll(Vector3 worldDirection)
        {
            if (!cameraTransform || worldDirection.sqrMagnitude < .0001f) return 0f;
            var local = cameraTransform.InverseTransformDirection(worldDirection);
            return Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        }

        void PlaceStretched(Texture texture, Vector3 at, float roll, float width, float height, Color color, bool additive = false)
        {
            if (!texture || used >= items.Length) return;
            Place(texture, at, cameraTransform.rotation * Quaternion.Euler(0f, 0f, roll), 1f, color, additive);
            items[used - 1].transform.localScale = new Vector3(width, height, 1f);
        }

        void DrawPlayerBuffs(Combatant player, GameManager gm, float time)
        {
            var skills = gm.skills;
            Vector3 feet = new Vector3(player.Position.x, .08f, player.Position.z);
            Vector3 chest = Query.AtCastHeight(player.Position) + Vector3.up * .3f;
            float pulse = .5f + .5f * Mathf.Sin(time * 6f);
            if (v5Textures && V5Vfx.On)
            {
                var fx5 = V5Vfx.I;
                if (skills && skills.TimeSlipActive)
                {
                    Flat(thinRing, feet, 3.2f + .3f * pulse, new Color(.6f, .82f, 1f, .6f), 0f, true);
                    fx5.BuffMotes(feet, 1.8f, new Color(.55f, .8f, 1f), 10f, .8f);
                }
                if (skills && skills.OverchargeActive)
                {
                    Flat(softGlow, feet, 3.4f, new Color(1f, .6f, .2f, .32f + .1f * pulse), 0f, true);
                    fx5.OverchargeTick(chest, .9f);
                }
                if (skills && skills.RadianceActive)
                {
                    Flat(softGlow, feet, 4.2f + .3f * pulse, new Color(1f, .86f, .45f, .36f), 0f, true);
                    fx5.BuffMotes(feet, 1.2f, new Color(1f, .9f, .55f), 18f);
                }
                if (skills && skills.VoidWalkActive)
                {
                    Flat(softGlow, feet, 3.4f, new Color(.42f, .16f, .9f, .4f), 0f, true);
                    fx5.VoidSmoke(chest);
                }
                var board = ElementStatus.For(player);
                if (board && board.Shield > 0f)
                {
                    float share5 = Mathf.Clamp01(board.Shield / Mathf.Max(1f, player.maxHp * (board.Rules ? board.Rules.playerShieldCapShare : .5f)));
                    Billboard(auraShield, chest, 2.7f, new Color(1f, .9f, .68f, .06f + .2f * share5), true);
                }
                return;
            }
            if (skills && skills.TimeSlipActive)
                Flat(auraTimeSlip, feet, 5.5f, new Color(.7f, .85f, 1f, .55f + .2f * pulse), -time * 25f);
            if (skills && skills.OverchargeActive)
                Flat(auraOvercharge, feet, 2.6f + .25f * pulse, new Color(1f, .9f, .45f, .95f), time * 160f);
            if (skills && skills.RadianceActive)
                Flat(auraRadiance, feet, 3.2f + .2f * pulse, new Color(1f, .96f, .7f, .85f), time * 30f);
            if (skills && skills.VoidWalkActive) Flat(auraVoid, feet, 3f, new Color(.85f, .6f, 1f, .9f), -time * 90f);
            var status = ElementStatus.For(player);
            if (status && status.Shield > 0f)
            {
                float share = Mathf.Clamp01(status.Shield / Mathf.Max(1f,
                    player.maxHp * (status.Rules ? status.Rules.playerShieldCapShare : .5f)));
                Billboard(auraShield, chest, 2.6f, new Color(1f, .93f, .75f, .35f + .5f * share));
            }
        }

        Texture Frame(Texture[] frames, float time)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[Mathf.Abs((int)(time * framesPerSecond)) % frames.Length];
        }

        void Billboard(Texture texture, Vector3 at, float size, Color color, bool additive = false)
        {
            if (!texture || used >= items.Length) return;

            Vector3 toCamera = (cameraTransform.position - at).normalized;
            Place(texture, at + toCamera * size * .35f, cameraTransform.rotation, size, color, additive);
        }

        void Flat(Texture texture, Vector3 at, float size, Color color, float angle, bool additive = false)
        {
            if (!texture || used >= items.Length) return;
            Place(texture, at, Quaternion.Euler(90f, angle, 0f), size, color, additive);
        }

        void Place(Texture texture, Vector3 at, Quaternion rotation, float size, Color color, bool additive = false)
        {
            var item = items[used++];
            if (!item) return;
            var wanted = additive && V5Vfx.I ? V5Vfx.I.QuadAdditive : baseMaterial;
            if (wanted && item.sharedMaterial != wanted) item.sharedMaterial = wanted;
            var t = item.transform;
            t.SetPositionAndRotation(at, rotation);
            t.localScale = new Vector3(size, size, 1f);
            block.Clear();
            block.SetColor(ColorId, color);
            block.SetTexture(TextureId, texture);
            item.SetPropertyBlock(block);
            if (!item.enabled) item.enabled = true;
        }

        void HideRest()
        {
            if (items == null) return;
            for (int i = used; i < items.Length; i++)
                if (items[i] && items[i].enabled)
                    items[i].enabled = false;
        }
    }
}