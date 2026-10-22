using System;
using UnityEngine;

namespace AnomalyArena
{

    public static class SkillWorldAPI
    {
        public static Func<Vector3, Vector3, Vector3?> ValidPoint;
        public static Func<Vector3, bool> IsHole;
        public static Func<Vector3, Vector3> GetPull;
        public static Func<Combatant, bool> HoleProtection;
        public static Func<Combatant, bool> IsElite;

        public static Action<Vector3, float, float, float, Combatant> CreateWall;
        public static Action<Vector3, float, float, Combatant> CreateTemporaryHole;

        public static Action<Vector3, float, float, float, Combatant, bool> CreateVoidHole;
        public static Action<Vector3, float, int> FeedNearestHole;
        public static Func<Vector3, float, Vector3?> ClosestHole;
        public static Action<Combatant, Combatant> Swallow;
        public static Action<Vector3, float, bool, Combatant> CreateDecoy;

        public static Action<Vector3> DecoyEnded;
        public static Action<float, float> TimeSlip;
        public static Action<Vector3, float, Combatant> ReflectProjectiles;
        public static Action<Combatant, Vector3> Teleport;

        public static Action<Combatant, Vector3, float> Pull;
        public static Func<Vector3, bool> IsOnScreen;
    }
}
