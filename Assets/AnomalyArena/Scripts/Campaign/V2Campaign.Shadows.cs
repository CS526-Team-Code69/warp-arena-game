using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The night's shadow monsters (V2 doc section 5). Every region of the map holds some at night, and a region gets more for the
    /// morning's kills there; those are dark doubles of the monsters that were killed. A shadow that notices the player comes for them a
    /// little faster than the player walks, so only a sprint opens the distance. Light burns shadows: one inside a lamp's light or the
    /// base's loses itself second by second, fades and leaves by the nearest edge, and once it has taken rules.shadowLightSeconds of light
    /// in all (it does not heal during the night) it dissolves. So they wait at a light's edge. A lamp's ring turns from its warm colour
    /// toward violet with each shadow that gathers at it (CampaignRules.LampCrowdShare), and the goal panel's note names them once
    /// rules.lampCrowdNotice gather at the player's lamp (V2Campaign.Night.cs). Once a crowd of them (rules.overrunShadows) waits at one
    /// lamp it shows itself for rules.lampCrowdWarning seconds, then they go in together, attack and wear the lamp down while it burns
    /// them: a crowd that wears it out first puts it out, and a lamp whose crowd burned away or left shines again. The base's light is
    /// never put out.
    /// Weapons and skills work at night and pass through shadows: only light hurts one.
    /// The tutorial night's shadows hunt wherever the player goes, a little slower than a walk, and touch for a share of a normal hit
    /// (rules.tutorialShadowHitScale), so the first night shows that a touch hurts; they never join a crowd at a lamp, and light burns
    /// them like any other. The first of them to reach a lamp's edge while the player stands in its light says that light burns them.
    /// </summary>
    public sealed partial class V2Campaign
    {
        sealed class Shadow
        {
            public Vector3 position, home, wander; public int region; public Transform view; public Animator animator;
            public float hitAt, wanderAt, bob, side, rippleUntil;
            public bool moving, alert;
            /// <summary>The lamp at whose edge this shadow waits (it counts toward that lamp's crowd), or null.</summary>
            public Lamp waitingAt;
            /// <summary>One of the tutorial night's: it hunts wherever the player goes, a little slower than a walk, touches lightly and
            /// never joins a crowd.</summary>
            public bool tutorial;
            /// <summary>Seconds of light it has taken this night (CampaignRules.ShadowExposure): it never heals, and at
            /// rules.shadowLightSeconds it dissolves (BurnAway).</summary>
            public float burned;
            /// <summary>Its surfaces, drawn see-through from the moment light first touched it (BeginBurn), which of them are its eyes,
            /// and its height above its feet, where its smoke rises from.</summary>
            public Renderer[] burnParts; public bool[] burnEyes; public float height;
        }

        /// <summary>The morning's kills by region in the order they fell: the shadows those kills raise there copy their looks.</summary>
        readonly Dictionary<int, List<EnemyKind>> regionKindKills = new();
        static readonly Color ShadowBody = new(.035f, .02f, .06f), ShadowGlow = new(.3f, .1f, .5f), ShadowEye = new(.75f, .3f, 1f);
        static readonly int ShadowBaseColorId = Shader.PropertyToID("_BaseColor"), ShadowEmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int ShadowRunState = Animator.StringToHash("Base Layer.Run"), ShadowIdleState = Animator.StringToHash("Base Layer.Idle");
        // A crowd inside a lamp's light wears the lamp down this fast per shadow (lampHp is the seconds one shadow needs at rate 1).
        const float CrowdDrain = .5f;
        // Shadows farther than this from the player stand still as far as their animation goes (the map holds dozens of them).
        const float ShadowAnimationReach = 45f;
        // Shadows keep this far from one another, so a pack closes in as a ring of bodies and not as one heap.
        const float ShadowSpacing = 1.25f;
        // A crowd's touches land one after another, this many seconds apart, and not all in one instant.
        const float SwarmHitGap = .35f;
        // A tutorial night shadow hunts at this share of the player's walk, so walking away always gets a new player back to the light
        // (the night's lamp step lets the player walk: SpotlightTutorial.Allowed).
        const float TutorialChaseShare = .9f;
        // A shadow this far inside a light's edge burns; one held on the edge itself (KeptOutOf) does not.
        const float BurnInset = .05f;
        // A burning shadow's body: its alpha when the light first touches it and when it is all but gone, and its colour, from near black
        // toward a thin violet smoke. Its eyes dim faster than its body.
        const float BurnAlphaStart = .9f, BurnAlphaEnd = .08f;
        static readonly Color BurnBody = new(.06f, .03f, .1f), BurnSmoke = new(.16f, .07f, .24f);
        // A shadow dissolving sounds this softly (its sound is short, and only one plays at a time).
        const float BurnSoundVolume = .4f;
        // The whitebox's wisps, drawn where the illustrated art's smoke is not (FxPool).
        static readonly Color WhiteboxWisp = new(.45f, .22f, .7f, .75f);
        float swarmHitAt;
        // Shadows raised this night, those the light has burned away included.
        int shadowsRaised;
        // That weapons pass through shadows has been said in this run: the first ripple says it (RippleUnderFire).
        bool shadowFireSaid;
        // The tutorial night's "light burns them, so they keep to its edge" was said; a shadow this far beyond a lamp's edge stands at it.
        bool tutEdgeSaid;
        const float TutorialEdgeSeen = 1.6f;
        MaterialPropertyBlock shadowBlock;

        void NoteKillKind(int region, Enemy enemy)
        {
            if (!regionKindKills.TryGetValue(region, out var kinds)) regionKindKills[region] = kinds = new List<EnemyKind>();
            kinds.Add(enemy.definition ? enemy.definition.kind : EnemyKind.Grunt);
        }

        int KillsIn(int region) => regionKills.TryGetValue(region, out int n) ? n : 0;

        void SpawnShadows()
        {
            regionShadows.Clear();
            if (Day == 0)
            {
                // The tutorial night shows a few shadows far from the base and from its dark matter; they come for the player like any
                // night's (TickShadows), only slower and with a lighter touch.
                for (int i = 0; i < rules.tutorialShadows; i++)
                {
                    float a = (i + .5f) * Mathf.PI * 2f / Mathf.Max(1, rules.tutorialShadows);
                    AddShadow(FindOpen(BaseCentre + new Vector3(Mathf.Cos(a) * 34f, 0f, Mathf.Sin(a) * 24f), .6f), null).tutorial = true;
                }
                return;
            }
            int total = 0, ambient = rules.AmbientShadows(Day);
            // The regions with the most kills first: the cap on all shadows then cuts the quiet regions' share, not theirs.
            var order = new List<int>();
            for (int r = 0; r < RegionCount; r++) order.Add(r);
            order.Sort((a, b) => KillsIn(b).CompareTo(KillsIn(a)));
            float keepOut = rules.baseLightRadius + 4f;
            foreach (int region in order)
            {
                regionKindKills.TryGetValue(region, out var kinds);
                int fromKills = rules.KillShadows(KillsIn(region));
                int count = Mathf.Min(fromKills + ambient, rules.maxShadows - total);
                if (count <= 0) continue;
                regionShadows[region] = count;
                total += count;
                var centre = RegionCentre(region, out var half);
                for (int i = 0; i < count; i++)
                {
                    Vector3 at = centre + new Vector3(Random.Range(-half.x * .9f, half.x * .9f), 0f, Random.Range(-half.y * .9f, half.y * .9f));
                    // None starts in the base's light or right at its edge.
                    Vector3 fromBase = Query.Flat(at - BaseCentre);
                    if (fromBase.magnitude < keepOut)
                        at = BaseCentre + (fromBase.sqrMagnitude > .01f ? fromBase.normalized : Vector3.right) * (keepOut + Random.Range(0f, 6f));
                    if (WorldMap.Active) at = WorldMap.Active.ClampPoint(at, 1f);
                    // A shadow raised by kills is the double of one of the monsters killed here: every killsPerShadow-th, in the order they fell.
                    EnemyKind? kind = null;
                    if (i < fromKills && kinds != null && kinds.Count > 0)
                        kind = kinds[Mathf.Min(kinds.Count - 1, Mathf.FloorToInt(i * Mathf.Max(1f, rules.killsPerShadow)))];
                    AddShadow(at, kind).region = region;
                }
            }
        }

        Shadow AddShadow(Vector3 at, EnemyKind? kind)
        {
            var s = new Shadow { position = at, home = at, wander = at, region = RegionOf(at), bob = Random.value * 6f, side = Random.value < .5f ? -1f : 1f };
            var root = new GameObject("Shadow").transform;
            root.SetParent(worldRoot, false);
            root.position = at;
            if (!(kind.HasValue && BuildKindShadow(root, kind.Value, s))) BuildPlainShadow(root);
            s.view = root;
            shadows.Add(s);
            shadowsRaised++;
            return s;
        }

        /// <summary>The dark double of a day monster: its whitebox look, or with the art on a copy of its model running as it ran, every
        /// surface turned near black with a faint violet glow. False when that kind has no look to copy (the plain shadow stands in).</summary>
        bool BuildKindShadow(Transform root, EnemyKind kind, Shadow s)
        {
            if (!Art.Enabled)
            {
                string look = WhiteboxCharacter.LookIdFor(kind);
                if (!Whitebox.Has(look)) return false;
                Whitebox.Build(look, root, "ShadowWhitebox");
                Darken(root);
                AddEyes(root);
                return true;
            }
            // Any prepared body of that kind, living or waiting in the pool, lends its model.
            Enemy body = null;
            if (gm.waves && gm.waves.preparedEnemies != null)
                foreach (var enemy in gm.waves.preparedEnemies)
                    if (enemy && enemy.definition && enemy.definition.kind == kind) { body = enemy; break; }
            var presentation = body ? body.GetComponentInChildren<V2ModelPresentation>(true) : null;
            if (!presentation || !presentation.modelRoot) return false;
            var model = Instantiate(presentation.modelRoot.gameObject, root);
            model.name = "ShadowModel";
            // The body it is copied from may lie dead in the pool: on its side, raised or swollen. The double stands as the model was made.
            model.transform.localPosition = presentation.authoredPosition;
            model.transform.localRotation = Quaternion.Euler(presentation.rotationOffsetEuler);
            model.transform.localScale = presentation.authoredScale;
            model.SetActive(true);
            StripColliders(model);
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(behaviour);
            s.animator = model.GetComponentInChildren<Animator>(true);
            if (s.animator)
            {
                s.animator.enabled = true;
                s.animator.applyRootMotion = false;
                s.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                s.animator.speed = 1f; // a pooled body's animation stands still
                s.animator.Rebind();
                s.animator.Play(ShadowIdleState, 0, Random.value);
            }
            Darken(root);
            return true;
        }

        /// <summary>The shadow that is nobody's double: one of the three shadow models, the whitebox table's three shadows, or the old cube.</summary>
        void BuildPlainShadow(Transform root)
        {
            var looks = art && Art.Enabled ? art.shadowPrefabs : null;
            if (looks != null && looks.Length > 0 && looks[Random.Range(0, looks.Length)] is { } look && look)
            {
                var model = Instantiate(look, root);
                StripColliders(model);
                FitHeight(model.transform, Random.Range(1.8f, 2.2f));
            }
            else if (!Art.Enabled)
            {
                // The whitebox table's three shadows: a tall thin one, a squat broad one, a small quick-looking one.
                Whitebox.Build(Random.Range(0, 3) switch { 0 => "shadow.a", 1 => "shadow.b", _ => "shadow.c" }, root, "ShadowWhitebox");
            }
            else
            {
                // The illustrated art without its shadow models: the old cube bodies, told apart by build and tint.
                int lookIndex = Random.Range(0, 3);
                Vector3 size = lookIndex switch { 0 => new Vector3(.75f, 1.9f, .75f), 1 => new Vector3(1.15f, 1.3f, 1.15f), _ => new Vector3(.6f, 1.2f, .6f) };
                Color tint = lookIndex switch { 0 => new Color(.04f, .03f, .07f), 1 => new Color(.09f, .03f, .12f), _ => new Color(.02f, .05f, .09f) };
                Box(root, "Body", new Vector3(0f, size.y * .5f, 0f), size, tint);
                Box(root, "EyeL", new Vector3(-.15f, size.y - .3f, size.z * .5f + .01f), new Vector3(.12f, .06f, .05f), ShadowEye);
                Box(root, "EyeR", new Vector3(.15f, size.y - .3f, size.z * .5f + .01f), new Vector3(.12f, .06f, .05f), ShadowEye);
            }
        }

        /// <summary>Turns every surface under root near black with a faint violet glow, through property blocks: the shared materials stay
        /// as the day's monsters use them.</summary>
        void Darken(Transform root)
        {
            shadowBlock ??= new MaterialPropertyBlock();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.GetPropertyBlock(shadowBlock);
                shadowBlock.SetColor(ShadowBaseColorId, ShadowBody);
                shadowBlock.SetColor(ShadowEmissionId, ShadowGlow);
                r.SetPropertyBlock(shadowBlock);
            }
        }

        /// <summary>Two violet eyes on the front of a whitebox double, a little under its top, so that it reads as a shadow in the dark.</summary>
        void AddEyes(Transform root)
        {
            bool any = false;
            var bounds = new Bounds(root.position, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (any) bounds.Encapsulate(r.bounds); else { bounds = r.bounds; any = true; }
            }
            if (!any) return;
            float top = bounds.max.y - root.position.y, front = bounds.max.z - root.position.z, gap = Mathf.Clamp(bounds.extents.x * .35f, .1f, .3f);
            Box(root, "EyeL", new Vector3(-gap, Mathf.Max(.2f, top - .3f), front + .03f), new Vector3(.2f, .1f, .06f), ShadowEye);
            Box(root, "EyeR", new Vector3(gap, Mathf.Max(.2f, top - .3f), front + .03f), new Vector3(.2f, .1f, .06f), ShadowEye);
        }

        void TickShadows(float dt)
        {
            var p = gm.player;
            if (!p) return;
            Vector3 pp = Query.Flat(p.Position);
            bool away = WorldMap.Active && WorldMap.Active.TrialActive; // in the void trial, off the map
            float lampRadius = LampRadius, lampSq = lampRadius * lampRadius;

            // Each lamp's crowd: the shadows at its edge or inside it. A crowd goes in, and a lamp a crowd went into shelters no more
            // while that crowd is in it.
            float band = lampRadius + rules.lampGatherBand;
            foreach (var lamp in lamps)
            {
                int crowd = 0, inside = 0;
                foreach (var s in shadows)
                {
                    if (s.tutorial) continue;
                    float apart = Query.Flat(s.position - lamp.position).sqrMagnitude;
                    if (apart <= band * band) crowd++;
                    if (apart <= lampSq) inside++;
                }
                lamp.crowd = crowd;
                if (lamp.smothered)
                {
                    // The light burned the crowd away, or it left after the player, before the lamp went out: worn as they left it,
                    // the lamp shines and shelters again.
                    if (inside == 0 && !rules.Crowd(crowd)) { lamp.smothered = false; lamp.crowdSince = 0f; DrawLampEdge(lamp); }
                    continue;
                }
                if (!rules.Crowd(crowd))
                {
                    // The crowd thinned before it went in: the lamp holds.
                    if (lamp.crowdSince > 0f) { lamp.crowdSince = 0f; DrawLampEdge(lamp); }
                }
                else if (lamp.crowdSince <= 0f)
                {
                    lamp.crowdSince = Time.time;
                    DrawLampEdge(lamp);
                    Fx.Ring(lamp.position, LampRadius + rules.lampGatherBand, ShadowEye, .6f);
                    // The crowd's low drone with its whispers (CampaignAudio plays it at most every few seconds).
                    CampaignAudio.PlayAt(CampaignSfx.ZoneOverrunWarning, lamp.position);
                }
                // A crowd shows itself for rules.lampCrowdWarning seconds (the ring violet, the lamp flickering), then goes in.
                else if (Time.time - lamp.crowdSince >= rules.lampCrowdWarning) Smother(lamp);
                // The ring turns toward violet as the shadows gather and back as they thin (drawn again only when their number changed).
                if (lamp.drawnCrowd != crowd) DrawLampEdge(lamp);
            }

            // What shelters the player now: the base's light, or a lamp no crowd has gone into.
            bool inBase = InBaseLight(pp);
            Lamp shelter = null;
            if (!inBase)
                foreach (var lamp in lamps)
                    if (!lamp.smothered && Query.Flat(pp - lamp.position).sqrMagnitude <= lampSq) { shelter = lamp; break; }
            bool sheltered = inBase || shelter != null;
            float chase = rules.ShadowChaseSpeed(p.moveSpeed);

            foreach (var s in shadows)
            {
                Vector3 to = Query.Flat(pp - s.position);
                float toPlayer = to.magnitude;
                // Every shadow hunts inside its own territory only; the tutorial night's hunt wherever the player goes.
                bool noticed = !away && p.IsAlive && (s.tutorial ? toPlayer <= rules.shadowSense : rules.ShadowHunts(toPlayer, Query.Flat(pp - s.home).magnitude));
                s.waitingAt = null;
                s.alert = noticed;
                Vector3 target;
                float speed;
                if (!noticed)
                {
                    // Nothing to hunt: it roams round the place it rose at.
                    if (Time.time >= s.wanderAt || Query.Flat(s.wander - s.position).magnitude < .5f)
                    {
                        s.wanderAt = Time.time + Random.Range(2f, 4f);
                        s.wander = s.home + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
                    }
                    target = s.wander;
                    speed = rules.ShadowRoamSpeed(p.moveSpeed);
                }
                else if (sheltered)
                {
                    // The player stands in light: it comes to the light's edge on its own side and waits there, drifting along the edge.
                    Vector3 centre = inBase ? BaseCentre : shelter.position;
                    float radius = inBase ? rules.baseLightRadius : lampRadius;
                    Vector3 from = Query.Flat(s.position - centre);
                    float angle = Mathf.Atan2(from.z, from.x) + s.side * .12f;
                    target = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius + .9f);
                    speed = Query.Flat(target - s.position).magnitude > 2.5f ? chase : rules.shadowSpeed * .35f;
                    if (!inBase && !s.tutorial) s.waitingAt = shelter;
                    // The tutorial night: the first shadow at a lamp's edge while the player stands in its light shows what the light
                    // does: it burns them, so they keep to its edge (that night's shadows never crowd a lamp, so the light holds).
                    if (s.tutorial && !inBase && !tutEdgeSaid && Query.Flat(s.position - centre).magnitude <= radius + TutorialEdgeSeen)
                    {
                        tutEdgeSaid = true;
                        ui.Flash(ArenaText.Pair("影子怕光：一进到光里就会被灼伤，几秒内就会消散，所以它们只在光圈外面打转。",
                            "Shadows fear the light: inside it they burn away within seconds, so they keep to its edge."), 4.5f);
                    }
                }
                else
                {
                    // The player stands in the dark: straight for them, a little to its own side, so that several close in from all round.
                    // The tutorial night's are a little slower than a walk.
                    Vector3 sideways = toPlayer > .01f ? new Vector3(-to.z, 0f, to.x) / toPlayer : Vector3.zero;
                    target = pp + sideways * (s.side * Mathf.Clamp(toPlayer * .35f, 0f, 3f));
                    speed = s.tutorial ? Mathf.Min(chase, p.moveSpeed * TutorialChaseShare) : chase;
                }

                Vector3 step = Query.Flat(target - s.position);
                float length = step.magnitude;
                step = length > .05f ? step * (Mathf.Min(length, speed * dt) / length) : Vector3.zero;
                Vector3 next = KeptOutOfLight(s.position, KeptApart(s, s.position + step), chase * dt);
                if (WorldMap.Active) next = WorldMap.Active.ClampPoint(next, .5f);
                Vector3 moved = next - s.position;
                s.position = next;

                // Light burns a shadow: what it takes is lost for the night, the body fades with it, and at rules.shadowLightSeconds the
                // shadow dissolves (the pass after this loop). KeptOutOfLight above takes one caught inside out by the nearest edge; a
                // crowd that went into a lamp burns there while it wears the lamp down.
                if (BurnsShadow(s.position))
                {
                    s.burned = CampaignRules.ShadowExposure(s.burned, true, dt);
                    ShowBurn(s);
                }

                // Inside a lamp a crowd went into, every shadow wears the lamp down.
                if (!s.tutorial)
                    foreach (var lamp in lamps)
                        if (lamp.smothered && Query.Flat(lamp.position - s.position).sqrMagnitude <= lampSq) lamp.hp -= dt * CrowdDrain;

                if (s.view)
                {
                    s.bob += dt * 3f;
                    s.view.position = s.position + (s.animator ? Vector3.zero : Vector3.up * (.15f + .1f * Mathf.Sin(s.bob)));
                    bool moving = moved.sqrMagnitude > 1e-6f && moved.magnitude > rules.shadowSpeed * .5f * dt;
                    Vector3 face = moving ? moved : noticed && toPlayer > .1f ? to : Vector3.zero;
                    if (face.sqrMagnitude > 1e-6f) s.view.rotation = Quaternion.Slerp(s.view.rotation, Quaternion.LookRotation(face.normalized), dt * 8f);
                    if (s.animator)
                    {
                        bool near = toPlayer <= ShadowAnimationReach;
                        if (s.animator.enabled != near) s.animator.enabled = near;
                        if (near && moving != s.moving)
                        {
                            s.moving = moving;
                            s.animator.CrossFadeInFixedTime(moving ? ShadowRunState : ShadowIdleState, .18f, 0);
                        }
                    }
                }

                if (!sheltered && noticed && toPlayer < .9f + p.Radius && Time.time >= s.hitAt && Time.time >= swarmHitAt)
                {
                    s.hitAt = Time.time + rules.shadowHitInterval;
                    swarmHitAt = Time.time + SwarmHitGap;
                    HitPlayerAtNight(s);
                }
            }
            for (int i = shadows.Count - 1; i >= 0; i--)
                if (rules.ShadowBurnedAway(shadows[i].burned)) BurnAway(i);
            RippleUnderFire();
        }

        /// <summary>Where a shadow that wants to step from one point to the next ends up: no shadow stays in the base's light, or in a
        /// lamp's light before a crowd has gone into it. One caught inside (a lamp was set down beside it) leaves straight out by the
        /// nearest edge, faster than it hunts, and burns until it is out (BurnsShadow); one that would step in slides along the edge
        /// instead.</summary>
        Vector3 KeptOutOfLight(Vector3 from, Vector3 next, float flee)
        {
            next = KeptOutOf(BaseCentre, rules.baseLightRadius, from, next, flee);
            foreach (var lamp in lamps)
                if (!lamp.smothered) next = KeptOutOf(lamp.position, LampRadius, from, next, flee);
            return next;
        }

        /// <summary>Shadows do not stand in one another: one that would end up closer than ShadowSpacing to another is pushed off it.</summary>
        Vector3 KeptApart(Shadow self, Vector3 next)
        {
            foreach (var other in shadows)
            {
                if (other == self) continue;
                Vector3 d = Query.Flat(next - other.position);
                float distance = d.magnitude;
                if (distance >= ShadowSpacing) continue;
                Vector3 away = distance > .01f ? d / distance : new Vector3(Mathf.Cos(self.bob), 0f, Mathf.Sin(self.bob));
                next += away * ((ShadowSpacing - distance) * .5f);
            }
            return next;
        }

        /// <summary>Where a step from one point to the next ends up beside one circle of light: unchanged outside it; from well inside,
        /// straight out at flee x 1.6; from its edge, along the edge by the step's whole length, to the side the step leans to, so a
        /// shadow making for something behind the light walks round it instead of pressing on one spot of the edge.</summary>
        public static Vector3 KeptOutOf(Vector3 centre, float radius, Vector3 from, Vector3 next, float flee)
        {
            Vector3 d = Query.Flat(next - centre);
            if (d.sqrMagnitude >= radius * radius) return next;
            Vector3 was = Query.Flat(from - centre);
            Vector3 outward = was.sqrMagnitude > 1e-4f ? was.normalized : d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.right;
            if (was.magnitude < radius - .3f) return from + outward * flee * 1.6f;
            Vector3 step = Query.Flat(next - from);
            Vector3 tangent = new(-outward.z, 0f, outward.x);
            float lean = Vector3.Dot(step, tangent);
            if (Mathf.Abs(lean) < 1e-5f) return centre + outward * radius;
            Vector3 slid = Query.Flat(from + tangent * (Mathf.Sign(lean) * step.magnitude) - centre);
            return centre + (slid.sqrMagnitude > 1e-6f ? slid.normalized : outward) * radius;
        }

        /// <summary>A crowd has gathered at this lamp: from now on its light holds no shadow back, and they wear it down.</summary>
        void Smother(Lamp lamp)
        {
            lamp.smothered = true;
            Fx.Ring(lamp.position, LampRadius, ShadowEye, .5f);
            CampaignAudio.PlayAt(CampaignSfx.LightFlicker, lamp.position);
            DrawLampEdge(lamp);
        }

        /// <summary>A crowd of this many shadows that goes into a lamp together wears it out in this many seconds (lampHp at CrowdDrain
        /// per shadow); the light burns them away in rules.shadowLightSeconds meanwhile.</summary>
        public static float LampSecondsAgainst(float lampHp, int crowd) => crowd > 0 ? lampHp / (crowd * CrowdDrain) : float.PositiveInfinity;

        /// <summary>The point lies in light that burns a shadow: the base's light or any lamp's, a lamp a crowd has gone into too (it
        /// still shines while they wear it down). A shadow held on the edge itself (KeptOutOf) is outside.</summary>
        bool BurnsShadow(Vector3 point)
        {
            float baseEdge = rules.baseLightRadius - BurnInset, lampEdge = LampRadius - BurnInset;
            if (Query.Flat(point - BaseCentre).sqrMagnitude < baseEdge * baseEdge) return true;
            foreach (var lamp in lamps)
                if (Query.Flat(point - lamp.position).sqrMagnitude < lampEdge * lampEdge) return true;
            return false;
        }

        /// <summary>The first touch of light: from now on every surface of the shadow is drawn with the see-through material (ShowBurn
        /// sets its colour and alpha), and its height is noted for its smoke.</summary>
        void BeginBurn(Shadow s)
        {
            var parts = new List<Renderer>();
            if (s.view)
                foreach (var r in s.view.GetComponentsInChildren<Renderer>(true))
                    if (r is MeshRenderer or SkinnedMeshRenderer) parts.Add(r);
            s.burnParts = parts.ToArray();
            s.burnEyes = new bool[s.burnParts.Length];
            var glass = Whitebox.SeeThrough;
            float top = 1f;
            for (int i = 0; i < s.burnParts.Length; i++)
            {
                var r = s.burnParts[i];
                s.burnEyes[i] = r.name.StartsWith("Eye", System.StringComparison.Ordinal);
                if (r.enabled) top = Mathf.Max(top, r.bounds.max.y - s.view.position.y);
                var materials = r.sharedMaterials;
                for (int m = 0; m < materials.Length; m++) materials[m] = glass;
                r.sharedMaterials = materials;
            }
            s.height = Mathf.Min(top, 3f);
        }

        /// <summary>A burning shadow fades with what the light has taken: its body thins from near black toward a faint violet smoke and
        /// its eyes dim faster; with the illustrated art a thin wisp leaves it now and then.</summary>
        void ShowBurn(Shadow s)
        {
            if (s.burnParts == null) BeginBurn(s);
            float t = rules.ShadowBurnShare(s.burned);
            Color body = Color.Lerp(BurnBody, BurnSmoke, t), eyes = ShadowEye;
            body.a = Mathf.Lerp(BurnAlphaStart, BurnAlphaEnd, t);
            eyes.a = (1f - t) * (1f - t);
            shadowBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < s.burnParts.Length; i++)
            {
                var r = s.burnParts[i];
                if (!r) continue;
                shadowBlock.Clear();
                shadowBlock.SetColor(ShadowBaseColorId, s.burnEyes[i] ? eyes : body);
                r.SetPropertyBlock(shadowBlock);
            }
            if (V5Vfx.On) V5Vfx.I.ShadowSmoulder(s.position, s.height, t);
        }

        /// <summary>The light has burned a shadow away: a few violet-black wisps rise where it stood, a soft sound plays, and its body goes
        /// as every shadow's body goes at dawn.</summary>
        void BurnAway(int index)
        {
            var s = shadows[index];
            shadows.RemoveAt(index);
            if (V5Vfx.On) V5Vfx.I.ShadowDissolve(s.position, s.height);
            else if (FxPool.Active)
                for (int i = 0; i < 4; i++)
                    FxPool.Active.Emit(s.position + new Vector3(Random.Range(-.3f, .3f), Random.Range(.4f, Mathf.Max(.5f, s.height)), Random.Range(-.3f, .3f)),
                        WhiteboxWisp, Random.Range(.3f, .5f), Random.Range(.5f, .8f), Vector3.up * Random.Range(1f, 1.8f));
            CampaignAudio.PlayAt(CampaignSfx.ShadowFleeLight, s.position, BurnSoundVolume);
            if (s.view) Destroy(s.view.gameObject);
        }

        /// <summary>A shot or a blast that crosses a shadow goes through it: the shadow only ripples, so the player sees that nothing
        /// landed. The first ripple of a run also says so, with what does work against shadows.</summary>
        void RippleUnderFire()
        {
            var pool = AttackPool.Instance;
            if (!pool || pool.actors == null || shadows.Count == 0) return;
            foreach (var actor in pool.actors)
            {
                if (!actor || !actor.gameObject.activeSelf) continue;
                Vector3 at = Query.Flat(actor.transform.position);
                foreach (var s in shadows)
                {
                    if (Time.time < s.rippleUntil || (at - s.position).sqrMagnitude > 1.1f) continue;
                    s.rippleUntil = Time.time + .35f;
                    Fx.Pop(s.position + Vector3.up * 1.1f, ShadowEye, .7f);
                    Fx.Ring(s.position, .9f, ShadowEye, .2f);
                    if (!shadowFireSaid)
                    {
                        shadowFireSaid = true;
                        ui.Flash(ArenaText.Pair("武器伤不到影子，只有光能灼伤它们。", "Weapons can't hurt shadows. Only light burns them."), 3f);
                    }
                }
            }
        }

        // ───── For automated checks ─────

        /// <summary>The night of this day begins at once, as if the morning's kills had fallen at these points (so many of one kind at
        /// each). The player gets lampCount lamps and stands at the base door.</summary>
        public void DebugBeginNight(int day, Vector3[] at, int[] kills, EnemyKind[] kind, int lampCount)
        {
            if (!Running) return;
            ClearNight(false);
            Day = day;
            regionKills.Clear(); regionKindKills.Clear();
            for (int i = 0; i < at.Length; i++)
            {
                int region = RegionOf(at[i]);
                regionKills[region] = KillsIn(region) + kills[i];
                if (!regionKindKills.TryGetValue(region, out var list)) regionKindKills[region] = list = new List<EnemyKind>();
                for (int k = 0; k < kills[i]; k++) list.Add(kind[i]);
            }
            BeginNight();
            lampStock = lampCount;
            PlacePlayer(DoorPoint);
        }

        public void DebugPlacePlayer(Vector3 at) => PlacePlayer(at);

        /// <summary>Lamp index's crowd (the shadows at its edge or inside it) and whether a crowd has gone into it; false when there is no
        /// such lamp.</summary>
        public bool DebugLampState(int index, out int crowd, out bool goneIn)
        {
            bool there = index >= 0 && index < lamps.Count;
            crowd = there ? lamps[index].crowd : 0;
            goneIn = there && lamps[index].smothered;
            return there;
        }
        public void DebugPlaceLamp(Vector3 at) { if (Stage == CampaignStage.Night) PlaceLamp(Query.Flat(at)); }

        /// <summary>Raises one plain shadow at a point and gives back a probe that reads it: whether it is still there, where it stands and
        /// how many seconds of light it has taken.</summary>
        public System.Func<(bool there, Vector3 position, float burned)> DebugAddShadow(Vector3 at)
        {
            var s = AddShadow(Query.Flat(at), null);
            return () => (shadows.Contains(s), s.position, s.burned);
        }

        /// <summary>The night in one line: the shadows within reach cells of the player (how many have noticed them, the nearest's
        /// distance), and each lamp's crowd, wear and whether a crowd has gone in.</summary>
        public string DebugNightReport(float reach = 30f)
        {
            var p = gm.player;
            Vector3 pp = p ? Query.Flat(p.Position) : BaseCentre;
            int near = 0, alert = 0, doubles = 0;
            float nearest = float.MaxValue;
            foreach (var s in shadows)
            {
                float d = Query.Flat(s.position - pp).magnitude;
                if (s.animator) doubles++;
                if (d > reach) continue;
                near++;
                if (s.alert) alert++;
                nearest = Mathf.Min(nearest, d);
            }
            var text = new System.Text.StringBuilder();
            text.Append($"shadows {shadows.Count} (model doubles {doubles}, burned away {Mathf.Max(0, shadowsRaised - shadows.Count)}), within {reach:0}: {near}, noticed {alert}, nearest {(near > 0 ? nearest.ToString("0.0") : "-")}");
            text.Append($"; player at ({pp.x:0.0}, {pp.z:0.0}) hp {(p ? p.Hp : 0f):0}/{(p ? p.maxHp : 0f):0} in base light {InBaseLight(pp)} in light {InLampLight(pp)}");
            for (int i = 0; i < lamps.Count; i++)
                text.Append($"; lamp {i} at ({lamps[i].position.x:0.0}, {lamps[i].position.z:0.0}) crowd {lamps[i].crowd} hp {lamps[i].hp:0.0} gone in {lamps[i].smothered}");
            return text.ToString();
        }
    }
}
