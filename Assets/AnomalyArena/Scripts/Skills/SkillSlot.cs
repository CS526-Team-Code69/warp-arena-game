using System;

namespace AnomalyArena
{
    [Serializable]
    public sealed class SkillSlot
    {
        public SkillDefinition Definition;
        public int Level = 1;
        public float CooldownLeft;
        public SkillId Id => Definition ? Definition.id : default;
        public bool Empty => !Definition;
        // The V2 day campaign's room factors (SkillLoadout.CostScale and CooldownScale) apply here, so the cast and the HUD agree.
        public float Cost => Definition ? Definition.cost * UnityEngine.Mathf.Max(0f, SkillLoadout.CostScale?.Invoke() ?? 1f) : 0f;
        public float Cooldown => Definition ? Definition.Cooldown(Level) * UnityEngine.Mathf.Max(0f, SkillLoadout.CooldownScale?.Invoke() ?? 1f) : 0f;
        public string Name => Definition ? ArenaTerms.Skill(Id) : ArenaText.Pair("空槽", "Empty");
    }
}
