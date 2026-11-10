using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The ship tab's live picture (D1-C, chosen 2026-11-04: the base stays a 2D panel and the ship bay shows the 3D ship itself). A camera
    /// of its own swings slowly to and fro north of the ship pad, so the base never stands in front of the ship, and draws into a render
    /// texture the base panel shows. It renders only while the ship tab is open, with a soft light of its own, so neither the night nor the
    /// darkness overlay (drawn on the HUD, not in this camera) hides what has been repaired: the engine, the armour and the fuel show on
    /// the ship the moment they are bought. With the whitebox it shows the whitebox ship.
    /// </summary>
    public sealed class V2ShipView : MonoBehaviour
    {
        const int Width = 960, Height = 540;      // the panel's picture is 560 x 315 (16:9)
        const float Distance = 17f, Height3D = 7.5f, Swing = 70f, SwingSpeed = .22f;
        const int UiLayer = 5;

        Camera view;
        Light fill;
        RenderTexture target;
        Vector3 centre;
        float clock;

        public Texture Texture => target;

        public static V2ShipView Create(Transform parent, Vector3 centre)
        {
            var go = new GameObject("Ship live view");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<V2ShipView>();
            v.centre = centre;
            v.target = new RenderTexture(Width, Height, 24) { name = "Ship live view", antiAliasing = 2 };
            v.view = go.AddComponent<Camera>();
            v.view.targetTexture = v.target;
            v.view.fieldOfView = 32f;
            v.view.nearClipPlane = .3f;
            v.view.farClipPlane = 140f;
            v.view.clearFlags = CameraClearFlags.SolidColor;
            v.view.backgroundColor = new Color(.05f, .06f, .09f);
            v.view.cullingMask = ~(1 << UiLayer);
            v.view.enabled = false;
            var lightGo = new GameObject("Ship view light");
            lightGo.transform.SetParent(go.transform, false);
            v.fill = lightGo.AddComponent<Light>();
            v.fill.type = LightType.Point;
            v.fill.range = 26f;
            v.fill.intensity = 1.6f;
            v.fill.color = new Color(.85f, .92f, 1f);
            v.fill.shadows = LightShadows.None;
            v.fill.enabled = false;
            v.Place();
            return v;
        }

        /// <summary>On while the ship tab is open: the camera and its light work only then.</summary>
        public bool Live
        {
            get => view && view.enabled;
            set
            {
                if (!view || view.enabled == value) return;
                view.enabled = value;
                fill.enabled = value;
                if (value) Place();
            }
        }

        void LateUpdate()
        {
            if (!view || !view.enabled) return;
            clock += Time.unscaledDeltaTime;   // the base panel may pause the game; the view keeps moving
            Place();
        }

        void Place()
        {
            // North of the ship (+Z), swinging between north-west and north-east: the base (south of the ship) stays behind it.
            float yaw = Mathf.Sin(clock * SwingSpeed) * Swing;
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            view.transform.position = centre + dir * Distance + Vector3.up * Height3D;
            view.transform.LookAt(centre + Vector3.up * 1.2f);
            fill.transform.position = centre + dir * 6f + Vector3.up * 9f;
        }

        void OnDestroy()
        {
            if (!target) return;
            target.Release();
            Destroy(target);
        }
    }
}
