using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
                                                                                                           
    public static class Query
    {
        public const float CastHeight = 0.8f;
        static readonly Collider[] buffer = new Collider[64];

        public static Vector3 AtCastHeight(Vector3 p) => new Vector3(p.x, CastHeight, p.z);

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

                                                                             
        public static List<Combatant> Characters(Vector3 center, float radius)
        {
            var list = new List<Combatant>();
            int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, GameManager.CharacterMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Combatant.From(buffer[i]);
                if (c != null && !list.Contains(c)) list.Add(c);
            }

            return list;
        }

                     
                                                                                                                          
                                                                                                                              
                      
        public static List<Combatant> CharactersAlong(Vector3 from, Vector3 to, float radius)
        {
            var list = new List<Combatant>();
            int n = Physics.OverlapCapsuleNonAlloc(from, to, radius, buffer, GameManager.CharacterMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Combatant.From(buffer[i]);
                if (c == null || list.Contains(c)) continue;
                                                                                                                                   
                float d = Flat(c.Position - from).sqrMagnitude;
                int at = list.Count;
                while (at > 0 && Flat(list[at - 1].Position - from).sqrMagnitude > d) at--;
                list.Insert(at, c);
            }

            return list;
        }

                     
                                                                                                                                                   
                      
        public static List<Combatant> InSector(Combatant self, Vector3 origin, Vector3 aim, float reach,
            float arcDeg)
        {
            var list = new List<Combatant>();
                                                                                                                          
            foreach (var c in Characters(AtCastHeight(origin), reach + 2f))
            {
                if (c == self || !c.IsAlive) continue;
                Vector3 to = Flat(c.Position - origin);
                float d = to.magnitude;
                if (d - c.Radius > reach) continue;
                if (d > c.Radius && Vector3.Angle(aim, to) > arcDeg * 0.5f) continue;
                list.Add(c);
            }

            return list;
        }

                                                                               
        public static bool WallBetween(Vector3 from, Vector3 to, out RaycastHit hit)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-5f)
            {
                hit = default;
                return false;
            }

            return Physics.Raycast(from, d / len, out hit, len, GameManager.WallMask, QueryTriggerInteraction.Ignore);
        }

                                                                                                
        public static Vector3 Steer(Vector3 dir, Vector3 want, float maxDegrees)
        {
            want = Flat(want);
            if (want.sqrMagnitude < 1e-6f) return dir;
            float angle = Vector3.SignedAngle(dir, want, Vector3.up);
            float step = Mathf.Clamp(angle, -maxDegrees, maxDegrees);
            return (Quaternion.AngleAxis(step, Vector3.up) * dir).normalized;
        }
    }
}