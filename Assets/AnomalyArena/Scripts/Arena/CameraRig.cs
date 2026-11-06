using UnityEngine;

namespace AnomalyArena
{

    public sealed class CameraRig : MonoBehaviour
    {

        [System.Serializable]
        public struct View
        {
            public string name;
            [Range(20f, 90f), Tooltip("Degrees below the horizon the camera looks: 90 is straight down; the arena scenes save 74.")]
            public float pitch;
            [Range(-180f, 180f), Tooltip("Degrees the camera turns round the vertical: 0 keeps map north at the top of the screen, 45 shows the map corner-on. WASD turn with it.")]
            public float yaw;
            [Min(2f), Tooltip("Metres from the camera to the point it looks at. An orthographic view does not grow or shrink with it; keep the far edge of the view inside the shadow distance (50 m).")]
            public float distance;
            [Tooltip("Height above the player's feet, in metres, that the view centres on.")]
            public float lookHeight;
            public bool orthographic;
            [Min(1f), Tooltip("Orthographic only: half the screen's height in metres. The arena scenes save 12.")]
            public float orthographicSize;
            [Range(5f, 90f), Tooltip("Perspective only: the vertical field of view in degrees.")]
            public float fieldOfView;
        }

        public Transform target;
        public Camera camera;
        public Vector3 offset = new Vector3(0f, 46f, -15f);

        public Vector3 Transient { get; private set; }
        public float smoothTime = 0.12f;
        [Min(0f), Tooltip("How fast a camera push springs back (per second).")] public float kickReturn = 14f;
        [Min(0f), Tooltip("Largest push, so stacked kicks never throw the view.")] public float kickLimit = .6f;

        [Header("Camera view (2026-11-06)")]

        [Min(0), Tooltip("0 keeps the camera exactly as the scene saved it (the offset above, the camera's own rotation and lens). 1 and up pick from Views. " +
                         "The default, 2, is the corner-on view chosen on 2026-11-06. The view is fixed in play: no key switches it.")]
        public int cameraView = 2;
        [Tooltip("The views Camera View picks from (1 and up). Changes here show at once while one of them is on.")]
        public View[] views =
        {
            new View { name = "Tilt 55, orthographic", pitch = 55f, yaw = 0f, distance = 30f, lookHeight = 1f, orthographic = true, orthographicSize = 12f, fieldOfView = 40f },
            new View { name = "Corner 45, pitch 50, orthographic", pitch = 50f, yaw = 45f, distance = 30f, lookHeight = 1f, orthographic = true, orthographicSize = 10f, fieldOfView = 40f },
            new View { name = "Perspective, pitch 50", pitch = 50f, yaw = 0f, distance = 31f, lookHeight = 1f, orthographic = false, orthographicSize = 12f, fieldOfView = 35f },
        };

        Vector3 follow, velocity, kick;
        float amplitude, punch;
        bool started;

        Vector3 focusPoint;
        float focusWeight, focusTarget, focusZoom = 1f, focusSeconds = 1f;

        int applied;
        Vector3 presetOffset;
        float moveYaw;
        bool sceneSaved, sceneOrthographic;
        Quaternion sceneRotation;
        float sceneSize, sceneFieldOfView;

        public float MoveYaw => moveYaw;

        Vector3 BaseOffset => applied == 0 ? offset : presetOffset;

        public void Focus(Vector3 point, float zoom, float seconds)
        {
            focusPoint = point; focusZoom = Mathf.Clamp(zoom, .2f, 1f); focusSeconds = Mathf.Max(.05f, seconds); focusTarget = 1f;
        }

        public void ClearFocus(float seconds)
        {
            focusSeconds = Mathf.Max(.05f, seconds); focusTarget = 0f;
        }

        public bool Focused => focusTarget > 0f;

        public void Shake(float amount) => amplitude = Mathf.Max(amplitude, amount);

        public void Punch(float amount) => punch = Mathf.Max(punch, Mathf.Min(amount, .05f));

