using System;
using System.Collections.Generic;

namespace AnomalyArena
{
    /// <summary>
    /// Document 7.6 and 9.5: element immunity and Damage Penetration. A body immune to an element takes none of that element's damage
    /// (a hit that carries it: a weapon's built-in or infused element, a skill of that element, an enemy affix; and its damage over time)
    /// and never gets its statuses. The attacker's Damage Penetration lets that share of the damage through, but never a status. Each
    /// body's own immunities live on its ElementStatus.
    /// </summary>
    public static class ElementImmunity
    {
        static readonly ElementKind[] boss = { ElementKind.Frost, ElementKind.Lightning };

        /// <summary>Elements both bosses always ignore (documents 7.6, 13.1 and 13.2): no damage, no freeze, no paralysis.</summary>
        public static IReadOnlyList<ElementKind> Boss => boss;

        public static bool BossImmuneTo(ElementKind element) => Array.IndexOf(boss, element) >= 0;

        /// <summary>Share of an element's damage target takes from source; all of it without a status board or an immunity.</summary>
        public static float DamageShare(Combatant target, ElementKind element, Combatant source)
        {
            var status = ElementStatus.For(target);
            return status ? status.DamageFactor(element, source) : 1f;
        }

        /// <summary>Skills whose own hits deal damage of their element, so Damage Penetration helps them. The other element skills only
        /// add stacks or help the player, which an immunity blocks whatever the penetration.</summary>
        public static bool SkillHitsWithElement(SkillId id) => id is SkillId.StormCall or SkillId.Singularity;
    }
}
