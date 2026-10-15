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
        public float Cost => Definition ? Definition.cost : 0f;
        public float Cooldown => Definition ? Definition.Cooldown(Level) : 0f;
        public string Name => Definition ? ArenaTerms.Skill(Id) : ArenaText.Pair("Empty","Empty");
    }
}
