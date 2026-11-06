using UnityEngine;
using Random = UnityEngine.Random;

namespace AnomalyArena
{
                                                                                                                                                            
    public static class Fx
    {
        const float NativeHalfAngle = 24f;

                                                         
        public static void Pop(Vector3 pos, Color c, float size)
        {
            if (V5Vfx.On) { V5Vfx.I.Pop(pos, c, size); return; }
            if (FxPool.Active) FxPool.Active.Emit(pos, c, size, .25f);
        }

                                                                                                                                                                
        public static void Burst(Vector3 pos, Vector3 dir, Color c, int count, float speed, float spreadDeg, float size)
        {
            if (V5Vfx.On) { V5Vfx.I.Burst(pos, dir, c, count, speed, spreadDeg, size); return; }
            var pool = FxPool.Active;
            if (!pool) return;
            Vector3 forward = dir.sqrMagnitude > 0f ? dir.normalized : Vector3.forward;
            for (int i = 0; i < Mathf.Min(count, 8); i++)
            {
                Vector3 d = Quaternion.Euler(0, Random.Range(-spreadDeg, spreadDeg) * .5f, 0) * forward;
                pool.Emit(pos, c, size * 2, .3f, d * speed);
            }
        }

                                                                                  
        public static void Ring(Vector3 pos, float radius, Color c, float life = 0.25f)
        {
            if (V5Vfx.On) { V5Vfx.I.Ring(pos, radius, c, life); return; }
            if (FxPool.Active) FxPool.Active.Emit(pos, c, radius * 2, life, texture: FxPool.Active.ring, flat: true);
        }

                                                                                                                    
        public static void Sector(Vector3 origin, Vector3 dir, float radius, float arcDeg, Color c)
            => Flat(origin, dir, radius, arcDeg, c, V5Vfx.On ? V5Vfx.I.TeleCone : FxPool.Active ? FxPool.Active.conePlain : null, .15f);

                                                                                                                                       
                                                                                                  
        public static void Cone(Vector3 origin, Vector3 dir, float radius, float arcDeg, ElementKind element)
        {
            if (V5Vfx.On) { V5Vfx.I.Cone(origin, dir, radius, arcDeg, element); return; }
            var pool = FxPool.Active;
            if (!pool) return;
            var texture = pool.ConeTexture(element);
            bool coloured = texture && texture != pool.conePlain;
            Flat(origin, dir, radius, arcDeg, coloured ? new Color(1f, 1f, 1f, .92f) : ElementCombat.ColorFor(element),
                texture,
                element == ElementKind.None ? .2f : .3f);
            if (element != ElementKind.Fire || pool.flamePuffs == null || pool.flamePuffs.Length == 0) return;
            Vector3 d = dir.sqrMagnitude > 0f ? dir.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, d);
            float halfTan = Mathf.Tan(Mathf.Clamp(arcDeg, 10f, 170f) * .5f * Mathf.Deg2Rad);
            for (int i = 0; i < 3; i++)
            {
                float along = Random.Range(.25f, .85f) * radius;
                Vector3 at = origin + d * along + side * (Random.Range(-1f, 1f) * along * halfTan * .8f) +
                             Vector3.up * .6f;
                var puff = pool.flamePuffs[Random.Range(0, pool.flamePuffs.Length)];
                if (!puff) continue;
                pool.Emit(at, new Color(1f, .85f, .6f, .9f), radius * Random.Range(.12f, .2f), .3f,
                    d * 2.5f + Vector3.up * .8f,
                    texture: puff, roll: Random.Range(-25f, 25f));
            }
        }

                                                                                                                
        public static void Telegraph(Vector3 origin, Vector3 dir, float radius, float arcDeg, Color c)
            => Flat(origin, dir, radius, arcDeg, c, V5Vfx.On ? V5Vfx.I.TeleCone : FxPool.Active ? FxPool.Active.telegraph : null, .18f);

                                                                                                                                        
                                                                           
        static void Flat(Vector3 origin, Vector3 dir, float radius, float arcDeg, Color c, Texture texture, float life)
        {
            var pool = FxPool.Active;
            if (!pool) return;
            Vector3 d = dir.sqrMagnitude > 0f ? dir.normalized : Vector3.forward;
            if (!texture)
            {
                pool.Emit(origin, c, radius * 2f, life, texture: pool.sector, forward: d, flat: true);
                return;
            }

            float width =
                Mathf.Clamp(
                    Mathf.Tan(Mathf.Clamp(arcDeg, 10f, 170f) * .5f * Mathf.Deg2Rad) /
                    Mathf.Tan(NativeHalfAngle * Mathf.Deg2Rad), .25f, 4.5f);
            pool.Emit(origin + d * (radius * .5f), c, radius, life, texture: texture, forward: d, flat: true,
                width: width);
        }
    }
}