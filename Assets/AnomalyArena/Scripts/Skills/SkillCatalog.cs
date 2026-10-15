using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Skill Catalog")]
    public sealed class SkillCatalog : ScriptableObject
    {
        public SkillDefinition[] skills;
        public SkillDefinition Find(SkillId id)
        {
            if (skills == null) return null;
            foreach (var skill in skills) if (skill && skill.id == id) return skill;
            return null;
        }
    }
}