        public void Kick(Vector3 worldDirection, float amount)
        {
            Vector3 d = new Vector3(worldDirection.x, 0f, worldDirection.z);
            if (d.sqrMagnitude < 1e-4f || amount <= 0f) return;
            Vector3 k = d.normalized * amount;
            kick = k.magnitude > kick.magnitude ? k : kick + k * .3f;
            kick = Vector3.ClampMagnitude(kick, kickLimit);
        }
        public Vector2 ScreenEdge(Vector3 world, float margin = 32f)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            Vector2 delta = new Vector2(screen.x - Screen.width * 0.5f, screen.y - Screen.height * 0.5f);
            if (screen.z < 0) delta = -delta;
            float ratio = Mathf.Max(Mathf.Abs(delta.x) / (Screen.width * 0.5f - margin), Mathf.Abs(delta.y) / (Screen.height * 0.5f - margin));
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + delta / Mathf.Max(1f, ratio);
        }

        void ApplyView()
        {
            int count = views != null ? views.Length : 0;
            if (cameraView < 0 || cameraView > count) cameraView = 0;
            if (cameraView == 0 && applied == 0) return;
            if (!sceneSaved)
            {
                sceneRotation = transform.rotation; sceneOrthographic = camera.orthographic;
                sceneSize = camera.orthographicSize; sceneFieldOfView = camera.fieldOfView; sceneSaved = true;
            }
            applied = cameraView;
            if (cameraView == 0)
            {
                transform.rotation = sceneRotation; camera.orthographic = sceneOrthographic;
                camera.orthographicSize = sceneSize; camera.fieldOfView = sceneFieldOfView; moveYaw = 0f;
                return;
            }
            var chosen = views[cameraView - 1];
            var rotation = Quaternion.Euler(chosen.pitch, chosen.yaw, 0f);
            transform.rotation = rotation;
            camera.orthographic = chosen.orthographic;
            if (chosen.orthographic) camera.orthographicSize = chosen.orthographicSize;
            else camera.fieldOfView = chosen.fieldOfView;
            presetOffset = Vector3.up * chosen.lookHeight - rotation * Vector3.forward * chosen.distance;
            moveYaw = chosen.yaw;
        }

        void LateUpdate()
        {
            if (!target || !camera) return;

            if (cameraView != 0 || applied != 0) ApplyView();
            Vector3 baseOffset = BaseOffset;
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
                    min = Vector2.Min(min, new Vector2(point.x + baseOffset.x, point.z + baseOffset.z));
                    max = Vector2.Max(max, new Vector2(point.x + baseOffset.x, point.z + baseOffset.z));
                }
                want.x = Mathf.Clamp(want.x, bounds.min.x - min.x, bounds.max.x - max.x);
                want.z = Mathf.Clamp(want.z, bounds.min.z - min.y, bounds.max.z - max.y);
            }
            focusWeight = Mathf.MoveTowards(focusWeight, focusTarget, Time.unscaledDeltaTime / focusSeconds);
            float eased = focusWeight * focusWeight * (3f - 2f * focusWeight);
            if (eased > 0f) want = Vector3.Lerp(want, focusPoint, eased);
            if (!started) { follow = want; started = true; }
            follow = Vector3.SmoothDamp(follow, want, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            amplitude = Mathf.MoveTowards(amplitude, 0f, 4f * Time.unscaledDeltaTime);
            Vector2 shake = amplitude > 0f ? Random.insideUnitCircle * amplitude : Vector2.zero;
            kick = Vector3.Lerp(kick, Vector3.zero, 1f - Mathf.Exp(-kickReturn * Time.unscaledDeltaTime));
            punch = Mathf.Lerp(punch, 0f, 1f - Mathf.Exp(-kickReturn * Time.unscaledDeltaTime));
            Vector3 viewOffset = baseOffset * (Mathf.Lerp(1f, focusZoom, eased) * (1f - punch));
            transform.position = follow + viewOffset + new Vector3(shake.x, 0f, shake.y) + kick;
            Transient = transform.position - (follow + baseOffset * Mathf.Lerp(1f, focusZoom, eased));
        }
    }
}
