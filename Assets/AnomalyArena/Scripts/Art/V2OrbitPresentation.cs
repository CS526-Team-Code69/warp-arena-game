using UnityEngine;

namespace AnomalyArena
{
    public class V2OrbitPresentation : MonoBehaviour
    {
        public RunWeapons weapons;
        [Tooltip("Two blades per auto slot, in slot 0/1/2 order.")]
        public SpriteRenderer[] blades = new SpriteRenderer[6];

        void LateUpdate()
        {
            if (!weapons) return;
            for (int i = 0; i < blades.Length; i++)
            {
                var renderer = blades[i]; if (!renderer) continue;
                bool active = weapons.HasOrbitBlades(i / 2);
                renderer.enabled = active;
                if (!active) continue;
                Vector3 position = weapons.GetOrbitPosition(i / 2, i % 2);
                renderer.transform.position = position + Vector3.up * .15f;
                Vector3 tangent = Vector3.Cross(Vector3.up, position - weapons.transform.position);
                if (tangent.sqrMagnitude > .001f)
                    renderer.transform.rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(90f, 0f, 0f);
                renderer.color = Art.Enabled ? Color.white : new Color(.7f, .9f, 1f);
            }
        }
    }
}
