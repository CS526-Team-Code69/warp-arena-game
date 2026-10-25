using System;
using System.Collections.Generic;

namespace AnomalyArena
{

    public static class ElementImmunity
    {
        static readonly ElementKind[] boss = { ElementKind.Frost, ElementKind.Lightning };

        public static IReadOnlyList<ElementKind> Boss => boss;

        public static bool BossImmuneTo(ElementKind element) => Array.IndexOf(boss, element) >= 0;

        public static float DamageShare(Combatant target, ElementKind element, Combatant source)
        {
            var status = ElementStatus.For(target);
            return status ? status.DamageFactor(element, source) : 1f;
        }

        public static bool SkillHitsWithElement(SkillId id) => id is SkillId.StormCall or SkillId.Singularity;
    }
}
