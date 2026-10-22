using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    public static class ElementCombat
    {
        static readonly Collider[] buffer = new Collider[256];
        static readonly HashSet<Combatant> visited = new();
        static readonly List<Combatant> queue = new();

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

        public static void ApplyHit(Combatant target, ElementKind element, float baseDamage, float actualDamage,
            Combatant source, bool builtInElement = false)
        {
            if (source && actualDamage > 0f) source.GetComponent<SkillLoadout>()?.HealFromDamage(actualDamage);
            var targetBoard = ElementStatus.For(target);
            var sourceBoard = ElementStatus.For(source);
            if (!targetBoard || !targetBoard.Rules || element == ElementKind.None) return;
            if (element is ElementKind.Earth or ElementKind.Light or ElementKind.Void)
            {
                sourceBoard?.PayWielder(element, actualDamage * targetBoard.ElementFactor(element, source));
                return;
            }
            if (!target.IsAlive) return;

            float stacks = baseDamage / (targetBoard.Rules.damagePerStack * (targetBoard.IsPlayer ? 1f : WaveScale))
                * (builtInElement ? targetBoard.Rules.builtInElementStackFactor : 1f);
            targetBoard.Add(element, stacks, source);
            if (element == ElementKind.Lightning) JumpLightning(target, stacks, source, targetBoard.Rules);
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
                case ElementKind.Fire: receiver.Add(element, rules.enemyFireStacks, attacker); break;
                case ElementKind.Poison: receiver.Add(element, rules.enemyPoisonStacks, attacker); break;
                case ElementKind.Frost: receiver.Add(element, rules.enemyFrostStacks, attacker); break;
                case ElementKind.Lightning: receiver.Add(element, rules.enemyLightningStacks, attacker); break;
                case ElementKind.Light: if (attacker && actualDamage > 0f) attacker.Heal(actualDamage * Mathf.Max(0f, rules.lightAffixLifesteal)); break;
                case ElementKind.Void: target.GetComponent<RunProgression>()?.GainEnergy(-rules.enemyVoidDrain); break;
            }
        }

        static void JumpLightning(Combatant original, float stacks, Combatant source, StatusRules rules)
        {
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

                    float keep = Mathf.Pow(Mathf.Clamp01(rules.lightningChainFalloff), visited.Count - 1);
                    visited.Add(c); queue.Add(c);
                    c.ReceiveDamage(DamageInfo.Attack(rules.lightningJumpDamage * WaveScale * keep, source, kind: HitKind.Bullet));
                    status.Add(ElementKind.Lightning, stacks * keep, source);
                }
            }
        }
    }
}
