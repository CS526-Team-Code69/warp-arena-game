using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The night's shadow monsters (V2 doc section 5). Every region of the map holds some at night, and a region gets more for the
    /// morning's kills there; those are dark doubles of the monsters that were killed. A shadow that notices the player comes for them a
    /// little faster than the player walks, so only a sprint opens the distance. Light holds shadows back: they wait at its edge. Once a
    /// crowd of them (rules.overrunShadows) waits at one lamp they go in together, put the lamp out and attack; the base's light is
    /// never put out. Weapons and skills work at night and pass through shadows: nothing hurts one.
    /// The tutorial night's shadows hunt wherever the player goes, a little slower than a walk, and touch for a share of a normal hit
    /// (rules.tutorialShadowHitScale), so the first night shows that a touch hurts; they never join a crowd at a lamp.
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
        // A tutorial night shadow hunts at this share of the player's walk, so walking away always gets a new player back to the light.
        const float TutorialChaseShare = .9f;
        // A crowd at a lamp shows itself this long before it goes in: the ring turns violet and the lamp flickers.
        const float CrowdWarning = 1.5f;
        float swarmHitAt;
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

            // Each lamp's crowd: the shadows at its edge or inside it. A crowd goes in, and a lamp a crowd went into shelters no more.
            float band = lampRadius + rules.lampGatherBand;
            foreach (var lamp in lamps)
            {
                int crowd = 0;
                foreach (var s in shadows)
                    if (!s.tutorial && Query.Flat(s.position - lamp.position).sqrMagnitude <= band * band) crowd++;
                lamp.crowd = crowd;
                if (lamp.smothered) continue;
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
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShadeSpawn, lamp.position, .7f, 1f);
                }
                else if (Time.time - lamp.crowdSince >= CrowdWarning) Smother(lamp);
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
                    speed = rules.shadowSpeed;
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
            RippleUnderFire();
        }

        /// <summary>Where a shadow that wants to step from one point to the next ends up: no shadow stands in the base's light, or in a
        /// lamp's light before a crowd has gone into it. One caught inside (a lamp was set down beside it) backs out; one that would step
        /// in slides along the edge instead.</summary>
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
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShadeSpawn, lamp.position, .5f, 1f);
            DrawLampEdge(lamp);
        }

        /// <summary>A shot or a blast that crosses a shadow goes through it: the shadow only ripples, so the player sees that nothing
        /// landed.</summary>
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
        public void DebugPlaceLamp(Vector3 at) { if (Stage == CampaignStage.Night) PlaceLamp(Query.Flat(at)); }

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
            text.Append($"shadows {shadows.Count} (model doubles {doubles}), within {reach:0}: {near}, noticed {alert}, nearest {(near > 0 ? nearest.ToString("0.0") : "-")}");
            text.Append($"; player at ({pp.x:0.0}, {pp.z:0.0}) hp {(p ? p.Hp : 0f):0}/{(p ? p.maxHp : 0f):0} in base light {InBaseLight(pp)} in light {InLampLight(pp)}");
            for (int i = 0; i < lamps.Count; i++)
                text.Append($"; lamp {i} at ({lamps[i].position.x:0.0}, {lamps[i].position.z:0.0}) crowd {lamps[i].crowd} hp {lamps[i].hp:0.0} gone in {lamps[i].smothered}");
            return text.ToString();
        }
    }
}
