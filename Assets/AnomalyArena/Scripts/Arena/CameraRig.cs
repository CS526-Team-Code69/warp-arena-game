using UnityEngine;

namespace AnomalyArena
{
    public sealed class CameraRig : MonoBehaviour
    {
        public Transform target;
        public Camera camera;
        public Vector3 offset = new Vector3(0f, 46f, -15f);
        public float smoothTime = 0.12f;
        Vector3 follow, velocity;
        float amplitude;
        bool started;

        public void Shake(float amount) => amplitude = Mathf.Max(amplitude, amount);
        public Vector2 ScreenEdge(Vector3 world, float margin = 32f)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            Vector2 delta = new Vector2(screen.x - Screen.width * 0.5f, screen.y - Screen.height * 0.5f);
            if (screen.z < 0) delta = -delta;
            float ratio = Mathf.Max(Mathf.Abs(delta.x) / (Screen.width * 0.5f - margin), Mathf.Abs(delta.y) / (Screen.height * 0.5f - margin));
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + delta / Mathf.Max(1f, ratio);
        }

        void LateUpdate()
        {
            if (!target || !camera) return;
            Vector3 want = target.position;
            if (WorldMap.Active && !WorldMap.Active.TrialActive)
            {
                Bounds bounds = WorldMap.Active.Bounds;

                var plane = new Plane(Vector3.up, Vector3.zero);
                Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
                for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
                {
                    Ray ray = camera.ViewportPointToRay(new Vector3(x, y, 0));
                    if (!plane.Raycast(ray, out float t)) continue;
                    Vector3 point = ray.GetPoint(t) - transform.position;
                    min = Vector2.Min(min, new Vector2(point.x + offset.x, point.z + offset.z));
                    max = Vector2.Max(max, new Vector2(point.x + offset.x, point.z + offset.z));
                }
                want.x = Mathf.Clamp(want.x, bounds.min.x - min.x, bounds.max.x - max.x);
                want.z = Mathf.Clamp(want.z, bounds.min.z - min.y, bounds.max.z - max.y);
            }
            if (!started) { follow = want; started = true; }
            follow = Vector3.SmoothDamp(follow, want, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            amplitude = Mathf.MoveTowards(amplitude, 0f, 4f * Time.unscaledDeltaTime);
            Vector2 shake = amplitude > 0f ? Random.insideUnitCircle * amplitude : Vector2.zero;
            transform.position = follow + offset + new Vector3(shake.x, 0f, shake.y);
        }
    }
}
