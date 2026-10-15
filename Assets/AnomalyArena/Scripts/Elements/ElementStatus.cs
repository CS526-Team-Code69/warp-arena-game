using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    [DisallowMultipleComponent]
    public sealed class ElementStatus : MonoBehaviour
    {
        [field: SerializeField, Tooltip("GDD §4.6–4.8, shared candidate values.")]
        public StatusRules Rules { get; set; }
        [field: SerializeField, Tooltip("2 for a large body; elites multiply this by another 2.")]
        public float ControlThresholdFactor { get; set; } = 1f;
        public float Shield { get; private set; }
        public float Fire { get; private set; }
        public float Poison { get; private set; }
        public float Frost { get; private set; }
        public float Lightning { get; private set; }
        public bool Frozen => freezeTimer > 0f;
        public bool Paralysed => paralysisTimer > 0f;
        public bool CarriesVoid { get; set; }
        public bool ImmuneToControl { get; set; }

        public bool BlocksMovement => Frozen;
        public bool BlocksActions => Frozen || (IsPlayer ? Paralysed : twitchTimer > 0f);
        public bool CanAct => !BlocksActions;
        public bool CarriesFrost => Frost >= 1f || Frozen;
        public bool Twitching => twitchTimer > 0f;
        public float FrostThreshold => Rules ? Mathf.Max(.01f, Rules.frostThreshold * ControlThresholdFactor) : 6f;
        public float Chill01 => Frozen ? 1f : Mathf.Clamp01(Frost / FrostThreshold);

        public float SpeedMultiplier
        {
            get
            {
                if (IsPlayer || !Rules) return 1f;
                float chill = Frozen ? 1f : 1f / (1f + Rules.frostChill * (ImmuneToControl ? .5f : 1f) * Frost / FrostThreshold);
                return chill * (Paralysed ? Mathf.Clamp01(Rules.lightningSlow) : 1f);
            }
        }
        public float FrozenKnockbackMultiplier { get; private set; } = 1f;
        public event Action<ElementKind> StatusApplied;
        public event Action<float, DamageSource, Combatant> DamageTicked;
        public event Action<float> OnControlThreshold;
        public event Action ShieldBroken;
        Combatant body, fireSource, poisonSource;
        float fireClock, poisonClock, freezeTimer, paralysisTimer, frostImmune, lightningImmune;
        float twitchClock, twitchTimer, shieldAge, shieldDecayClock;
        float earthFraction, lightFraction, voidFraction, fireCarry, poisonCarry;
        readonly float[] immunities = new float[8];
        static readonly List<ElementStatus> active = new();
        static readonly Dictionary<Combatant, ElementStatus> byBody = new();
        public bool IsPlayer => body && body.Team == Team.Player;

        public static ElementStatus For(Combatant combatant)
        {
            if (!combatant) return null;
            return byBody.TryGetValue(combatant, out var status) ? status : combatant.GetComponent<ElementStatus>();
        }

        void Awake() { body = GetComponent<Combatant>(); if (body) byBody[body] = this; }
        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() => active.Remove(this);
        void OnDestroy() { if (!ReferenceEquals(body, null)) byBody.Remove(body); }

        public static void TickAll(float dt) => TickAll(dt, dt);

        public static void TickAll(float worldDeltaTime, float playerDeltaTime)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                var status = active[i];
                if (status && status.body && status.body.IsAlive && status.Rules)
                    status.Tick(status.IsPlayer ? playerDeltaTime : worldDeltaTime);
            }
        }

        public void ResetForSpawn()
        {
            ClearStatuses(); Shield = 0f; shieldAge = 0f; shieldDecayClock = 0f;
            CarriesVoid = false; ImmuneToControl = false; ControlThresholdFactor = 1f;
            earthFraction = lightFraction = voidFraction = 0f;
            Array.Clear(immunities, 0, immunities.Length);
        }

        public void SetImmunity(ElementKind element, float factor) => immunities[(int)element] = Mathf.Clamp01(factor);
        public float ElementFactor(ElementKind element, Combatant source = null)
        {
            var progression = source ? source.GetComponent<RunProgression>() : null;
            float penetration = progression ? progression.Stats.elementPenetration : 0f;
            return 1f - immunities[(int)element] * (1f - penetration);
        }

        public void Add(ElementKind element, float amount, Combatant source = null)
        {
            if (!Rules || !body || !body.IsAlive || amount <= 0f) return;
            amount *= ElementFactor(element, source);
            if (amount <= 0f) return;
            switch (element)
            {
                case ElementKind.Fire:
                    if (Fire <= 0f) fireClock = 0f;
                    Fire = Mathf.Min(Rules.fireCap, Fire + amount); fireSource = source; break;
                case ElementKind.Poison:
                    if (Poison <= 0f) poisonClock = 0f;
                    Poison = Mathf.Min(IsPlayer ? Rules.poisonCap : Rules.poisonOnEnemyCap, Poison + amount); poisonSource = source; break;
                case ElementKind.Frost:
                    if (frostImmune > 0f || Frozen) return;
                    Frost += amount;
                    if (Frost >= Rules.frostThreshold * ControlThresholdFactor)
                    {
                        Frost = 0f;
                        if (ImmuneToControl) { frostImmune = Rules.frostImmunity; OnControlThreshold?.Invoke(Rules.bossControlStagger); }
                        else freezeTimer = IsPlayer ? Mathf.Min(Rules.playerControlMax, Rules.frostDuration) : Rules.frostDuration;
                    }
                    break;
                case ElementKind.Lightning:
                    if (lightningImmune > 0f || Paralysed) return;
                    Lightning += amount * (CarriesFrost ? Mathf.Max(1f, Rules.frostLightningStackMultiplier) : 1f);
                    if (Lightning >= Rules.lightningThreshold * ControlThresholdFactor)
                    {
                        Lightning = 0f;
                        if (ImmuneToControl) { lightningImmune = Rules.lightningImmunity; OnControlThreshold?.Invoke(Rules.bossControlStagger); }
                        else
                        {
                            paralysisTimer = IsPlayer ? Mathf.Min(Rules.playerControlMax, Rules.lightningDuration) : Rules.lightningDuration;
                            twitchClock = Rules.twitchInterval;
                        }
                    }
                    break;
                default: break;
            }
            OnStatusApplied(element);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || !Rules || !body || !body.IsAlive) return;
            frostImmune = Mathf.Max(0f, frostImmune - dt);
            lightningImmune = Mathf.Max(0f, lightningImmune - dt);
            if (Frozen)
            {
                freezeTimer -= dt;
                if (!Frozen) { frostImmune = IsPlayer ? Rules.playerControlImmunity : Rules.frostImmunity; FrozenKnockbackMultiplier = 1f; }
            }
            else Frost = Mathf.Max(0f, Frost - Rules.frostDecay * dt);
            if (Paralysed)
            {
                paralysisTimer -= dt;
                twitchTimer = Mathf.Max(0f, twitchTimer - dt);
                twitchClock -= dt;
                if (!IsPlayer && twitchClock <= 0f) { twitchTimer = Rules.twitchLength; twitchClock += Rules.twitchInterval; }
                if (!Paralysed)
                {
                    twitchTimer = 0f;
                    lightningImmune = IsPlayer ? Rules.playerControlImmunity : Rules.lightningImmunity;
                }
            }
            else Lightning = Mathf.Max(0f, Lightning - Rules.lightningDecay * dt);

            if (Fire > 0f)
            {
                fireClock += dt;
                while (fireClock >= Rules.fireInterval && Fire > 0f && body.IsAlive)
                {
                    fireClock -= Rules.fireInterval;
                    float damage;
                    if (IsPlayer)
                    {
                        damage = Mathf.Floor(Fire);
                        Fire = Mathf.Max(0f, Fire - 1f);
                    }
                    else
                    {

                        float burned = Fire * Rules.fireBurnFraction;
                        Fire -= burned;
                        if (Fire < .1f) { burned += Fire; Fire = 0f; }
                        fireCarry += burned * Rules.fireDamagePerStack * ElementCombat.WaveScale;
                        damage = Mathf.Floor(fireCarry);
                        fireCarry -= damage;
                    }
                    if (damage > 0f) OnDamageTicked(damage, DamageSource.Burn, fireSource);
                }
            }
            if (IsPlayer ? Poison >= 1f : Poison > 0f)
            {
                poisonClock += dt;
                while (poisonClock >= Rules.poisonInterval && body.IsAlive)
                {
                    poisonClock -= Rules.poisonInterval;
                    float damage;
                    if (IsPlayer) damage = Mathf.Ceil(Mathf.Floor(Poison) * Rules.poisonDamagePerStack);
                    else
                    {

                        poisonCarry += Poison * Rules.poisonOnEnemyDamagePerStack * ElementCombat.WaveScale;
                        damage = Mathf.Floor(poisonCarry);
                        poisonCarry -= damage;
                    }
                    if (damage > 0f) OnDamageTicked(damage, DamageSource.Poison, poisonSource);
                }
            }
            if (IsPlayer && Shield > 0f)
            {
                shieldAge += dt;
                if (shieldAge >= Rules.shieldDecayDelay)
                {
                    shieldDecayClock += dt;
                    if (shieldDecayClock >= 1f)
                    {
                        float decay = Mathf.Floor(shieldDecayClock);
                        shieldDecayClock -= decay;
                        Shield = Mathf.Max(0f, Shield - decay * Rules.shieldDecayPerSecond);
                    }
                }
            }
        }

        public float ResolveDamage(float amount, DamageSource source = DamageSource.Hit, bool ignoreShield = false)
        {
            amount = Mathf.Max(0f, amount);
            if (ignoreShield || source == DamageSource.Poison || Shield <= 0f) return amount;
            amount = source == DamageSource.Burn ? Mathf.Ceil(amount * Rules.fireShieldFactor)
                : Mathf.Round(amount * Rules.shieldDirectFactor);
            float absorbed = Mathf.Min(Shield, amount);
            Shield -= absorbed;
            if (Shield <= 0f && absorbed > 0f) OnShieldBroken();
            return amount - absorbed;
        }

        public void GainShield(float points, bool capToPlayer = false)
        {
            if (points <= 0f || !Rules) return;
            float cap = IsPlayer || capToPlayer ? (body ? body.maxHp : 50f) * Rules.playerShieldCapShare : float.MaxValue;
            Shield = Mathf.Min(cap, Shield + points); shieldAge = 0f; shieldDecayClock = 0f;
        }
        public void SetShield(float points) { Shield = Mathf.Max(0f, points); shieldAge = 0f; shieldDecayClock = 0f; }
        public void OnHeal() { if (IsPlayer) Poison = Mathf.Max(0f, Poison - 1f); }
        public void MarkFrozenKnockbackBonus(float multiplier) { if (Frozen) FrozenKnockbackMultiplier = Mathf.Max(1f, multiplier); }
        public void ClearStatuses()
        {
            Fire = Poison = Frost = Lightning = 0f;
            fireClock = poisonClock = freezeTimer = paralysisTimer = frostImmune = lightningImmune = twitchClock = twitchTimer = 0f;
            fireCarry = poisonCarry = 0f;
            fireSource = poisonSource = null;
            FrozenKnockbackMultiplier = 1f;
        }

        public void PayWielder(ElementKind element, float damage)
        {
            if (!body || !body.IsAlive || !Rules || damage <= 0f) return;
            switch (element)
            {
                case ElementKind.Earth:
                    earthFraction += damage * Rules.earthShare;
                    int shield = Mathf.FloorToInt(earthFraction); earthFraction -= shield; GainShield(shield); break;
                case ElementKind.Light:
                    float missing = body.maxHp > 0f ? Mathf.Clamp01(1f - body.Hp / body.maxHp) : 0f;
                    lightFraction += damage * Rules.lightShare * (1f + Rules.lightMissingHpBonus * missing);
                    int hp = Mathf.FloorToInt(lightFraction); lightFraction -= hp; if (hp > 0) body.Heal(hp); break;
                case ElementKind.Void:
                    voidFraction += damage * Rules.voidShare;
                    int energy = Mathf.FloorToInt(voidFraction); voidFraction -= energy;
                    if (energy > 0) body.GetComponent<RunProgression>()?.GainEnergy(energy); break;
            }
        }
        public string Summary => $"Fire {Fire:0}  Poison {Poison:0}  Frost {Frost:0}  Lightning {Lightning:0}"
            + (Frozen ? "  FROZEN" : "") + (Paralysed ? "  PARALYSED" : "");
        void OnStatusApplied(ElementKind element) => StatusApplied?.Invoke(element);
        void OnDamageTicked(float amount, DamageSource source, Combatant dealer) => DamageTicked?.Invoke(amount, source, dealer);
        void OnShieldBroken() => ShieldBroken?.Invoke();
    }
}
