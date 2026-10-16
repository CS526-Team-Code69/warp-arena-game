using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    [DisallowMultipleComponent]
    public sealed class SkillLoadout : MonoBehaviour
    {
        [field: SerializeField, Tooltip("The 13 normal skills and 3 Greater Void skills as editable assets.")]
        public SkillCatalog Catalog { get; set; }
        [field: SerializeField] public SkillSlot[] Slots { get; private set; } = { new(), new(), new() };
        public int OwnedCount { get { int count = 0; foreach (var slot in Slots) if (!slot.Empty) count++; return count; } }
        public bool VoidWalkActive => voidWalkTimer > 0f;
        public Vector3 VoidWalkStart { get; private set; }
        public Vector3 VoidWalkEnd { get; private set; }
        public float VoidWalkFlash01 => voidWalkDuration > 0f ? Mathf.Clamp01(voidWalkTimer / voidWalkDuration) : 0f;
        public bool SingularityActive => CurrentSingularity != null;
        public bool ActionLocked => SingularityActive;
        public bool MovementLocked => SingularityActive;
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
        public float WeaponCooldownRate => overchargeTimer > 0f ? 2f : 1f;
        public float AttackSpeedMultiplier => timeSlipTimer > 0f && timeSlipLevel >= 3 ? 1.5f : 1f;
        public float KnockbackMultiplier => overchargeTimer > 0f && overchargeLevel >= 3 ? 1.5f : 1f;
        public event Action Changed;
        public event Action<int, string> CastRefused;

        public event Action<SkillId, Vector3, float, float> CastSucceeded;

        public event Action<SkillId, Vector3, float, float> VisualRequested;
        Combatant body;
        RunProgression progression;
        float radianceTimer, radianceFraction, radianceShare, voidWalkTimer, voidWalkDuration, timeSlipTimer, overchargeTimer;
        int overchargeLevel, timeSlipLevel;
        readonly List<TimedSkill> effects = new();
        readonly List<TimedSkill> suspendedBattlefieldEffects = new();
        bool battlefieldSuspended;
        readonly List<Combatant> nearby = new();
        readonly List<Combatant> deathNearby = new();
        readonly HashSet<Combatant> found = new();
        readonly List<SkillDefinition> candidates = new();
        readonly List<int> upgradeable = new();
        readonly Collider[] queryBuffer = new Collider[256];
        static readonly List<SkillLoadout> active = new();

        sealed class TimedSkill
        {
            public SkillId id;
            public SkillDefinition definition;
            public int level, remainingBolts;
            public float remaining, clock, interval;
            public Vector3 center;
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
        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() { active.Remove(this); ClearWaveEffects(); }
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
            ClearWaveEffects(); OnChanged();
        }

        public bool Learn(SkillId id, int replaceIndex = -1)
        {
            var definition = Catalog ? Catalog.Find(id) : null;
            if (!definition) return false;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Empty || Slots[i].Id != id) continue;
                if (Slots[i].Level >= 3 || definition.greaterVoid || id == SkillId.StormCall && Slots[i].Level >= 2) return false;
                Slots[i].Level++; OnChanged(); return true;
            }
            int place = replaceIndex;
            if (place < 0) for (int i = 0; i < Slots.Length; i++) if (Slots[i].Empty) { place = i; break; }
            if (place < 0 || place >= Slots.Length) return false;
            Slots[place].Definition = definition; Slots[place].Level = 1; Slots[place].CooldownLeft = 0f;
            progression?.UnlockEnergy(); OnChanged(); return true;
        }

        public DraftCard RollCard(int wave = 1)
        {
            if (!Catalog || Catalog.skills == null) return null;
            candidates.Clear(); upgradeable.Clear();
            foreach (var definition in Catalog.skills)
            {
                if (!definition || definition.greaterVoid) continue;
                bool owned = false;
                for (int i = 0; i < Slots.Length; i++) if (!Slots[i].Empty && Slots[i].Id == definition.id) { owned = true; break; }
                if (!owned) candidates.Add(definition);
            }
            for (int i = 0; i < Slots.Length; i++)
                if (!Slots[i].Empty && Slots[i].Level < 3 && !Slots[i].Definition.greaterVoid
                    && !(Slots[i].Id == SkillId.StormCall && Slots[i].Level >= 2)) upgradeable.Add(i);
            float upgradeChance = OwnedCount == 0 ? 0f : OwnedCount < 3 ? 0.3f : 0.6f;
            if (upgradeable.Count > 0 && (candidates.Count == 0 || UnityEngine.Random.value < upgradeChance))
            {
                var slot = Slots[upgradeable[UnityEngine.Random.Range(0, upgradeable.Count)]];
                SkillId id = slot.Id; int next = slot.Level + 1;
                return new DraftCard(DraftCardKind.Skill, ArenaText.Pair($"{slot.Name} · Level {next}",$"{slot.Name} · Level {next}"),
                    $"{slot.Definition.Description(slot.Level)} → {slot.Definition.Description(next)}",
                    _ => Learn(id), payload: id, rarity: ArenaRarity.ForSkill(slot.Definition, next));
            }
            if (candidates.Count == 0) return null;
            var picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return MakeLearnCard(picked);
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

        DraftCard MakeLearnCard(SkillDefinition definition) => new(DraftCardKind.Skill, ArenaTerms.Skill(definition.id),
            definition.Description(1) + ArenaText.Pair($"\n{definition.cost:0} Energy",$"\n{definition.cost:0} Energy"),
            replacement => Learn(definition.id, replacement), OwnedCount >= 3, definition.id,
            ArenaRarity.ForSkill(definition, 1));

        public bool TryCast(int index)
        {
            if (index < 0 || index >= Slots.Length || Slots[index].Empty || !body || !body.IsAlive) return false;
            var game = GameManager.Instance;
            if (game && !game.IsCombatActive) return Refuse(index, ArenaText.Pair("Not during a menu","Not during a menu"));
            var slot = Slots[index]; var def = slot.Definition; int level = slot.Level;
            var board = ElementStatus.For(body);
            if (ActionLocked || body.Stunned || body.State == CharacterState.Dashing || (board && board.BlocksActions) || slot.CooldownLeft > 0f)
                return Refuse(index, ArenaText.Pair("Not ready","Not ready"));
            if (!HasWorldHook(def.id)) return Refuse(index, ArenaText.Pair("World action unavailable","World action unavailable"));
            Vector3 desired = body.AimPoint;
            Vector3 origin = body.Position;
            Vector3 offset = Query.Flat(desired - origin);
            if (offset.magnitude > def.Range(level)) desired = origin + offset.normalized * def.Range(level);
            bool pointTarget = def.id is SkillId.Blink or SkillId.VoidWalk or SkillId.Bulwark or SkillId.Decoy or SkillId.GravityWell
                or SkillId.PocketVoid or SkillId.StormCall;
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
                QueryInto(origin, def.Range(level), nearby);
                float distance = float.MaxValue;
                foreach (var c in nearby)
                {
                    if ((SkillWorldAPI.IsElite?.Invoke(c) ?? false) || c is Enemy { IsBoss: true }
                        || c.Hp >= c.maxHp * def.Strength(level)) continue;
                    float aimDistance = Query.Flat(c.Position - body.AimPoint).sqrMagnitude;
                    if (aimDistance >= distance) continue;
                    distance = aimDistance; target = c;
                }
                if (!target) return Refuse(index, ArenaText.Pair("No weakened target","No weakened target"));
            }
            else if (def.id == SkillId.Contagion)
            {
                QueryInto(origin, def.Range(level), nearby);
                float distance = float.MaxValue;
                foreach (var c in nearby)
                {
                    float aimDistance = Query.Flat(c.Position - body.AimPoint).sqrMagnitude;
                    if (aimDistance >= distance) continue;
                    distance = aimDistance; target = c;
                }
                if (!target) return Refuse(index, ArenaText.Pair("No target","No target"));
            }
            if (!progression || !progression.TrySpendEnergy(def.cost)) return Refuse(index, ArenaText.Pair("No Energy","No Energy"));
            slot.CooldownLeft = def.Cooldown(level);
            Vector3 visualPoint = target ? target.Position : pointTarget ? point : origin;
            Execute(def, level, point, target);
            CastSucceeded?.Invoke(def.id, visualPoint, def.Radius(level), def.Duration(level));
            OnChanged(); return true;
        }

        void Execute(SkillDefinition def, int level, Vector3 point, Combatant target)
        {
            switch (def.id)
            {
                case SkillId.FlameWake: AddTimed(def, level, body.Position); break;
                case SkillId.Contagion:
                    ElementStatus.For(target)?.Add(ElementKind.Poison, def.Strength(level), body);
                    AddTimed(def, level, point); break;
                case SkillId.FrostNova:
                    QueryInto(body.Position, def.Radius(level), nearby);
                    foreach (var c in nearby)
                    {
                        var board = ElementStatus.For(c); bool alreadyFrozen = board && board.Frozen;
                        board?.Add(ElementKind.Frost, def.Strength(level), body);
                        if (level >= 3 && !alreadyFrozen && board && board.Frozen) board.MarkFrozenKnockbackBonus(1.5f);
                    }
                    break;
                case SkillId.StormCall: AddTimed(def, level, point); break;
                case SkillId.Bulwark:
                    SkillWorldAPI.CreateWall?.Invoke(point, def.Radius(level), def.Duration(level), level >= 3 ? def.endKnockback : 0f, body);
                    ElementStatus.For(body)?.GainShield(def.shieldScalesWithMaxHp ? body.maxHp * def.Strength(level) : def.Strength(level)); break;
                case SkillId.Radiance:
                    ElementStatus.For(body)?.ClearStatuses(); radianceTimer = def.Duration(level); radianceShare = def.Strength(level);
                    if (level >= 3) body.Heal(def.healOnCast); break;
                case SkillId.Devour:
                    Vector3 where = target.Position;
                    NotifyDevoured(target);

                    if (target.State == CharacterState.Hooked) target.SetState(CharacterState.Normal);
                    if (SkillWorldAPI.Swallow != null) SkillWorldAPI.Swallow(target, body);
                    else target.OnEnterFallZone(Vector3.zero);
                    SkillWorldAPI.FeedNearestHole?.Invoke(where, def.feedRange, 1);
                    progression.GainEnergy(def.energyReward); break;
                case SkillId.Blink:
                    Vector3 before = body.Position;
                    body.SetState(CharacterState.Normal);
                    if (SkillWorldAPI.Teleport != null) SkillWorldAPI.Teleport(body, point);
                    else
                    {
                        var rigidbody = body.GetComponent<Rigidbody>();
                        rigidbody.position = point; rigidbody.linearVelocity = Vector3.zero; body.transform.position = point;
                    }
                    if (level >= 3) SkillWorldAPI.CreateDecoy?.Invoke(before, 2f, false, body); break;
                case SkillId.Shockwave:
                    AreaHit(body.Position, def.Radius(level), 0f, def.Strength(level));
                    if (level >= 3) SkillWorldAPI.ReflectProjectiles?.Invoke(body.Position, def.Radius(level), body); break;
                case SkillId.GravityWell: AddTimed(def, level, point); break;
                case SkillId.TimeSlip:
                    timeSlipTimer = def.Duration(level); timeSlipLevel = level;
                    SkillWorldAPI.TimeSlip?.Invoke(def.Strength(level), timeSlipTimer); break;
                case SkillId.Decoy: SkillWorldAPI.CreateDecoy?.Invoke(point, def.Duration(level), level >= 3, body); break;
                case SkillId.Overcharge: overchargeTimer = def.Duration(level); overchargeLevel = level; break;
                case SkillId.PocketVoid:
                    SkillWorldAPI.CreateVoidHole?.Invoke(point, def.Radius(level), def.voidGravityWidth, def.Duration(level), body, false); break;
                case SkillId.VoidWalk:
                    VoidWalkStart = body.Position;
                    SkillWorldAPI.Teleport?.Invoke(body, point);
                    VoidWalkEnd = body.Position;
                    voidWalkTimer = voidWalkDuration = def.Duration(level);
                    SkillWorldAPI.CreateVoidHole?.Invoke(VoidWalkStart, def.voidWalkHoleDiameter, def.voidGravityWidth, def.Duration(level), body, true);
                    SkillWorldAPI.CreateVoidHole?.Invoke(VoidWalkEnd, def.voidWalkHoleDiameter, def.voidGravityWidth, def.Duration(level), body, true);
                    break;
                case SkillId.Singularity:
                    body.CancelDisplacement();
                    AddTimed(def, level, body.Position);
                    break;
            }
        }

        void AddTimed(SkillDefinition definition, int level, Vector3 point)
        {
            var effect = new TimedSkill { id = definition.id, definition = definition, level = level,
                remaining = definition.Duration(level), center = point };
            if (effect.id == SkillId.FlameWake) effect.trail.Add(body.Position);
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
            radianceTimer = Mathf.Max(0f, radianceTimer - dt);
            voidWalkTimer = Mathf.Max(0f, voidWalkTimer - worldDt);
            timeSlipTimer = Mathf.Max(0f, timeSlipTimer - dt);
            overchargeTimer = Mathf.Max(0f, overchargeTimer - dt);
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i]; var def = effect.definition; int level = effect.level;
                float activeDt = Mathf.Min(worldDt, Mathf.Max(0f, effect.remaining));
                effect.remaining = Mathf.Max(0f, effect.remaining - worldDt); effect.clock += activeDt;
                switch (effect.id)
                {
                    case SkillId.FlameWake:
                        if (effect.trail.Count < 128 && Query.Flat(body.Position - effect.trail[^1]).sqrMagnitude >= def.trailSpacing * def.trailSpacing)
                        {
                            effect.trail.Add(body.Position);
                        }
                        if (effect.clock >= 0.25f)
                        {
                            float elapsed = effect.clock; effect.clock = 0f;
                            QueryInto(body.Position, 100f, nearby);
                            foreach (var c in nearby)
                            {
                                foreach (var position in effect.trail)
                                {
                                    if (Query.Flat(c.Position - position).sqrMagnitude > def.Radius(level) * def.Radius(level)) continue;
                                    ElementStatus.For(c)?.Add(ElementKind.Fire, def.Strength(level) * elapsed, body); break;
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
                            if (c.ReceiveDamage(DamageInfo.Attack(def.ScaledDamage(def.hitDamage, level, DamageMultiplier), body, kind: HitKind.Bullet)))
                                ElementCombat.ApplyStacks(c, ElementKind.Lightning, def.hitStacks, body);
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
                        if (effect.remaining <= 0f && level >= 3) AreaHit(effect.center, def.Radius(level), def.ScaledDamage(def.endDamage, level, DamageMultiplier), def.endKnockback);
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
                            AreaHit(effect.center, def.Radius(level), damage, 0f);
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
            int count = Mathf.Min(level >= 3 ? 2 : 1, deathNearby.Count);
            for (int i = 0; i < count; i++) ElementStatus.For(deathNearby[i])?.Add(ElementKind.Poison, stacks, body);
        }

        public void FreezeBattlefieldForTrial(bool freeze)
        {
            if (freeze)
            {
                if (battlefieldSuspended) return;
                suspendedBattlefieldEffects.Clear(); suspendedBattlefieldEffects.AddRange(effects); effects.Clear();
                battlefieldSuspended = true;
            }
            else if (battlefieldSuspended)
            {
                effects.Clear(); effects.AddRange(suspendedBattlefieldEffects); suspendedBattlefieldEffects.Clear();
                battlefieldSuspended = false;
            }
        }

        public void ClearWaveEffects()
        {
            if (timeSlipTimer > 0f) SkillWorldAPI.TimeSlip?.Invoke(1f,0f);
            effects.Clear(); radianceTimer = voidWalkTimer = voidWalkDuration = timeSlipTimer = overchargeTimer = 0f;
            suspendedBattlefieldEffects.Clear(); battlefieldSuspended = false;
            radianceFraction = 0f;
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
            SkillId.VoidWalk => SkillWorldAPI.ValidPoint != null && SkillWorldAPI.Teleport != null && SkillWorldAPI.CreateVoidHole != null,
            SkillId.Singularity => SkillWorldAPI.Pull != null,
            SkillId.TimeSlip => SkillWorldAPI.TimeSlip != null,
            SkillId.Blink or SkillId.GravityWell or SkillId.StormCall => SkillWorldAPI.ValidPoint != null,
            _ => true
        };

        float KnockScale => (progression ? progression.Stats.KnockbackMultiplier : 1f) * KnockbackMultiplier;
        void AreaHit(Vector3 point, float radius, float damage, float knock)
        {
            QueryInto(point, radius, nearby);
            foreach (var c in nearby)
            {
                Vector3 direction = c.Position - point;
                if (damage > 0f) c.ReceiveDamage(DamageInfo.Attack(Mathf.Round(damage * (progression ? progression.Stats.DamageMultiplier : 1f)),
                    body, direction, knock * KnockScale, HitKind.Blast));
                else c.Knockback(direction, knock * KnockScale, KnockKind.Hit);
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
