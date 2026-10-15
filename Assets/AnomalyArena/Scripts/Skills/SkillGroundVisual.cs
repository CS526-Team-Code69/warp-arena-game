using UnityEngine;

namespace AnomalyArena
{
    public readonly struct SkillGroundVisual
    {
        public readonly SkillId Id;
        public readonly Vector3 Position;
        public readonly float Radius, Remaining, Age;

        public SkillGroundVisual(SkillId id, Vector3 position, float radius, float remaining, float age)
        { Id = id; Position = position; Radius = radius; Remaining = remaining; Age = age; }

        public SkillGroundVisual At(Vector3 position) => new(Id, position, Radius, Remaining, Age);
    }
}
