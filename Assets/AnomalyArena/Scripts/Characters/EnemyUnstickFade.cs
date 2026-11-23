using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// A monster set down on open ground (Enemy.SetDownOnOpenGround) does not jump in sight: it shrinks away where it stood, in a little
    /// void smoke while the art is on, is moved while out of sight, and grows back where it is set down, holding still meanwhile. Each half
    /// takes HalfSeconds of the world's time. The body's children are scaled (its model or whitebox look and what hangs on them), never the
    /// body itself, so its collider stays as it is; their scales are put back when the fade ends or the body leaves play. A body that dies
    /// during the fade is not moved.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    [DisallowMultipleComponent]
    public sealed class EnemyUnstickFade : MonoBehaviour
    {
        const float HalfSeconds = .3f, LeastScale = .02f;

        Enemy body;
        Rigidbody rb;
        Transform[] parts;
        Vector3[] scales;
        Vector3 ground;
        float clock;
        bool moved;

        /// <summary>A fade runs on this body now.</summary>
        public bool Active { get; private set; }

        /// <summary>Starts the fade of a body toward a point on open ground (again from the start if one runs).</summary>
        public static EnemyUnstickFade Begin(Enemy enemy, Vector3 to)
        {
            var fade = enemy.GetComponent<EnemyUnstickFade>();
            if (!fade) fade = enemy.gameObject.AddComponent<EnemyUnstickFade>();
            fade.Run(enemy, to);
            return fade;
        }

        void Run(Enemy enemy, Vector3 to)
        {
            if (Active) Restore();
            body = enemy;
            rb = enemy.GetComponent<Rigidbody>();
            int count = transform.childCount;
            parts = new Transform[count];
            scales = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                parts[i] = transform.GetChild(i);
                scales[i] = parts[i].localScale;
            }
            ground = to;
            clock = 0f;
            moved = false;
            Active = true;
        }

        void FixedUpdate()
        {
            // It holds still while it fades: this runs after the monster's own walk has set its velocity.
            if (Active && rb && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        void LateUpdate()
        {
            if (!Active) return;
            if (!body || !body.IsAlive)
            {
                Restore();
                return;
            }
            var gm = GameManager.Instance;
            clock += gm ? gm.WorldDeltaTime : Time.deltaTime;
            float shown;
            if (clock < HalfSeconds) shown = 1f - Smooth(clock / HalfSeconds);
            else
            {
                if (!moved)
                {
                    body.MoveForSetDown(ground);
                    moved = true;
                }
                shown = Smooth(Mathf.Clamp01((clock - HalfSeconds) / HalfSeconds));
            }
            float scale = Mathf.Max(LeastScale, shown);
            for (int i = 0; i < parts.Length; i++)
                if (parts[i]) parts[i].localScale = scales[i] * scale;
            if (V5Vfx.On) V5Vfx.I.VoidSmoke(body.Position, clock < HalfSeconds ? 1.2f : .8f);
            if (clock >= 2f * HalfSeconds) Restore();
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>The children's own scales back, and no fade running.</summary>
        void Restore()
        {
            if (parts != null)
                for (int i = 0; i < parts.Length; i++)
                    if (parts[i]) parts[i].localScale = scales[i];
            parts = null;
            scales = null;
            Active = false;
        }

        void OnDisable() => Restore();
    }
}
