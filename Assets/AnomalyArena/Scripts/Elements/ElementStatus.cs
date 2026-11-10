using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>One fixed status board per character. GameManager ticks all boards in one loop.
    /// Enemies follow the element rules: an element status lasts StatusRules.elementWindow after the last hit of
    /// that element and then drains; Fire is a burn rather than stacks; each Frost stack slows and each Lightning stack brings twitches
    /// and extra damage, and full stacks freeze or paralyse, clear the stacks and leave the enemy immune to control for a moment. The
    /// player's Poison follows the enemy rules too (without the wave scale); its Fire stays Burning stacks and its
    /// Frost and Lightning keep their decay per second and the immunity after a freeze or paralysis.</summary>
    [DisallowMultipleComponent]
    public sealed class ElementStatus : MonoBehaviour
    {
        // A little room on threshold checks and on the last burn tick, which falls exactly at the end of its window.
        const float Tolerance = 1e-4f;

        [field: SerializeField, Tooltip("GDD §4.6–4.8, shared candidate values.")]
        public StatusRules Rules { get; set; }
        [field: SerializeField, Tooltip("2 for a large body; elites multiply this by another 2.")]
        public float ControlThresholdFactor { get; set; } = 1f;
        public float Shield { get; private set; }
        /// <summary>The part of Shield that a level-5 Bulwark gave and no hit has used up yet. While any is left, a direct
        /// hit on the Shield counts the Bulwark's share of its damage instead of StatusRules.shieldDirectFactor, and spends these points first.</summary>
        public float BulwarkShield { get; private set; }
        float bulwarkDirectFactor = 1f;
        /// <summary>Player: Burning stacks. Enemy: the damage per second its burn deals, 0 when it is not burning (a
        /// Fire hit ignites an enemy, it adds no stacks).</summary>
        public float Fire { get; private set; }
        public float Poison { get; private set; }
        public float Frost { get; private set; }
        public float Lightning { get; private set; }
        public bool Frozen => freezeTimer > 0f;
        public bool Paralysed => paralysisTimer > 0f;
        public bool CarriesVoid { get; set; }
        public bool ImmuneToControl { get; set; }
        // Paralysis is intermittent for both teams: during each twitch the body can neither move nor act (a short stun);
        // between twitches an enemy only crawls under SpeedMultiplier. An enemy's Lightning stacks also bring the odd twitch.
        public bool BlocksMovement => Frozen || Twitching;
        public bool BlocksActions => Frozen || Twitching;
        public bool CanAct => !BlocksActions;
        public bool CarriesFrost => Frost >= 1f || Frozen;
        public bool Twitching => twitchTimer > 0f;
        public bool Burning => Fire > 0f;
        /// <summary>Enemy: for a moment after a freeze or paralysis it cannot be frozen, paralysed or made to twitch (stacks still build).</summary>
        public bool ControlImmune => controlImmune > 0f;
        public float FrostThreshold => Rules ? Mathf.Max(.01f, Rules.frostThreshold * ControlThresholdFactor) : 6f;
        public float LightningThreshold => Rules ? Mathf.Max(.01f, Rules.lightningThreshold * ControlThresholdFactor) : 6f;
        public float Chill01 => Frozen ? 1f : Mathf.Clamp01(Frost / FrostThreshold);
        /// <summary>Player move and weapon timer rate under Frost (division chill); paralysis twitches stop the player outright instead (BlocksMovement/BlocksActions).</summary>
        public float PlayerChillMultiplier => !IsPlayer || !Rules || Frozen ? 1f : 1f / (1f + Rules.frostChill * Frost / FrostThreshold);
        /// <summary>Keeps a frozen body frozen for at least this long (Blizzard spray).</summary>
        public void ExtendFreeze(float seconds) { if (Frozen && !ImmuneToControl) freezeTimer = Mathf.Max(freezeTimer, seconds); }
        /// <summary>Ends a freeze at once (Blizzard Twist shatter); the usual post-freeze immunity follows, as when it thaws by itself.</summary>
        public void BreakFreeze()
        {
            if (!Frozen) return;
            freezeTimer = 0f;
            if (!Rules) return;
            if (IsPlayer) frostImmune = Rules.frostImmunity;
            else controlImmune = Rules.controlImmunity;
        }
        /// <summary>Enemy: share of its movement and timer rate its Frost stacks take away (10% a stack, up to
        /// frostSlowMax). Every stack counts the same on any body; a large body or an elite only needs more of them to freeze.</summary>
        public float FrostSlow => IsPlayer || !Rules || Frozen ? 0f
            : Mathf.Clamp(Rules.frostSlowPerStack * Frost, 0f, Mathf.Clamp01(Rules.frostSlowMax));
        /// <summary>Enemy movement and timer rate: the Frost slow times the paralysis slow between twitches (chain). The player uses PlayerChillMultiplier.</summary>
        public float SpeedMultiplier
        {
            get
            {
                if (IsPlayer || !Rules) return 1f;
                float chill = 1f - FrostSlow * (ImmuneToControl ? .5f : 1f);
                return chill * (Paralysed ? Mathf.Clamp01(Rules.lightningSlow) : 1f);
            }
        }
        /// <summary>Enemy: multiplier on player attacks from its Lightning (+5% a stack on any body, up to
        /// lightningVulnerabilityMax). The paralysis clears the stacks, so a paralysed enemy takes lightningParalysedVulnerability instead:
        /// it counts as fully charged.</summary>
        public float LightningDamageTaken
        {
            get
            {
                if (IsPlayer || !Rules) return 1f;
                if (Paralysed) return 1f + Mathf.Max(0f, Rules.lightningParalysedVulnerability);
                return 1f + Mathf.Min(Mathf.Max(0f, Rules.lightningVulnerabilityMax), Rules.lightningVulnerabilityPerStack * Lightning);
            }
        }
        /// <summary>Enemy below the paralysis threshold: the chance of a twitch at each twitchInterval from its Lightning stacks, the same
        /// per stack on any body, up to lightningTwitchChanceMax.</summary>
        public float TwitchChance => IsPlayer || !Rules ? 0f
            : Mathf.Clamp(Rules.lightningTwitchChancePerStack * Lightning, 0f, Mathf.Clamp01(Rules.lightningTwitchChanceMax));
        public event Action<ElementKind> StatusApplied;
        public event Action<float, DamageSource, Combatant> DamageTicked;
        public event Action ShieldBroken;
        Combatant body, fireSource, poisonSource;
        float fireClock, poisonClock, freezeTimer, paralysisTimer, frostImmune, lightningImmune;
        float twitchClock, twitchTimer, shieldAge, shieldDecayClock;
        float earthFraction, lightFraction, voidFraction, fireCarry, poisonCarry;
        // Enemy rules: each element's window, the drain rate of its stacks once the window has run out, the control immunity after a
        // freeze or paralysis, and the clock of the Lightning stacks' twitch rolls.
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

        /// <summary>Time Slip slows world statuses while the player's own statuses use player time.</summary>
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
        /// <summary>Document 9.5: the share of an element's stacks this body takes. An immune body takes none, whatever the attacker's
        /// Damage Penetration.</summary>
        public float ElementFactor(ElementKind element) => 1f - immunities[(int)element];
        /// <summary>Document 9.5: the share of an element's damage this body takes from source. All of it without an immunity; against
        /// one only source's Damage Penetration share gets through.</summary>
        public float DamageFactor(ElementKind element, Combatant source = null)
        {
            float immunity = immunities[(int)element];
            if (immunity <= 0f) return 1f;
            var progression = source ? source.GetComponent<RunProgression>() : null;
            float penetration = progression ? progression.Stats.elementPenetration : 0f;
            penetration = Mathf.Clamp01(penetration + (ExtraPenetration?.Invoke(body, source) ?? 0f));
            return 1f - immunity * (1f - penetration);
        }

        /// <summary>V2 day campaign (since 2026-11-10): Damage Penetration source has against this body on top of its stat (the Armament
        /// Bay's milestone Mk against a boss). Null or 0 outside a running campaign.</summary>
        public static Func<Combatant, Combatant, float> ExtraPenetration;
        /// <summary>V2 day campaign (since 2026-11-10): the least damage a second a burn or poison from source deals to an enemy (the Energy
        /// Lab's infusion floor). Null or 0 outside a running campaign.</summary>
        public static Func<Combatant, float> StatusDamageFloor;

        public void Add(ElementKind element, float amount, Combatant source = null)
        {
            if (!Rules || !body || !body.IsAlive || amount <= 0f) return;
            // The Singularity's invincibility: no stacks reach the player meanwhile, not even a backfire's own.
            if (IsPlayer && GameManager.Instance && GameManager.Instance.skills && GameManager.Instance.skills.SingularityActive) return;
            if (V5Vfx.On && ElementFactor(element) > 0f) V5Vfx.I.ElementTouch(body, element);
            // A real hit, not a per-frame trickle (snow, ice): the sound layer holds the rate and the level down itself.
            if (ElementFactor(element) > 0f) V5WeaponAudio.ElementHit(element, body.Position, amount);
            // An enemy's Fire is a burn: Fire stacks from a source with no burn rate of its own set it burning, each stack worth
            // fireStackDamage over the window.
            if (!IsPlayer && element == ElementKind.Fire)
            {
                Ignite(amount * Rules.fireStackDamage * ElementCombat.WaveScale / Mathf.Max(.01f, Rules.elementWindow), source);
                return;
            }
            amount *= ElementFactor(element);
            // Radiance: status resistance scales stacks the player receives from others; self-inflicted backfire stays whole.
            if (IsPlayer && source != body)
            {
                var gm = GameManager.Instance;
                var skills = gm ? gm.skills : null;
                if (skills) amount *= 1f - Mathf.Clamp01(skills.StatusResistance);
            }
            if (amount <= 0f) return;
            if (IsPlayer ? AddToPlayer(element, amount, source) : AddToEnemy(element, amount, source)) OnStatusApplied(element);
        }

        /// <summary>Enemy: it burns for StatusRules.elementWindow at this much damage per second. While it already burns,
        /// the stronger rate holds and the window starts again. A body immune to Fire does not burn. The player's Fire stays stacks (Add).</summary>
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

        /// <summary>The player's stack rules: Frost and Lightning take no stacks while frozen, paralysed or in the immunity after it.
        /// Poison is as on an enemy: at most poisonOnEnemyCap, and every stack starts its window again.</summary>
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
                        // The player takes element control exactly like a monster (no 1 s cap).
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

        /// <summary>Enemy rules: every stack starts the element's window again; Poison holds at most poisonOnEnemyCap,
        /// Frost and Lightning at most their threshold. Full Frost freezes and full Lightning paralyses unless the body is still immune to
        /// control; then the stacks wait full. A frozen body takes no Frost and a paralysed one no Lightning.</summary>
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

        /// <summary>Enemy: full Frost freezes it for frostDuration and clears the stacks, unless it is still immune to control.</summary>
        void TryFreeze()
        {
            if (Frozen || controlImmune > 0f || Frost < FrostThreshold - Tolerance) return;
            Frost = 0f; frostWindow = frostDrain = 0f;
            // A control-immune body (a boss) only clears its stacks and starts the immunity window.
            if (ImmuneToControl) controlImmune = Rules.controlImmunity;
            else freezeTimer = Rules.frostDuration;
        }

        /// <summary>Enemy: full Lightning paralyses it for lightningDuration and clears the stacks, unless it is still immune to control.</summary>
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
            // The player's Poison holds for its window, then drains, and ticks as on an enemy without the wave scale.
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
            // Stacks that filled up while the body was immune to control take effect as soon as the immunity ends.
            TryFreeze();
            TryParalyse();

            if (Fire > 0f)
            {
                // The burn earns Fire per second for as long as its window lasts and pays what it has earned every fireInterval, and
                // the rest when the window ends: a heavier hit raises the rate from that moment only, and a window renewed off the tick
                // rhythm still pays its last part.
                float burning = Mathf.Clamp(fireWindow, 0f, dt);
                fireWindow -= dt;
                float interval = Mathf.Max(.01f, Rules.fireInterval);
                float burnRate = Mathf.Max(Fire, StatusDamageFloor?.Invoke(fireSource) ?? 0f);
                while (body.IsAlive)
                {
                    float step = Mathf.Min(burning, Mathf.Max(0f, interval - fireClock));
                    fireCarry += burnRate * step;
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

        /// <summary>Poison on any body: every poisonOnEnemyInterval each stack deals poisonOnEnemyDamagePerStack a second times scale (R(w)
        /// on an enemy, 1 on the player). The damage gathers in fractions and is paid in whole points; the stacks stay.</summary>
        void TickPoison(float dt, float scale)
        {
            if (Poison <= 0f) return;
            poisonClock += dt;
            float interval = Mathf.Max(.01f, Rules.poisonOnEnemyInterval);
            while (poisonClock >= interval && Poison > 0f && body.IsAlive)
            {
                poisonClock -= interval;
                float perSecond = Poison * Rules.poisonOnEnemyDamagePerStack * scale;
                if (!IsPlayer) perSecond = Mathf.Max(perSecond, StatusDamageFloor?.Invoke(poisonSource) ?? 0f);
                poisonCarry += perSecond * interval;
                float damage = Mathf.Floor(poisonCarry);
                poisonCarry -= damage;
                if (damage > 0f) OnDamageTicked(damage, DamageSource.Poison, poisonSource);
            }
        }

        /// <summary>Enemy: pays the burn damage earned so far in whole points; the fraction waits for the next payment. The tolerance keeps
        /// a sum that float steps leave just under a whole point from losing that point.</summary>
        void PayBurn()
        {
            float damage = Mathf.Floor(fireCarry + Tolerance * 10f);
            if (damage <= 0f) return;
            fireCarry -= damage;
            OnDamageTicked(damage, DamageSource.Burn, fireSource);
        }

        /// <summary>Same twitch rhythm for the player and enemies.</summary>
        void TickParalysis(float dt)
        {
            paralysisTimer -= dt;
            twitchTimer = Mathf.Max(0f, twitchTimer - dt);
            twitchClock -= dt;
            if (twitchClock <= 0f) { twitchTimer = Rules.twitchLength; twitchClock += Rules.twitchInterval; }
            if (!Paralysed) twitchTimer = 0f;
        }

        /// <summary>Enemy below the paralysis threshold: every twitchInterval its Lightning stacks give it TwitchChance of a short twitch,
        /// which, like a paralysis twitch, stops it and cuts a wind-up short. Not while it is frozen or immune to control.</summary>
        void TickStackTwitches(float dt)
        {
            twitchTimer = Mathf.Max(0f, twitchTimer - dt);
            if (Lightning <= 0f || Frozen || ImmuneToControl || controlImmune > 0f) return;
            float interval = Mathf.Max(.05f, Rules.twitchInterval);
            stackTwitchClock -= dt;
            while (stackTwitchClock <= 0f)
            {
                stackTwitchClock += interval;
                // Random.value can return 1 itself, so a full chance is taken as certain.
                float chance = TwitchChance;
                if (chance >= 1f || UnityEngine.Random.value < chance) twitchTimer = Mathf.Max(twitchTimer, Rules.twitchLength);
            }
        }

        /// <summary>Enemy, and the player's Poison: stacks hold while their window lasts; once it has run out they drain to zero within elementDrainSeconds.</summary>
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

        /// <summary>Share of a direct hit's damage that counts against the Shield: the level-5 Bulwark's own while any of its Shield is left
        /// (a hit that meets any of it counts wholly at that share), else StatusRules.shieldDirectFactor.</summary>
        float ShieldDirectFactor => BulwarkShield > 0f ? bulwarkDirectFactor : Rules.shieldDirectFactor;

        /// <summary>Returns HP damage after mitigation and spending Shield. Do not call on a rejected protected hit.
        /// ignoreShieldReduction (Lance, document 6.4): the Shield still takes the hit first, it only does not soften it.</summary>
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
            // A level-5 Bulwark's Shield may stand above the cap (GainBulwarkShield); a top-up never cuts it back down.
            Shield = Mathf.Max(Shield, Mathf.Min(cap, Shield + points)); shieldAge = 0f; shieldDecayClock = 0f;
        }
        /// <summary>The Bulwark's Shield: added like GainShield, except that the player's cap never cuts it below its own
        /// size, so the level-5 Shield (60% of max HP) goes past the usual cap. directFactor, given at level 5, marks that many points of the
        /// Shield as the Bulwark's own (BulwarkShield, never more than the whole Shield): while any are left, a direct hit counts that share
        /// of its damage. The level-5 Shield stands above any other Shield the player can hold, so the cast lifts the Shield to exactly its
        /// size and all of it counts as the Bulwark's: a smaller Shield it lifts merges into it rather than staying apart.</summary>
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
        /// <summary>Ends a freeze or paralysis at once and drops the Frost and Lightning stacks; Fire, Poison and Shield stay, and no
        /// post-control immunity follows (the Singularity's invincibility).</summary>
        public void ClearControl()
        {
            Frost = Lightning = 0f;
            freezeTimer = paralysisTimer = twitchClock = twitchTimer = 0f;
        }

        /// <summary>V2 day campaign (since 2026-11-10): the factor on what a body is paid as a share of the damage it deals (Earth, Light and
        /// Void here, Radiance in SkillLoadout.HealFromDamage); null or 1 outside a running campaign.</summary>
        public static Func<Combatant, float> PayoffScale;

        public void PayWielder(ElementKind element, float damage)
        {
            if (!body || !body.IsAlive || !Rules || damage <= 0f) return;
            damage *= Mathf.Max(0f, PayoffScale?.Invoke(body) ?? 1f);
            if (V5Vfx.On) V5Vfx.I.WielderPay(body, element);
            V5WeaponAudio.ElementHit(element, body.Position);
            switch (element)
            {
                case ElementKind.Earth:
                    earthFraction += damage * Rules.earthShare;
                    int shield = Mathf.FloorToInt(earthFraction); earthFraction -= shield; GainShield(shield); break;
                case ElementKind.Light:
                    // Document 6.2: a flat share of the damage, whatever the wielder's HP.
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
            if (body) V5WeaponAudio.At(V5Sfx.RingShieldBreak, body.Position, .5f, 1.15f); // a Warden's shield or your own going: heard, lower than the Energy Shield's break
        }
    }
}
