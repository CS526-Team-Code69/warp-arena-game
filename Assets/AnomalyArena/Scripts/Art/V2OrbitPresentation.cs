using UnityEngine;

namespace AnomalyArena
{

    public class V2OrbitPresentation : MonoBehaviour
    {
        public RunWeapons weapons;
        [Tooltip("Two blades per auto slot, in slot 0/1/2 order.")]
        public SpriteRenderer[] blades = new SpriteRenderer[6];
        Sprite savedBlade;

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
                if (V5Vfx.On)
                {

                    var blade = V5Vfx.I.Sprite("blade");
                    if (blade && renderer.sprite != blade)
                    {
                        savedBlade ??= renderer.sprite;
                        renderer.sprite = blade;
                    }
                    if (blade)
                    {
                        var parent = renderer.transform.parent ? renderer.transform.parent.lossyScale : Vector3.one;
                        renderer.transform.localScale = new Vector3(1.9f / Mathf.Max(.001f, blade.bounds.size.x * Mathf.Abs(parent.x)), .55f / Mathf.Max(.001f, blade.bounds.size.y * Mathf.Abs(parent.z)), 1f);
                    }
                    var w = weapons.Auto[i / 2];
                    V5Vfx.I.OrbitTrail(position, tangent, w != null && w.variant == 1, w != null ? w.element : ElementKind.None);
                }
                else if (savedBlade && renderer.sprite != savedBlade) renderer.sprite = savedBlade;
            }
        }
    }
}
