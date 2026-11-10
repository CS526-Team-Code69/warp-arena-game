using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    public static class ElementCombat
    {
        static readonly Collider[] buffer = new Collider[256];
        static readonly HashSet<Combatant> visited = new();
        static readonly List<Combatant> queue = new();
        // Continuous sources: for each (source, target) pair, the element-clock time of its next turn to add stacks, and the target's
        // spawn version then, so a pooled enemy coming back is not held to its previous life's turns.
        static readonly Dictionary<(object source, Combatant target), (float next, int spawn)> continuousNext = new();
        static readonly List<(object source, Combatant target)> expired = new();
        // Fields tick on the physics step while the element clock moves per frame, so a source ticking exactly every interval can land
        // a little before its turn: a hit this share of the interval early still takes it. Turns stay a whole interval apart, so over
        // time a source never adds stacks more than once per interval.
        const float ContinuousEarlyShare = .1f;
        // Past this many remembered pairs, the ones past their turn are dropped before a new one is added.
        const int ContinuousPruneAt = 512;

        /// <summary>World seconds of combat, advanced with the status boards (ElementStatus.TickAll): it stops while a menu pauses
        /// the game and slows under Time Stop, like the statuses it gates.</summary>
        public static float Clock { get; private set; }

        public static void AdvanceClock(float worldDeltaTime) { if (worldDeltaTime > 0f) Clock += worldDeltaTime; }

        /// <summary>Wave end: forgets which continuous sources hit which targets.</summary>
        public static void ClearContinuousHits() => continuousNext.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime() { Clock = 0f; continuousNext.Clear(); }

        /// <summary>Wave budget scale R(w); 1 outside a V2 run so isolated tests keep raw values.</summary>
        public static float WaveScale
        {
            get { var gm = GameManager.Instance; return gm && gm.waves ? Mathf.Max(1f, gm.waves.CombatScale) : 1f; }
        }

        public static Color ColorFor(ElementKind element) => element switch
        {
            ElementKind.Fire => new Color(1f, 0.44f, 0.18f),
            ElementKind.Poison => new Color(0.62f, 0.89f, 0.36f),
            ElementKind.Frost => new Color(0.40f, 0.87f, 1f),
            ElementKind.Lightning => new Color(1f, 0.85f, 0.37f),
            ElementKind.Earth => new Color(0.84f, 0.71f, 0.56f),
            ElementKind.Light => new Color(1f, 0.95f, 0.67f),
            ElementKind.Void => new Color(0.78f, 0.54f, 1f),
            _ => Color.white
        };

        public static void ApplyStacks(Combatant target, ElementKind element, float amount, Combatant source)
        {
            var status = ElementStatus.For(target);
            if (!status || !status.Rules || !target.IsAlive) return;
            status.Add(element, amount, source);
            if (element == ElementKind.Lightning) JumpLightning(target, amount, source, status.Rules);
        }

        /// <summary>A player weapon hit that cost HP or Shield. Document 6.2: Poison, Frost and Lightning add a fixed
        /// number of stacks per hit, however much damage it deals, so a fast weapon stacks faster; a Fire hit on an enemy sets it burning
        /// at StatusRules.fireBurnShare of the weapon's theoretical DPS per second (a built-in Fire also times
        /// builtInFireBurnFactor); a Fire hit that no player weapon dealt burns that share of baseDamage (the hit as dealt, before a crit
        /// and the target's own modifiers) instead. A hit from a continuous source (continuousSource set) adds them at most once per
        /// StatusRules.continuousStackInterval on each target. Earth, Light and Void still pay the wielder a share of the damage dealt.</summary>
        public static void ApplyHit(Combatant target, ElementKind element, float actualDamage, Combatant source,
            bool builtInElement = false, bool noLightningJump = false, object continuousSource = null, float baseDamage = -1f,
            RunWeapon weapon = null)
        {
            if (source && actualDamage > 0f) source.GetComponent<SkillLoadout>()?.HealFromDamage(actualDamage);
            var targetBoard = ElementStatus.For(target);
            var sourceBoard = ElementStatus.For(source);
            if (!targetBoard || !targetBoard.Rules || element == ElementKind.None) return;
            if (element is ElementKind.Earth or ElementKind.Light or ElementKind.Void)
            {
                // The damage already leaves out what the target's immunity blocked (Combatant.ReceiveDamage); it is not cut again.
                sourceBoard?.PayWielder(element, actualDamage);
                return;
            }
            if (!target.IsAlive) return;
            var rules = targetBoard.Rules;
            // A continuous hit that does not count adds no stacks and so sets off no Lightning jump either.
            if (continuousSource != null && !CountsContinuousHit(continuousSource, target, rules.continuousStackInterval)) return;
            if (element == ElementKind.Fire && !targetBoard.IsPlayer)
            {
                // One burn that each hit renews (no stacks). A weapon's burn follows its DPS (its Mk, attack speed and Damage cards
                // included), so it takes no R(w).
                var weapons = weapon != null && GameManager.Instance ? GameManager.Instance.runWeapons : null;
                float burn = weapons
                    ? rules.fireBurnShare * weapons.TheoreticalDps(weapon) * (!builtInElement ? 1f
                        : weapon.form == WeaponForm.Flamethrower && weapon.variant == 1 ? rules.wildfireFireBurnFactor : rules.builtInFireBurnFactor)
                    : rules.fireBurnShare * Mathf.Max(0f, baseDamage >= 0f ? baseDamage : actualDamage);
                targetBoard.Ignite(burn, source);
                return;
            }
            // The damage side of each stack is multiplied by R(w) in ElementStatus, so a stack is worth the same on every wave.
            float stacks = rules.stacksPerHit * (builtInElement ? rules.builtInElementStackFactor : 1f);
            targetBoard.Add(element, stacks, source);
            if (element == ElementKind.Lightning && !noLightningJump) JumpLightning(target, stacks, source, rules);
        }

        /// <summary>Stacks a continuous weapon source adds by itself rather than through its hits (the Blizzard spray's own Frost
        /// dose): like a continuous hit, at most once per StatusRules.continuousStackInterval on each target. No Lightning jump.</summary>
        public static void ApplyContinuousStacks(object continuousSource, Combatant target, ElementKind element, float amount, Combatant source)
        {
            var status = ElementStatus.For(target);
            if (!status || !status.Rules || !target.IsAlive || amount <= 0f) return;
            if (!CountsContinuousHit(continuousSource, target, status.Rules.continuousStackInterval)) return;
            status.Add(element, amount, source);
        }

        /// <summary>True when this continuous source's hit on target takes its turn to add stacks now. The next turn comes one interval
        /// after the one taken, or one interval from now when the source comes back after a pause.</summary>
        static bool CountsContinuousHit(object continuousSource, Combatant target, float interval)
        {
            if (interval <= 0f) return true;
            var key = (continuousSource, target);
            float early = interval * ContinuousEarlyShare;
            bool known = continuousNext.TryGetValue(key, out var gate) && gate.spawn == target.SpawnVersion;
            if (known && Clock < gate.next - early) return false;
            if (continuousNext.Count >= ContinuousPruneAt) PruneContinuousHits();
            float next = known ? Mathf.Max(gate.next + interval, Clock + interval - early) : Clock + interval;
            continuousNext[key] = (next, target.SpawnVersion);
            return true;
        }

        static void PruneContinuousHits()
        {
            expired.Clear();
            foreach (var pair in continuousNext)
                if (Clock >= pair.Value.next || !pair.Key.target || pair.Key.target.SpawnVersion != pair.Value.spawn) expired.Add(pair.Key);
            foreach (var key in expired) continuousNext.Remove(key);
            expired.Clear();
        }

        public static float DirectDamageFactor(Combatant target, ElementKind element)
        {
            var status = ElementStatus.For(target);
            return element == ElementKind.Light && status && status.CarriesVoid ? status.Rules.lightAgainstVoid : 1f;
        }

        public static void ApplyAffix(Combatant attacker, Combatant target, ElementKind element, float actualDamage = 0f)
        {
            var receiver = ElementStatus.For(target);
            if (!receiver || !receiver.Rules) return;
            var rules = receiver.Rules;
            switch (element)
            {
                // An enemy's Fire hit on another enemy (a Stomper's cone, a Bloater's blast) ignites it as a Fire weapon hit would.
                case ElementKind.Fire when !receiver.IsPlayer: receiver.Ignite(rules.fireBurnShare * Mathf.Max(0f, actualDamage), attacker); break;
                case ElementKind.Fire: receiver.Add(element, rules.enemyFireStacks, attacker); break;
                case ElementKind.Poison: receiver.Add(element, rules.enemyPoisonStacks, attacker); break;
                case ElementKind.Frost: receiver.Add(element, rules.enemyFrostStacks, attacker); break;
                case ElementKind.Lightning: receiver.Add(element, rules.enemyLightningStacks, attacker); break;
                case ElementKind.Light: if (attacker && actualDamage > 0f) attacker.Heal(actualDamage * Mathf.Max(0f, rules.lightAffixLifesteal)); break;
                case ElementKind.Void: target.GetComponent<RunProgression>()?.GainEnergy(-rules.enemyVoidDrain); break;
            }
        }

        /// <summary>A body fully immune to Lightning does not pass it on: the chain stops at it.</summary>
        static bool Conducts(Combatant body)
        {
            var status = ElementStatus.For(body);
            return !status || status.ElementFactor(ElementKind.Lightning) > 0f;
        }

        static void JumpLightning(Combatant original, float stacks, Combatant source, StatusRules rules)
        {
            if (!Conducts(original)) return;
            visited.Clear(); queue.Clear(); visited.Add(original); queue.Add(original);
            for (int cursor = 0; cursor < queue.Count && visited.Count < rules.lightningJumpMax + 1; cursor++)
            {
                var originStatus = ElementStatus.For(queue[cursor]);
                float radius = originStatus && originStatus.CarriesFrost ? Mathf.Max(rules.lightningJumpRadius, rules.frostLightningJumpRadius) : rules.lightningJumpRadius;
                int found = Physics.OverlapSphereNonAlloc(Query.AtCastHeight(queue[cursor].Position),
                    radius, buffer, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < found && visited.Count < rules.lightningJumpMax + 1; i++)
                {
                    var c = Combatant.From(buffer[i]);
                    if (!c || !c.IsAlive || c.Team != original.Team || visited.Contains(c)) continue;
                    var status = ElementStatus.For(c);
                    if (!status) continue;
                    // Multiplicative chain: the n-th jump keeps falloff^n of the damage and stacks.
                    float keep = Mathf.Pow(Mathf.Clamp01(rules.lightningChainFalloff), visited.Count - 1);
                    visited.Add(c);
                    if (Conducts(c)) queue.Add(c);
                    // The jump's damage carries no element tag (that would stack Lightning twice), so a Lightning-immune body's share is
                    // applied here: none, or the Damage Penetration share (document 9.5). Its stacks are refused by Add.
                    float share = status.DamageFactor(ElementKind.Lightning, source);
                    if (share > 0f) c.ReceiveDamage(DamageInfo.Attack(rules.lightningJumpDamage * WaveScale * keep * share, source, kind: HitKind.Bullet));
                    status.Add(ElementKind.Lightning, stacks * keep, source);
                }
            }
        }
    }
}
