using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    [DisallowMultipleComponent]
    public sealed class ElementStatus : MonoBehaviour
    {

        const float Tolerance = 1e-4f;

        [field: SerializeField, Tooltip("GDD §4.6–4.8, shared candidate values.")]
        public StatusRules Rules { get; set; }
        [field: SerializeField, Tooltip("2 for a large body; elites multiply this by another 2.")]
        public float ControlThresholdFactor { get; set; } = 1f;
        public float Shield { get; private set; }

        public float BulwarkShield { get; private set; }
        float bulwarkDirectFactor = 1f;

        public float Fire { get; private set; }
        public float Poison { get; private set; }
        public float Frost { get; private set; }
        public float Lightning { get; private set; }
        public bool Frozen => freezeTimer > 0f;
        public bool Paralysed => paralysisTimer > 0f;
        public bool CarriesVoid { get; set; }
        public bool ImmuneToControl { get; set; }

        public bool BlocksMovement => Frozen || Twitching;
        public bool BlocksActions => Frozen || Twitching;
        public bool CanAct => !BlocksActions;
        public bool CarriesFrost => Frost >= 1f || Frozen;
        public bool Twitching => twitchTimer > 0f;
        public bool Burning => Fire > 0f;

        public bool ControlImmune => controlImmune > 0f;
        public float FrostThreshold => Rules ? Mathf.Max(.01f, Rules.frostThreshold * ControlThresholdFactor) : 6f;
        public float LightningThreshold => Rules ? Mathf.Max(.01f, Rules.lightningThreshold * ControlThresholdFactor) : 6f;
        public float Chill01 => Frozen ? 1f : Mathf.Clamp01(Frost / FrostThreshold);

        public float PlayerChillMultiplier => !IsPlayer || !Rules || Frozen ? 1f : 1f / (1f + Rules.frostChill * Frost / FrostThreshold);

        public void ExtendFreeze(float seconds) { if (Frozen && !ImmuneToControl) freezeTimer = Mathf.Max(freezeTimer, seconds); }

        public void BreakFreeze()
        {
            if (!Frozen) return;
            freezeTimer = 0f;
            if (!Rules) return;
            if (IsPlayer) frostImmune = Rules.frostImmunity;
            else controlImmune = Rules.controlImmunity;
        }

        public float FrostSlow => IsPlayer || !Rules || Frozen ? 0f
            : Mathf.Clamp(Rules.frostSlowPerStack * Frost, 0f, Mathf.Clamp01(Rules.frostSlowMax));

        public float SpeedMultiplier
        {
            get
            {
                if (IsPlayer || !Rules) return 1f;
                float chill = 1f - FrostSlow * (ImmuneToControl ? .5f : 1f);
                return chill * (Paralysed ? Mathf.Clamp01(Rules.lightningSlow) : 1f);
            }
        }

        public float LightningDamageTaken
        {
            get
            {
                if (IsPlayer || !Rules) return 1f;
                if (Paralysed) return 1f + Mathf.Max(0f, Rules.lightningParalysedVulnerability);
                return 1f + Mathf.Min(Mathf.Max(0f, Rules.lightningVulnerabilityMax), Rules.lightningVulnerabilityPerStack * Lightning);
            }
        }

        public float TwitchChance => IsPlayer || !Rules ? 0f
            : Mathf.Clamp(Rules.lightningTwitchChancePerStack * Lightning, 0f, Mathf.Clamp01(Rules.lightningTwitchChanceMax));
        public event Action<ElementKind> StatusApplied;
        public event Action<float, DamageSource, Combatant> DamageTicked;
        public event Action ShieldBroken;
        Combatant body, fireSource, poisonSource;
        float fireClock, poisonClock, freezeTimer, paralysisTimer, frostImmune, lightningImmune;
        float twitchClock, twitchTimer, shieldAge, shieldDecayClock;
        float earthFraction, lightFraction, voidFraction, fireCarry, poisonCarry;

        float fireWindow, poisonWindow, frostWindow, lightningWindow, poisonDrain, frostDrain, lightningDrain, controlImmune, stackTwitchClock;
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
            ElementCombat.AdvanceClock(worldDeltaTime);
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
            ClearStatuses(); Shield = BulwarkShield = 0f; shieldAge = 0f; shieldDecayClock = 0f;
            CarriesVoid = false; ImmuneToControl = false; ControlThresholdFactor = 1f;
            earthFraction = lightFraction = voidFraction = 0f;
            Array.Clear(immunities, 0, immunities.Length);
        }

        public void SetImmunity(ElementKind element, float factor) => immunities[(int)element] = Mathf.Clamp01(factor);

        public float ElementFactor(ElementKind element) => 1f - immunities[(int)element];

        public float DamageFactor(ElementKind element, Combatant source = null)
        {
            float immunity = immunities[(int)element];
            if (immunity <= 0f) return 1f;
            var progression = source ? source.GetComponent<RunProgression>() : null;
            float penetration = progression ? Mathf.Clamp01(progression.Stats.elementPenetration) : 0f;
            return 1f - immunity * (1f - penetration);
        }

        public void Add(ElementKind element, float amount, Combatant source = null)
        {
            if (!Rules || !body || !body.IsAlive || amount <= 0f) return;

            if (IsPlayer && GameManager.Instance && GameManager.Instance.skills && GameManager.Instance.skills.SingularityActive) return;
            if (V5Vfx.On && ElementFactor(element) > 0f) V5Vfx.I.ElementTouch(body, element);

            if (ElementFactor(element) > 0f) V5WeaponAudio.ElementHit(element, body.Position, amount);

            if (!IsPlayer && element == ElementKind.Fire)
            {
                Ignite(amount * Rules.fireStackDamage * ElementCombat.WaveScale / Mathf.Max(.01f, Rules.elementWindow), source);
                return;
            }
            amount *= ElementFactor(element);

            if (IsPlayer && source != body)
            {
                var gm = GameManager.Instance;
                var skills = gm ? gm.skills : null;
                if (skills) amount *= 1f - Mathf.Clamp01(skills.StatusResistance);
            }
            if (amount <= 0f) return;
            if (IsPlayer ? AddToPlayer(element, amount, source) : AddToEnemy(element, amount, source)) OnStatusApplied(element);
        }

        public void Ignite(float burnPerSecond, Combatant source = null)
        {
            if (!Rules || !body || !body.IsAlive || IsPlayer || burnPerSecond <= 0f || ElementFactor(ElementKind.Fire) <= 0f) return;
            if (fireWindow <= 0f) { Fire = 0f; fireClock = 0f; }
            Fire = Mathf.Max(Fire, burnPerSecond);
            fireWindow = Rules.elementWindow;
            fireSource = source;
            OnStatusApplied(ElementKind.Fire);
            V5WeaponAudio.ElementHit(ElementKind.Fire, body.Position);
        }

        bool AddToPlayer(ElementKind element, float amount, Combatant source)
        {
            switch (element)
            {
                case ElementKind.Fire:
                    if (Fire <= 0f) fireClock = 0f;
                    Fire = Mathf.Min(Rules.fireCap, Fire + amount); fireSource = source; return true;
                case ElementKind.Poison:
                    if (Poison <= 0f) poisonClock = 0f;
                    Poison = Mathf.Min(Rules.poisonOnEnemyCap, Poison + amount); poisonSource = source;
                    poisonWindow = Rules.elementWindow; poisonDrain = 0f;
                    return true;
                case ElementKind.Frost:
                    if (frostImmune > 0f || Frozen) return false;
                    Frost += amount;
                    if (Frost >= FrostThreshold)
                    {
                        Frost = 0f;
                        if (ImmuneToControl) frostImmune = Rules.frostImmunity;

                        else freezeTimer = Rules.frostDuration;
                    }
                    return true;
                case ElementKind.Lightning:
                    if (lightningImmune > 0f || Paralysed) return false;
                    Lightning += amount * (CarriesFrost ? Mathf.Max(1f, Rules.frostLightningStackMultiplier) : 1f);
                    if (Lightning >= LightningThreshold)
                    {
                        Lightning = 0f;
                        if (ImmuneToControl) lightningImmune = Rules.lightningImmunity;
                        else
                        {
                            paralysisTimer = Rules.lightningDuration;
                            twitchClock = Rules.twitchInterval;
                        }
                    }
                    return true;
                default: return true;
            }
        }

        bool AddToEnemy(ElementKind element, float amount, Combatant source)
        {
            switch (element)
            {
                case ElementKind.Poison:
                    if (Poison <= 0f) poisonClock = 0f;
                    Poison = Mathf.Min(Rules.poisonOnEnemyCap, Poison + amount); poisonSource = source;
                    poisonWindow = Rules.elementWindow; poisonDrain = 0f;
                    return true;
                case ElementKind.Frost:
                    if (Frozen) return false;
                    Frost = Mathf.Min(FrostThreshold, Frost + amount);
                    frostWindow = Rules.elementWindow; frostDrain = 0f;
                    TryFreeze();
                    return true;
                case ElementKind.Lightning:
                    if (Paralysed) return false;
                    if (Lightning <= 0f) stackTwitchClock = Rules.twitchInterval;
                    Lightning = Mathf.Min(LightningThreshold, Lightning + amount * (CarriesFrost ? Mathf.Max(1f, Rules.frostLightningStackMultiplier) : 1f));
                    lightningWindow = Rules.elementWindow; lightningDrain = 0f;
                    TryParalyse();
                    return true;
                default: return true;
            }
        }

        void TryFreeze()
        {
            if (Frozen || controlImmune > 0f || Frost < FrostThreshold - Tolerance) return;
            Frost = 0f; frostWindow = frostDrain = 0f;

            if (ImmuneToControl) controlImmune = Rules.controlImmunity;
            else freezeTimer = Rules.frostDuration;
        }

        void TryParalyse()
        {
            if (Paralysed || controlImmune > 0f || Lightning < LightningThreshold - Tolerance) return;
            Lightning = 0f; lightningWindow = lightningDrain = 0f;
            if (ImmuneToControl) controlImmune = Rules.controlImmunity;
            else
            {
                paralysisTimer = Rules.lightningDuration;
                twitchClock = Rules.twitchInterval;
            }
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || !Rules || !body || !body.IsAlive) return;
            if (IsPlayer) TickPlayer(dt);
            else TickEnemy(dt);
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
                        BulwarkShield = Mathf.Min(BulwarkShield, Shield);
                    }
                }
            }
        }

        void TickPlayer(float dt)
        {
            frostImmune = Mathf.Max(0f, frostImmune - dt);
            lightningImmune = Mathf.Max(0f, lightningImmune - dt);
            if (Frozen)
            {
                freezeTimer -= dt;
                if (!Frozen) frostImmune = Rules.frostImmunity;
            }
            else Frost = Mathf.Max(0f, Frost - Rules.frostDecay * dt);
            if (Paralysed)
            {
                TickParalysis(dt);
                if (!Paralysed) lightningImmune = Rules.lightningImmunity;
            }
            else Lightning = Mathf.Max(0f, Lightning - Rules.lightningDecay * dt);

            if (Fire > 0f)
            {
                fireClock += dt;
                while (fireClock >= Rules.fireInterval && Fire > 0f && body.IsAlive)
                {
                    fireClock -= Rules.fireInterval;
                    float damage = Mathf.Floor(Fire);
                    Fire = Mathf.Max(0f, Fire - 1f);
                    if (damage > 0f) OnDamageTicked(damage, DamageSource.Burn, fireSource);
                }
            }

            Poison = Drain(Poison, ref poisonWindow, ref poisonDrain, dt);
            TickPoison(dt, 1f);
        }

        void TickEnemy(float dt)
        {
            controlImmune = Mathf.Max(0f, controlImmune - dt);
            if (Frozen)
            {
                freezeTimer -= dt;
                if (!Frozen) controlImmune = Rules.controlImmunity;
            }
            if (Paralysed)
            {
                TickParalysis(dt);
                if (!Paralysed) controlImmune = Rules.controlImmunity;
            }
            else TickStackTwitches(dt);
            Poison = Drain(Poison, ref poisonWindow, ref poisonDrain, dt);
            Frost = Drain(Frost, ref frostWindow, ref frostDrain, dt);
            Lightning = Drain(Lightning, ref lightningWindow, ref lightningDrain, dt);

            TryFreeze();
            TryParalyse();

            if (Fire > 0f)
            {

                float burning = Mathf.Clamp(fireWindow, 0f, dt);
                fireWindow -= dt;
                float interval = Mathf.Max(.01f, Rules.fireInterval);
                while (body.IsAlive)
                {
                    float step = Mathf.Min(burning, Mathf.Max(0f, interval - fireClock));
                    fireCarry += Fire * step;
                    fireClock += step;
                    burning -= step;
                    if (fireClock < interval - Tolerance) break;
                    fireClock -= interval;
                    PayBurn();
                }
                if (fireWindow <= 0f)
                {
                    if (body.IsAlive) PayBurn();
                    Fire = 0f; fireWindow = 0f; fireClock = 0f;
                }
            }
            TickPoison(dt, ElementCombat.WaveScale);
        }

        void TickPoison(float dt, float scale)
        {
            if (Poison <= 0f) return;
            poisonClock += dt;
            float interval = Mathf.Max(.01f, Rules.poisonOnEnemyInterval);
            while (poisonClock >= interval && Poison > 0f && body.IsAlive)
            {
                poisonClock -= interval;
                poisonCarry += Poison * Rules.poisonOnEnemyDamagePerStack * interval * scale;
                float damage = Mathf.Floor(poisonCarry);
                poisonCarry -= damage;
                if (damage > 0f) OnDamageTicked(damage, DamageSource.Poison, poisonSource);
            }
        }

        void PayBurn()
        {
            float damage = Mathf.Floor(fireCarry + Tolerance * 10f);
            if (damage <= 0f) return;
            fireCarry -= damage;
            OnDamageTicked(damage, DamageSource.Burn, fireSource);
        }

        void TickParalysis(float dt)
        {
            paralysisTimer -= dt;
            twitchTimer = Mathf.Max(0f, twitchTimer - dt);
            twitchClock -= dt;
            if (twitchClock <= 0f) { twitchTimer = Rules.twitchLength; twitchClock += Rules.twitchInterval; }
            if (!Paralysed) twitchTimer = 0f;
        }

        void TickStackTwitches(float dt)
        {
            twitchTimer = Mathf.Max(0f, twitchTimer - dt);
            if (Lightning <= 0f || Frozen || ImmuneToControl || controlImmune > 0f) return;
            float interval = Mathf.Max(.05f, Rules.twitchInterval);
            stackTwitchClock -= dt;
            while (stackTwitchClock <= 0f)
            {
                stackTwitchClock += interval;

                float chance = TwitchChance;
                if (chance >= 1f || UnityEngine.Random.value < chance) twitchTimer = Mathf.Max(twitchTimer, Rules.twitchLength);
            }
        }

        float Drain(float stacks, ref float window, ref float drain, float dt)
        {
            if (stacks <= 0f) { window = drain = 0f; return 0f; }
            if (window > 0f)
            {
                window -= dt;
                if (window > 0f) return stacks;
                dt = -window;
                window = 0f;
            }
            if (Rules.elementDrainSeconds <= 0f) { drain = 0f; return 0f; }
            if (drain <= 0f) drain = stacks / Rules.elementDrainSeconds;
            stacks -= drain * dt;
            if (stacks > 0f) return stacks;
            drain = 0f;
            return 0f;
        }

        float ShieldDirectFactor => BulwarkShield > 0f ? bulwarkDirectFactor : Rules.shieldDirectFactor;

        public float ResolveDamage(float amount, DamageSource source = DamageSource.Hit, bool ignoreShield = false, bool ignoreShieldReduction = false)
        {
            amount = Mathf.Max(0f, amount);
            if (ignoreShield || source == DamageSource.Poison || Shield <= 0f) return amount;
            if (!ignoreShieldReduction)
                amount = source == DamageSource.Burn ? Mathf.Ceil(amount * Rules.fireShieldFactor)
                    : Mathf.Round(amount * ShieldDirectFactor);
            float absorbed = Mathf.Min(Shield, amount);
            Shield -= absorbed;
            BulwarkShield = Mathf.Clamp(BulwarkShield - absorbed, 0f, Shield);
            if (Shield <= 0f && absorbed > 0f) OnShieldBroken();
            return amount - absorbed;
        }

        public void GainShield(float points, bool capToPlayer = false)
        {
            if (points <= 0f || !Rules) return;
            float cap = IsPlayer || capToPlayer ? (body ? body.maxHp : 50f) * Rules.playerShieldCapShare : float.MaxValue;

            Shield = Mathf.Max(Shield, Mathf.Min(cap, Shield + points)); shieldAge = 0f; shieldDecayClock = 0f;
        }

        public void GainBulwarkShield(float points, float? directFactor = null)
        {
            if (points <= 0f || !Rules) return;
            float cap = IsPlayer ? Mathf.Max(points, (body ? body.maxHp : 50f) * Rules.playerShieldCapShare) : float.MaxValue;
            Shield = Mathf.Max(Shield, Mathf.Min(cap, Shield + points)); shieldAge = 0f; shieldDecayClock = 0f;
            if (!directFactor.HasValue) return;
            BulwarkShield = Mathf.Min(Shield, BulwarkShield + points);
            bulwarkDirectFactor = Mathf.Clamp01(directFactor.Value);
        }
        public void SetShield(float points) { Shield = Mathf.Max(0f, points); BulwarkShield = Mathf.Min(BulwarkShield, Shield); shieldAge = 0f; shieldDecayClock = 0f; }
        public void OnHeal() { if (IsPlayer) Poison = Mathf.Max(0f, Poison - 1f); }
        public void ClearStatuses()
        {
            Fire = Poison = Frost = Lightning = 0f;
            fireClock = poisonClock = freezeTimer = paralysisTimer = frostImmune = lightningImmune = twitchClock = twitchTimer = 0f;
            fireWindow = poisonWindow = frostWindow = lightningWindow = poisonDrain = frostDrain = lightningDrain = controlImmune = stackTwitchClock = 0f;
            fireCarry = poisonCarry = 0f;
            fireSource = poisonSource = null;
        }

        public void ClearControl()
        {
            Frost = Lightning = 0f;
            freezeTimer = paralysisTimer = twitchClock = twitchTimer = 0f;
        }

        public void PayWielder(ElementKind element, float damage)
        {
            if (!body || !body.IsAlive || !Rules || damage <= 0f) return;
            if (V5Vfx.On) V5Vfx.I.WielderPay(body, element);
            V5WeaponAudio.ElementHit(element, body.Position);
            switch (element)
            {
                case ElementKind.Earth:
                    earthFraction += damage * Rules.earthShare;
                    int shield = Mathf.FloorToInt(earthFraction); earthFraction -= shield; GainShield(shield); break;
                case ElementKind.Light:

                    lightFraction += damage * Rules.lightShare;
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
        void OnShieldBroken()
        {
            ShieldBroken?.Invoke();
            if (body) V5WeaponAudio.At(V5Sfx.RingShieldBreak, body.Position, .5f, 1.15f);                                                                                    
        }
    }
}
