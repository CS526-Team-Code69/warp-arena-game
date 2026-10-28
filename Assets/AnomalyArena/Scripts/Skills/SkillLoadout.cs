using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    [DisallowMultipleComponent]
    public sealed class SkillLoadout : MonoBehaviour
    {

        const int BlinkChargeCap = 2;

        const int MaxLevel = SkillDefinition.MaxLevel;
        [field: SerializeField, Tooltip("The 13 normal skills and 3 Greater Void skills as editable assets.")]
        public SkillCatalog Catalog { get; set; }
        [field: SerializeField] public SkillSlot[] Slots { get; private set; } = { new(), new(), new() };
        public int OwnedCount { get { int count = 0; foreach (var slot in Slots) if (!slot.Empty) count++; return count; } }
        public bool VoidWalkActive => voidWalkTimer > 0f;

        public bool PlayerUntargetable => VoidWalkActive;

        public event System.Action<float> VoidWalkEnded;
        float voidWalkEnchant;

        public Vector3 VoidWalkStart { get; private set; }
        public Vector3 VoidWalkEnd { get; private set; }
        public float VoidWalkFlash01 => voidWalkDuration > 0f ? Mathf.Clamp01(voidWalkTimer / voidWalkDuration) : 0f;

        public float PlayerMoveMultiplier => (VoidWalkActive ? voidWalkMove : 1f)
            * (CurrentSingularity is { } singularity ? Mathf.Clamp01(singularity.definition.singularityPlayerMoveMultiplier) : 1f);

        public float StatusResistance => radianceTimer > 0f ? radianceResistance : 0f;
        public bool SingularityActive => CurrentSingularity != null;

        public bool Invincible => VoidWalkActive || SingularityActive;

        public bool ActionLocked => false;

        public bool MovementLocked => false;
        public float SingularityProgress01 => CurrentSingularity is { } effect
            ? 1f - Mathf.Clamp01(effect.remaining / Mathf.Max(0.01f, effect.definition.Duration(effect.level))) : 0f;
        public float SingularityRadius => CurrentSingularity is { } effect ? effect.definition.Radius(effect.level) : 0f;
        public Vector3 SingularityOrigin => CurrentSingularity is { } effect ? effect.center : body ? body.Position : transform.position;
        public int SingularityAbsorbedCount => CurrentSingularity?.absorbed.Count ?? 0;
        public IReadOnlyList<Combatant> SingularityTargets => CurrentSingularity is { } effect ? effect.targets : Array.Empty<Combatant>();
        public bool TimeSlipActive => timeSlipTimer > 0f;
        public bool OverchargeActive => overchargeTimer > 0f;
        public bool RadianceActive => radianceTimer > 0f;
        float DamageMultiplier => progression ? progression.Stats.DamageMultiplier : 1f;

        public float WeaponCooldownRate => overchargeTimer > 0f ? overchargeCooldownRate : 1f;

        public float SkillDamageMultiplier => overchargeTimer > 0f && overchargeLevel >= MaxLevel ? overchargeDamageMultiplier : 1f;
        public float AttackSpeedMultiplier => timeSlipTimer > 0f && timeSlipLevel >= MaxLevel ? 1.5f : 1f;

        public float KnockbackMultiplier => 1f;

        public int BlinkCharges { get { int max = BlinkChargesMax, ready = 0; for (int c = 0; c < max; c++) if (blinkRecharge[c] <= 0f) ready++; return ready; } }

        public int BlinkChargesMax { get { int index = SlotOf(SkillId.Blink); return index < 0 ? 0 : Slots[index].Level >= MaxLevel ? BlinkChargeCap : 1; } }
        public event Action Changed;
        public event Action<int, string> CastRefused;

        public event Action<SkillId, Vector3, float, float> CastSucceeded;

        public event Action<SkillId, Vector3, float, float> VisualRequested;
        Combatant body;
        RunProgression progression;
        float radianceTimer, radianceFraction, radianceShare, radianceResistance, voidWalkTimer, voidWalkDuration, voidWalkMove = 1f, timeSlipTimer, overchargeTimer;
        float overchargeCooldownRate = 2f, overchargeDamageMultiplier = 1f;
        int overchargeLevel, timeSlipLevel;

        bool mirrorEcho, suspendedMirrorEcho;

        readonly float[] blinkRecharge = new float[BlinkChargeCap];

        int skillCardsWithoutHighTier;
        readonly List<TimedSkill> effects = new();
        readonly List<TimedSkill> suspendedBattlefieldEffects = new();
        bool battlefieldSuspended;
        readonly List<Combatant> nearby = new();
        readonly List<Combatant> deathNearby = new();
        readonly HashSet<Combatant> found = new();
        readonly List<SkillDefinition> candidates = new();
        readonly List<int> upgradeable = new();
        readonly List<int> echoSlots = new();
        readonly Collider[] queryBuffer = new Collider[256];
        static readonly List<SkillLoadout> active = new();

        sealed class TimedSkill
        {
            public SkillId id;
            public SkillDefinition definition;
            public int level, remainingBolts;
            public float remaining, clock, interval;
            public Vector3 center;

            public bool anchored;
            public readonly List<Vector3> trail = new();
            public readonly List<Combatant> targets = new();
            public readonly HashSet<Combatant> absorbed = new();
        }

        TimedSkill CurrentSingularity
        {
            get
            {
                foreach (var effect in effects) if (effect.id == SkillId.Singularity && effect.remaining > 0f) return effect;
                return null;
            }
        }
        void Awake()
        {
            body = GetComponent<Combatant>(); progression = GetComponent<RunProgression>();
            if (body) body.Eliminated += OnBodyEliminated;
        }
        void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            SkillWorldAPI.DecoyEnded -= OnDecoyEnded; SkillWorldAPI.DecoyEnded += OnDecoyEnded;
        }
        void OnDisable() { active.Remove(this); SkillWorldAPI.DecoyEnded -= OnDecoyEnded; ClearWaveEffects(); }
        void OnDestroy() { if (body) body.Eliminated -= OnBodyEliminated; }
        void OnBodyEliminated(Combatant _) => ClearWaveEffects();

        public static float EnemySlowMultiplier(Combatant enemy)
        {
            if (!enemy) return 1f;
            float multiplier = 1f;
            foreach (var loadout in active)
            {
                if (!loadout || !loadout.body || !loadout.body.IsAlive) continue;
                var effect = loadout.CurrentSingularity;
                if (effect != null && effect.targets.Contains(enemy))
                    multiplier = Mathf.Min(multiplier, effect.definition.singularitySlowMultiplier);
            }
            return multiplier;
        }

        public static void TickAll(float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                var skills = active[i];
                if (skills && skills.body && skills.body.IsAlive) skills.Tick(dt);
            }
        }

        public void ResetRun()
        {
            foreach (var slot in Slots) { slot.Definition = null; slot.Level = 1; slot.CooldownLeft = 0f; }
            Array.Clear(blinkRecharge, 0, blinkRecharge.Length);
            skillCardsWithoutHighTier = 0;
            ClearWaveEffects(); OnChanged();
        }

        public bool Learn(SkillId id, int replaceIndex = -1, int level = 1)
        {
            var definition = Catalog ? Catalog.Find(id) : null;
            if (!definition) return false;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Empty || Slots[i].Id != id) continue;
                if (Slots[i].Level >= MaxLevel || definition.greaterVoid) return false;
                Slots[i].Level = Mathf.Min(MaxLevel, Mathf.Max(Slots[i].Level + 1, level));

                if (id == SkillId.Blink && Slots[i].Level >= MaxLevel) { blinkRecharge[1] = 0f; SyncBlinkCooldown(); }
                OnChanged(); return true;
            }
            int place = replaceIndex;
            if (place < 0) for (int i = 0; i < Slots.Length; i++) if (Slots[i].Empty) { place = i; break; }
            if (place < 0 || place >= Slots.Length) return false;
            Slots[place].Definition = definition; Slots[place].Level = definition.greaterVoid ? 1 : Mathf.Clamp(level, 1, MaxLevel); Slots[place].CooldownLeft = 0f;
            if (id == SkillId.Blink) Array.Clear(blinkRecharge, 0, blinkRecharge.Length);
            progression?.UnlockEnergy(); OnChanged(); return true;
        }

        public static bool OffersNewSkill(SkillCategory category, int wave) => wave <= 5 ? category == SkillCategory.BasicElement
            : wave <= 8 ? category is SkillCategory.BasicElement or SkillCategory.BasicMechanic
            : wave <= 11 ? category is SkillCategory.AdvancedElement or SkillCategory.BasicMechanic
            : category is SkillCategory.AdvancedElement or SkillCategory.AdvancedMechanic;

        public DraftCard RollCard(int wave = 1, SkillId? avoid = null)
        {
            if (!Catalog || Catalog.skills == null) return null;
            candidates.Clear(); upgradeable.Clear();
            foreach (var definition in Catalog.skills)
                if (definition && !definition.greaterVoid && OffersNewSkill(definition.category, wave) && SlotOf(definition.id) < 0 && definition.id != avoid)
                    candidates.Add(definition);
            for (int i = 0; i < Slots.Length; i++)
                if (!Slots[i].Empty && Slots[i].Level < MaxLevel && !Slots[i].Definition.greaterVoid && Slots[i].Id != avoid) upgradeable.Add(i);
            if (avoid.HasValue && candidates.Count == 0 && upgradeable.Count == 0) return RollCard(wave);
            float upgradeChance = OwnedCount == 0 ? 0f : OwnedCount < 3 ? 0.3f : 0.6f;
            if (upgradeable.Count > 0 && (candidates.Count == 0 || UnityEngine.Random.value < upgradeChance))
            {
                var slot = Slots[upgradeable[UnityEngine.Random.Range(0, upgradeable.Count)]];

                SkillId id = slot.Id; int level = slot.Level, next = RollSkillLevel(wave, level + 1);

                return new DraftCard(DraftCardKind.Skill, ArenaText.Pair($"{slot.Name} · Level {next}",$"{slot.Name} · Level {next}"),
                    $"{slot.Definition.Description(slot.Level)} → {slot.Definition.Description(next)}",
                    _ => Learn(id, -1, next), payload: id, rarity: ArenaRarity.ForSkill(slot.Definition, next),
                    stillFits: () => SlotOf(id) >= 0 && Slots[SlotOf(id)].Level == level);
            }
            if (candidates.Count == 0) return null;
            var picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return MakeLearnCard(picked, RollSkillLevel(wave));
        }

        int RollSkillLevel(int wave, int floor = 1)
        {
            floor = Mathf.Clamp(floor, 1, MaxLevel);
            var rules = progression ? progression.Rules : null;
            if (!rules) return floor;
            bool pity = skillCardsWithoutHighTier >= Mathf.Max(1, rules.highTierPity);
            var rolled = rules.SkillRarity(wave, UnityEngine.Random.Range(0, 10000) / 100f, pity);
            int level = Mathf.Max(floor, ArenaRarity.SkillLevel(rolled));
            ProgressionRules.RarityBand(wave, out _, out var high);
            skillCardsWithoutHighTier = level >= ArenaRarity.SkillLevel(high) ? 0 : skillCardsWithoutHighTier + 1;
            return level;
        }

        public DraftCard[] GreaterVoidCards()
        {
            var result = new DraftCard[3];
            for (int i = 0; i < result.Length; i++)
            {
                var definition = Catalog.Find((SkillId)((int)SkillId.PocketVoid + i));
                result[i] = MakeLearnCard(definition);
            }
            return result;
        }

        DraftCard MakeLearnCard(SkillDefinition definition, int level = 1)
        {

            bool full = OwnedCount >= 3;
            level = definition.greaterVoid ? 1 : Mathf.Clamp(level, 1, MaxLevel);
            string title = ArenaTerms.Skill(definition.id) + (level > 1 ? ArenaText.Pair($" · Level {level}",$" · Level {level}") : "");
            return new(DraftCardKind.Skill, title,
                definition.Description(level) + ArenaText.Pair($"\n{definition.cost:0} Energy",$"\n{definition.cost:0} Energy"),
                replacement => Learn(definition.id, replacement, level), full, definition.id,
                ArenaRarity.ForSkill(definition, level), () => SlotOf(definition.id) < 0 && (OwnedCount >= 3) == full);
        }

        public bool TryCast(int index)
        {
            if (index < 0 || index >= Slots.Length || Slots[index].Empty || !body || !body.IsAlive) return false;
            var game = GameManager.Instance;
            if (game && !game.IsCombatActive) return Refuse(index, ArenaText.Pair("Not during a menu","Not during a menu"));
            var slot = Slots[index]; var def = slot.Definition; int level = slot.Level;
            var board = ElementStatus.For(body);

            bool coolingDown = def.id == SkillId.Blink ? BlinkCharges == 0 : slot.CooldownLeft > 0f;

            bool sliding = (body.State is CharacterState.Knocked or CharacterState.Thrown) && !BreaksDisplacement(def.id);
            if (body.Stunned || body.State == CharacterState.Dashing || sliding || (board && board.BlocksActions) || coolingDown)
                return Refuse(index, ArenaText.Pair("Not ready","Not ready"));

            if (EffectActive(def.id)) return Refuse(index, ArenaText.Pair("Still active","Still active"));
            if (!HasWorldHook(def.id)) return Refuse(index, ArenaText.Pair("World action unavailable","World action unavailable"));
            Vector3 desired = body.AimPoint;
            Vector3 origin = body.Position;
            Vector3 offset = Query.Flat(desired - origin);
            if (offset.magnitude > def.Range(level)) desired = origin + offset.normalized * def.Range(level);
            bool pointTarget = IsPointSkill(def.id);
            Vector3 point = desired;
            if (pointTarget && SkillWorldAPI.ValidPoint != null)
            {
                var valid = SkillWorldAPI.ValidPoint(origin, desired);
                if (!valid.HasValue)
                    return Refuse(index, ArenaText.Pair("No valid point","No valid point"));
                point = valid.Value;
            }

            Combatant target = null;
            if (def.id == SkillId.Devour)
            {
                target = FindDevourTarget(def, level, origin, body.AimPoint);
                if (!target) return Refuse(index, ArenaText.Pair("No weakened target","No weakened target"));
            }
            if (!progression || !progression.TrySpendEnergy(def.cost)) return Refuse(index, ArenaText.Pair("No Energy","No Energy"));
            if (def.id == SkillId.Blink) SpendBlinkCharge(def.Cooldown(level));
            else slot.CooldownLeft = def.Cooldown(level);
            Vector3 visualPoint = target ? target.Position : pointTarget ? point : origin;
            Execute(def, level, point, target, origin, false);
            CastSucceeded?.Invoke(def.id, visualPoint, def.Radius(level), def.Duration(level));
            OnChanged(); return true;
        }

        public static bool BreaksDisplacement(SkillId id) => id is SkillId.Blink or SkillId.VoidWalk;

        public bool EffectActive(SkillId id) => id switch
        {
            SkillId.Radiance => radianceTimer > 0f,
            SkillId.TimeSlip => timeSlipTimer > 0f,
            SkillId.Overcharge => overchargeTimer > 0f,
            SkillId.VoidWalk => voidWalkTimer > 0f,
            SkillId.Bulwark or SkillId.Decoy or SkillId.PocketVoid => body && (SkillWorldAPI.SkillObjectStanding?.Invoke(id, body) ?? false),
            _ => HasTimedEffect(id)
        };

        bool HasTimedEffect(SkillId id)
        {
            foreach (var effect in effects) if (effect.id == id && effect.remaining > 0f) return true;
            return false;
        }
        static bool IsPointSkill(SkillId id) => id is SkillId.Blink or SkillId.Bulwark or SkillId.Decoy or SkillId.GravityWell
            or SkillId.PocketVoid or SkillId.StormCall;

        Combatant FindDevourTarget(SkillDefinition def, int level, Vector3 center, Vector3 aim)
        {
            QueryInto(center, def.Range(level), nearby);
            Combatant target = null;
            float distance = float.MaxValue;
            foreach (var c in nearby)
            {
                if (c is Enemy { IsBoss: true } || c.Hp >= c.maxHp * def.Strength(level)) continue;
                float aimDistance = Query.Flat(c.Position - aim).sqrMagnitude;
                if (aimDistance >= distance) continue;
                distance = aimDistance; target = c;
            }
            return target;
        }

        void Execute(SkillDefinition def, int level, Vector3 point, Combatant target, Vector3 center, bool echo)
        {
            switch (def.id)
            {
                case SkillId.FlameWake: AddTimed(def, level, center, anchored: echo); break;
                case SkillId.Contagion:
                    QueryInto(center, def.Radius(level), nearby);
                    foreach (var c in nearby) ElementStatus.For(c)?.Add(ElementKind.Poison, def.Strength(level), body);
                    AddTimed(def, level, center); break;
                case SkillId.FrostNova:
                    QueryInto(center, def.Radius(level), nearby);
                    foreach (var c in nearby)
                    {
                        var board = ElementStatus.For(c);
                        if (!board) continue;
                        board.Add(ElementKind.Frost, def.Strength(level), body);

                        if (level >= MaxLevel && board.Frozen) board.ExtendFreeze(def.freezeHold);
                    }
                    break;
                case SkillId.StormCall: AddTimed(def, level, point); break;
                case SkillId.Bulwark:
                    SkillWorldAPI.CreateWall?.Invoke(point, def.Radius(level), def.Duration(level), def.ContactKnockback(level), body);

                    ElementStatus.For(body)?.GainBulwarkShield(def.shieldScalesWithMaxHp ? body.maxHp * def.Strength(level) : def.Strength(level),
                        level >= MaxLevel ? def.bulwarkShieldDirectFactor : (float?)null); break;
                case SkillId.Radiance:
                    ElementStatus.For(body)?.ClearStatuses(); radianceTimer = def.Duration(level); radianceShare = def.Strength(level);
                    radianceResistance = def.StatusResistance(level);
                    if (level >= MaxLevel) body.Heal(def.healOnCast); break;
                case SkillId.Devour:
                    Vector3 where = target.Position;
                    NotifyDevoured(target);

                    if (target.State == CharacterState.Hooked) target.SetState(CharacterState.Normal);
                    if (SkillWorldAPI.Swallow != null) SkillWorldAPI.Swallow(target, body);
                    else target.OnEnterFallZone(Vector3.zero);
                    SkillWorldAPI.FeedNearestHole?.Invoke(where, def.feedRange, 1);
                    progression.GainEnergy(def.energyReward); break;
                case SkillId.Blink:
                    body.CancelDisplacement();
                    if (SkillWorldAPI.Teleport != null) SkillWorldAPI.Teleport(body, point);
                    else
                    {
                        var rigidbody = body.GetComponent<Rigidbody>();
                        rigidbody.position = point; rigidbody.linearVelocity = Vector3.zero; body.transform.position = point;
                    }
                    break;
                case SkillId.Shockwave:
                    AreaHit(center, def.Radius(level), 0f, def.Strength(level));
                    if (level >= MaxLevel) SkillWorldAPI.ReflectProjectiles?.Invoke(center, def.Radius(level), body); break;
                case SkillId.GravityWell: AddTimed(def, level, point); break;
                case SkillId.TimeSlip:
                    timeSlipTimer = def.Duration(level); timeSlipLevel = level;
                    SkillWorldAPI.TimeSlip?.Invoke(def.Strength(level), timeSlipTimer); break;
                case SkillId.Decoy:

                    SkillWorldAPI.CreateDecoy?.Invoke(point, def.Duration(level), false, body);
                    mirrorEcho = level >= MaxLevel; break;
                case SkillId.Overcharge:
                    overchargeTimer = def.Duration(level); overchargeLevel = level;
                    overchargeCooldownRate = Mathf.Max(1f, def.overchargeRate);
                    overchargeDamageMultiplier = Mathf.Max(1f, def.overchargeDamageMultiplier); break;
                case SkillId.PocketVoid:
                    SkillWorldAPI.CreateVoidHole?.Invoke(point, def.Radius(level), def.voidGravityWidth, def.Duration(level), body, false); break;
                case SkillId.VoidWalk:

                    body.CancelDisplacement();
                    VoidWalkStart = VoidWalkEnd = body.Position;
                    voidWalkTimer = voidWalkDuration = def.Duration(level);
                    voidWalkMove = Mathf.Max(1f, def.voidWalkMoveMultiplier);
                    voidWalkEnchant = Mathf.Max(0f, def.voidWalkEnchantSeconds);
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.SkillVoidWalk, body.Position); break;
                case SkillId.Singularity:

                    body.CancelDisplacement();
                    ElementStatus.For(body)?.ClearControl();
                    AddTimed(def, level, body.Position);
                    break;
            }
        }

        void OnDecoyEnded(Vector3 point)
        {
            if (!mirrorEcho) return;
            mirrorEcho = false;
            if (!body || !body.IsAlive) return;
            var game = GameManager.Instance;
            if (game && !game.IsCombatActive) return;
            echoSlots.Clear();
            for (int i = 0; i < Slots.Length; i++)
                if (!Slots[i].Empty && Slots[i].Id != SkillId.Decoy && !Slots[i].Definition.greaterVoid && HasWorldHook(Slots[i].Id)
                    && !EffectActive(Slots[i].Id))
                    echoSlots.Add(i);

            while (echoSlots.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, echoSlots.Count);
                var slot = Slots[echoSlots[pick]];
                echoSlots.RemoveAt(pick);
                if (TryEchoCast(slot.Definition, slot.Level, point)) { V3WorldAudio.Instance?.Play(ArenaWorldCue.SkillMirrorEcho, point); return; }
            }
        }

        bool TryEchoCast(SkillDefinition def, int level, Vector3 at)
        {
            Vector3 point = at;
            if (IsPointSkill(def.id) && SkillWorldAPI.ValidPoint != null)
            {
                var valid = SkillWorldAPI.ValidPoint(at, at);
                if (!valid.HasValue) return false;
                point = valid.Value;
            }
            Combatant target = null;
            if (def.id == SkillId.Devour)
            {
                if (!progression) return false;
                target = FindDevourTarget(def, level, at, at);
                if (!target) return false;
            }
            if (def.id == SkillId.Blink && body.State == CharacterState.Dashing) return false;
            Vector3 visualPoint = target ? target.Position : point;
            Execute(def, level, point, target, at, true);
            CastSucceeded?.Invoke(def.id, visualPoint, def.Radius(level), def.Duration(level));
            OnChanged(); return true;
        }

        void AddTimed(SkillDefinition definition, int level, Vector3 point, bool anchored = false)
        {
            var effect = new TimedSkill { id = definition.id, definition = definition, level = level,
                remaining = definition.Duration(level), center = point, anchored = anchored };
            if (effect.id == SkillId.FlameWake) effect.trail.Add(point);
            if (effect.id == SkillId.StormCall)
            {
                effect.remainingBolts = Mathf.RoundToInt(definition.Strength(level));
                effect.interval = effect.remaining / Mathf.Max(1, effect.remainingBolts);
            }
            if (effect.id == SkillId.Singularity)
            {
                foreach (var combatant in Combatant.All)
                    if (combatant && combatant.IsAlive && combatant.Team == Team.Enemy && OnScreen(combatant.Position))
                        effect.targets.Add(combatant);
            }
            effects.Add(effect);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            var game = GameManager.Instance;
            float worldDt = game ? game.WorldDeltaTime : dt;
            foreach (var slot in Slots) slot.CooldownLeft = Mathf.Max(0f, slot.CooldownLeft - dt);
            for (int c = 0; c < blinkRecharge.Length; c++) blinkRecharge[c] = Mathf.Max(0f, blinkRecharge[c] - dt);
            SyncBlinkCooldown();
            radianceTimer = Mathf.Max(0f, radianceTimer - dt);
            if (voidWalkTimer > 0f)
            {
                voidWalkTimer = Mathf.Max(0f, voidWalkTimer - dt);
                if (voidWalkTimer <= 0f) VoidWalkEnded?.Invoke(voidWalkEnchant);
            }
            timeSlipTimer = Mathf.Max(0f, timeSlipTimer - dt);
            overchargeTimer = Mathf.Max(0f, overchargeTimer - dt);
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i]; var def = effect.definition; int level = effect.level;

                float step = effect.id == SkillId.Singularity ? dt : worldDt;
                float activeDt = Mathf.Min(step, Mathf.Max(0f, effect.remaining));
                effect.remaining = Mathf.Max(0f, effect.remaining - step); effect.clock += activeDt;
                switch (effect.id)
                {
                    case SkillId.FlameWake:
                        if (!effect.anchored && effect.trail.Count < 128
                            && Query.Flat(body.Position - effect.trail[^1]).sqrMagnitude >= def.trailSpacing * def.trailSpacing)
                        {
                            effect.trail.Add(body.Position);
                        }
                        if (effect.clock >= 0.25f)
                        {
                            effect.clock = 0f;
                            QueryInto(effect.anchored ? effect.center : body.Position, 100f, nearby);
                            foreach (var c in nearby)
                            {
                                foreach (var position in effect.trail)
                                {
                                    if (Query.Flat(c.Position - position).sqrMagnitude > def.Radius(level) * def.Radius(level)) continue;

                                    ElementStatus.For(c)?.Ignite(def.Strength(level) * ElementCombat.WaveScale, body); break;
                                }
                            }
                        }
                        break;
                    case SkillId.StormCall:
                        while ((effect.clock >= effect.interval || effect.remaining <= 0f) && effect.remainingBolts > 0)
                        {
                            effect.clock -= effect.interval; effect.remainingBolts--;
                            QueryInto(effect.center, def.Radius(level), nearby);
                            if (nearby.Count == 0) continue;
                            var c = nearby[UnityEngine.Random.Range(0, nearby.Count)];

                            float share = ElementImmunity.DamageShare(c, def.element, body);
                            bool struck = share > 0f
                                ? c.ReceiveDamage(DamageInfo.Attack(def.ScaledDamage(def.hitDamage, level, DamageMultiplier) * share, body, kind: HitKind.Bullet))
                                : c.IsAlive && !c.Protected;
                            if (struck)
                            {

                                if (level >= MaxLevel) ElementCombat.ApplyStacks(c, ElementKind.Lightning, def.HitStacks(level), body);
                                else if (c.IsAlive) ElementStatus.For(c)?.Add(ElementKind.Lightning, def.HitStacks(level), body);
                            }
                            OnVisualRequested(effect.id, c.Position, 0.5f, 0.2f);
                        }
                        break;
                    case SkillId.GravityWell:
                        QueryInto(effect.center, def.Radius(level), nearby);
                        foreach (var c in nearby)
                        {
                            Vector3 direction = Query.Flat(effect.center - c.Position);
                            if (SkillWorldAPI.Pull != null) SkillWorldAPI.Pull(c, effect.center, def.Strength(level) * activeDt);
                            else c.SetMovePosition(c.Position + direction.normalized * (def.Strength(level) * activeDt));
                        }
                        if (effect.remaining <= 0f && level >= MaxLevel) AreaHit(effect.center, def.Radius(level), def.ScaledDamage(def.endDamage, level, DamageMultiplier), def.endKnockback);
                        break;
                    case SkillId.Singularity:
                        effect.center = body.Position;
                        float pullStep = def.Strength(level) * activeDt / Mathf.Max(0.01f, def.Duration(level));
                        foreach (var c in effect.targets)
                        {
                            if (!c || !c.IsAlive) continue;
                            Vector3 beforePull = c.Position;
                            SkillWorldAPI.Pull?.Invoke(c, effect.center, pullStep);

                            if (Query.Flat(beforePull - effect.center).sqrMagnitude <= def.Radius(level) * def.Radius(level))
                                effect.absorbed.Add(c);
                        }
                        if (effect.remaining <= 0f)
                        {
                            float damage = def.ScaledDamage(def.singularityBaseDamage + effect.absorbed.Count * def.singularityDamagePerEnemy, level, DamageMultiplier);
                            AreaHit(effect.center, def.Radius(level), damage, 0f, def.element);
                            var fx = FxPool.Active;
                            if (fx) fx.Emit(effect.center, ElementCombat.ColorFor(ElementKind.Void), def.Radius(level) * 2f, 2f,
                                texture: fx.ring, flat: true);
                        }
                        break;
                }
                if (effect.remaining <= 0f) effects.RemoveAt(i);
            }
        }

        public void HealFromDamage(float damage)
        {
            if (!body || !body.IsAlive || radianceTimer <= 0f || damage <= 0f) return;
            radianceFraction += damage * radianceShare;
            int heal = Mathf.FloorToInt(radianceFraction); radianceFraction -= heal;
            if (heal > 0) body.Heal(heal);
        }

        public static void NotifyEnemyDied(Combatant victim)
        {
            foreach (var loadout in active) if (loadout && loadout.body && loadout.body.IsAlive) loadout.SpreadPoison(victim);
        }

        public static void NotifyDevoured(Combatant victim)
        {
            foreach (var loadout in active)
                if (loadout && loadout.body && loadout.body.IsAlive) loadout.SpreadPoisonFromDevour(victim);
        }

        void SpreadPoisonFromDevour(Combatant victim)
        {
            var victimStatus = ElementStatus.For(victim);
            if (!victimStatus || victimStatus.Poison <= 0f) return;
            SpreadExistingPoison(victim, victimStatus.Poison);
        }

        void SpreadPoison(Combatant victim)
        {
            var victimStatus = ElementStatus.For(victim);
            if (!victimStatus || victimStatus.Poison <= 0f || victim.DeathCause != DeathCause.HpDepleted) return;
            SpreadExistingPoison(victim, victimStatus.Poison);
        }

        void SpreadExistingPoison(Combatant victim, float stacks)
        {
            int level = 0; float radius = 5f;
            foreach (var effect in effects)
                if (effect.id == SkillId.Contagion && effect.remaining > 0f)
                { level = Mathf.Max(level, effect.level); radius = effect.definition.Radius(effect.level); }
            if (level == 0) return;
            QueryInto(victim.Position, radius, deathNearby);
            deathNearby.Remove(victim);
            deathNearby.Sort((a, b) => (a.Position - victim.Position).sqrMagnitude.CompareTo((b.Position - victim.Position).sqrMagnitude));
            int count = Mathf.Min(level >= MaxLevel ? 2 : 1, deathNearby.Count);
            for (int i = 0; i < count; i++) ElementStatus.For(deathNearby[i])?.Add(ElementKind.Poison, stacks, body);
        }

        public void FreezeBattlefieldForTrial(bool freeze)
        {
            if (freeze)
            {
                if (battlefieldSuspended) return;
                suspendedBattlefieldEffects.Clear(); suspendedBattlefieldEffects.AddRange(effects); effects.Clear();
                suspendedMirrorEcho = mirrorEcho; mirrorEcho = false;
                battlefieldSuspended = true;
            }
            else if (battlefieldSuspended)
            {
                effects.Clear(); effects.AddRange(suspendedBattlefieldEffects); suspendedBattlefieldEffects.Clear();
                mirrorEcho = suspendedMirrorEcho; suspendedMirrorEcho = false;
                battlefieldSuspended = false;
            }
        }

        public void ClearWaveEffects()
        {
            if (timeSlipTimer > 0f) SkillWorldAPI.TimeSlip?.Invoke(1f,0f);
            effects.Clear(); radianceTimer = voidWalkTimer = voidWalkDuration = timeSlipTimer = overchargeTimer = 0f;
            suspendedBattlefieldEffects.Clear(); battlefieldSuspended = false;
            radianceFraction = radianceResistance = 0f; voidWalkMove = 1f;
            mirrorEcho = suspendedMirrorEcho = false;
        }

        public void CopyGroundVisuals(List<SkillGroundVisual> output)
        {
            output.Clear();
            foreach (var effect in effects)
            {
                if (effect.remaining <= 0f) continue;
                var sample = new SkillGroundVisual(effect.id, effect.center, effect.definition.Radius(effect.level),
                    effect.remaining, effect.definition.Duration(effect.level) - effect.remaining);
                if (effect.id == SkillId.FlameWake)
                    foreach (var point in effect.trail) output.Add(sample.At(point));
                else if (effect.id is SkillId.GravityWell or SkillId.StormCall) output.Add(sample);
            }
        }

        static bool HasWorldHook(SkillId id) => id switch
        {
            SkillId.Bulwark => SkillWorldAPI.ValidPoint != null && SkillWorldAPI.CreateWall != null,
            SkillId.Decoy => SkillWorldAPI.ValidPoint != null && SkillWorldAPI.CreateDecoy != null,
            SkillId.PocketVoid => SkillWorldAPI.ValidPoint != null && SkillWorldAPI.CreateVoidHole != null,
            SkillId.Singularity => SkillWorldAPI.Pull != null,
            SkillId.TimeSlip => SkillWorldAPI.TimeSlip != null,
            SkillId.Blink or SkillId.GravityWell or SkillId.StormCall => SkillWorldAPI.ValidPoint != null,
            _ => true
        };

        int SlotOf(SkillId id)
        {
            for (int i = 0; i < Slots.Length; i++) if (!Slots[i].Empty && Slots[i].Id == id) return i;
            return -1;
        }

        void SpendBlinkCharge(float cooldown)
        {
            int max = BlinkChargesMax;
            for (int c = 0; c < max; c++) if (blinkRecharge[c] <= 0f) { blinkRecharge[c] = cooldown; break; }
            SyncBlinkCooldown();
        }

        void SyncBlinkCooldown()
        {
            int index = SlotOf(SkillId.Blink);
            if (index < 0) return;
            int max = BlinkChargesMax;
            float wait = max > 0 ? float.MaxValue : 0f;
            for (int c = 0; c < max; c++)
            {
                if (blinkRecharge[c] <= 0f) { wait = 0f; break; }
                wait = Mathf.Min(wait, blinkRecharge[c]);
            }
            Slots[index].CooldownLeft = wait;
        }

        void AreaHit(Vector3 point, float radius, float damage, float knock, ElementKind element = ElementKind.None)
        {
            QueryInto(point, radius, nearby);
            foreach (var c in nearby)
            {
                Vector3 direction = c.Position - point;
                float share = ElementImmunity.DamageShare(c, element, body);
                if (damage <= 0f) c.Knockback(direction, knock, KnockKind.Hit);
                else if (share > 0f) c.ReceiveDamage(DamageInfo.Attack(Mathf.Round(damage * share), body, direction, knock, HitKind.Blast));
            }
        }

        void QueryInto(Vector3 point, float radius, List<Combatant> output)
        {
            output.Clear(); found.Clear();
            int count = Physics.OverlapSphereNonAlloc(Query.AtCastHeight(point), radius, queryBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = Combatant.From(queryBuffer[i]);
                if (c && c.IsAlive && c.Team == Team.Enemy && found.Add(c)) output.Add(c);
            }
        }
        bool OnScreen(Vector3 point)
        {
            if (SkillWorldAPI.IsOnScreen != null) return SkillWorldAPI.IsOnScreen(point);
            var cam = GameManager.Instance ? GameManager.Instance.cam : Camera.main;
            if (!cam) return false;
            Vector3 view = cam.WorldToViewportPoint(point);
            return view.z > 0f && view.x >= 0f && view.x <= 1f && view.y >= 0f && view.y <= 1f;
        }
        bool Refuse(int slot, string message) { CastRefused?.Invoke(slot, message); return false; }
        void OnChanged() => Changed?.Invoke();
        void OnVisualRequested(SkillId id, Vector3 point, float radius, float duration) => VisualRequested?.Invoke(id, point, radius, duration);
    }
}
