using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>WorldMap and GameManager bind these to saved scene objects and their run state.</summary>
    public static class SkillWorldAPI
    {
        public static Func<Vector3, Vector3, Vector3?> ValidPoint;
        public static Func<Vector3, bool> IsHole;
        public static Func<Vector3, Vector3> GetPull;
        public static Func<Combatant, bool> HoleProtection;
        public static Func<Combatant, bool> IsElite;
        /// <summary>Position, width, lifetime, knockback applied to enemies that touch the wall, and caster.</summary>
        public static Action<Vector3, float, float, float, Combatant> CreateWall;
        public static Action<Vector3, float, float, Combatant> CreateTemporaryHole;
        /// <summary>Position, swallowing diameter, gravity ring width, lifetime, caster, and protection from this hole for its caster.</summary>
        public static Action<Vector3, float, float, float, Combatant, bool> CreateVoidHole;
        public static Action<Vector3, float, int> FeedNearestHole;
        public static Func<Vector3, float, Vector3?> ClosestHole;
        public static Action<Combatant, Combatant> Swallow;
        public static Action<Vector3, float, bool, Combatant> CreateDecoy;
        /// <summary>WorldMap invokes this with the decoy's position when it expires; SkillLoadout subscribes for the Mirror Image max-level echo.</summary>
        public static Action<Vector3> DecoyEnded;
        /// <summary>Skill and caster: whether that caster's Bulwark wall still stands, its mirror image is still out or its Pocket Void hole
        /// is still open where the player is now (WorldMap). A skill cannot be cast again while its own effect lasts.</summary>
        public static Func<SkillId, Combatant, bool> SkillObjectStanding;
        public static Action<float, float> TimeSlip;
        public static Action<Vector3, float, Combatant> ReflectProjectiles;
        public static Action<Combatant, Vector3> Teleport;
        /// <summary>Actor, world point to pull toward, and distance for this tick.</summary>
        public static Action<Combatant, Vector3, float> Pull;
        public static Func<Vector3, bool> IsOnScreen;
    }
}
