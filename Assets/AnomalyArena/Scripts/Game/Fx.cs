using UnityEngine;
using Random = UnityEngine.Random;

namespace AnomalyArena
{

    public static class Fx
    {

        public static void Pop(Vector3 pos, Color c, float size)
        {
            if (FxPool.Active) FxPool.Active.Emit(pos, c, size, .25f);
        }

        public static void Burst(Vector3 pos, Vector3 dir, Color c, int count, float speed, float spreadDeg, float size)
        {
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
            if (FxPool.Active) FxPool.Active.Emit(pos, c, radius * 2, life, texture: FxPool.Active.ring, flat: true);
        }

        public static void Sector(Vector3 origin, Vector3 dir, float radius, float arcDeg, Color c)
        {
            if (FxPool.Active) FxPool.Active.Emit(origin, c, radius * 2, .15f, texture: FxPool.Active.sector, forward: dir, flat: true);
        }
    }
}
