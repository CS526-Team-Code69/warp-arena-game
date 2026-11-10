using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Six saved renderers follow the weapon runtime's real two-blade orbit positions. With the art off (2026-11-10) each blade is
    /// the whitebox table's thin solid blade ("orbit.blade") instead of the flat picture, edge along its path, in its element's colour.</summary>
    public class V2OrbitPresentation : MonoBehaviour
    {
        public RunWeapons weapons;
        [Tooltip("Two blades per auto slot, in slot 0/1/2 order.")]
        public SpriteRenderer[] blades = new SpriteRenderer[6];
        [Tooltip("Height of a whitebox blade's middle above the ground, cells.")]
        public float whiteboxBladeHeight = .5f;
        Sprite savedBlade;
        WhiteboxLook[] bladeLooks = new WhiteboxLook[0];
        ElementKind[] bladeElements = new ElementKind[0];

        void LateUpdate()
        {
            if (!weapons) return;
            for (int i = 0; i < blades.Length; i++)
            {
                var renderer = blades[i]; if (!renderer) continue;
                bool active = weapons.HasOrbitBlades(i / 2);
                bool solid = active && !Art.Enabled;
                renderer.enabled = active && !solid;
                Vector3 position = active ? weapons.GetOrbitPosition(i / 2, i % 2) : Vector3.zero;
                Vector3 tangent = active ? Vector3.Cross(Vector3.up, position - weapons.transform.position) : Vector3.forward;
                ShowSolidBlade(i, solid, position, tangent);
                if (!active || solid) continue;
                renderer.transform.position = position + Vector3.up * .15f;
                if (tangent.sqrMagnitude > .001f)
                    renderer.transform.rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(90f, 0f, 0f);
                renderer.color = Art.Enabled ? Color.white : new Color(.7f, .9f, 1f);
                if (V5Vfx.On)
                {
                    // The V5 art: a real blade, with a streak behind it that shows which way the pair works (closed pushes out, open pulls in).
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

        /// <summary>The whitebox blade i: built on first use, placed on its orbit with its length along the path.</summary>
        void ShowSolidBlade(int i, bool show, Vector3 position, Vector3 tangent)
        {
            if (bladeLooks.Length != blades.Length)
            {
                System.Array.Resize(ref bladeLooks, blades.Length);
                System.Array.Resize(ref bladeElements, blades.Length);
            }
            var look = bladeLooks[i];
            if (!show)
            {
                if (look && look.gameObject.activeSelf) look.gameObject.SetActive(false);
                return;
            }
            if (!look)
            {
                bladeLooks[i] = look = Whitebox.Build(V2WeaponPresentation.LookIdFor(WeaponForm.OrbitBlades), transform, "Whitebox blade " + i).GetComponent<WhiteboxLook>();
                bladeElements[i] = (ElementKind)(-1);
            }
            if (!look.gameObject.activeSelf) look.gameObject.SetActive(true);
            var weapon = weapons.Auto[i / 2];
            var element = weapon != null ? weapon.element : ElementKind.None;
            if (bladeElements[i] != element)
            {
                bladeElements[i] = element;
                if (element == ElementKind.None) look.ResetColours();
                else for (int p = 0; p < look.PartCount; p++) look.SetColour(p, ElementCombat.ColorFor(element));
            }
            var along = tangent.sqrMagnitude > .001f ? Quaternion.LookRotation(tangent.normalized, Vector3.up) : Quaternion.identity;
            Whitebox.PlaceWorld(look.transform, new Vector3(position.x, whiteboxBladeHeight, position.z), along);
        }
    }
}
