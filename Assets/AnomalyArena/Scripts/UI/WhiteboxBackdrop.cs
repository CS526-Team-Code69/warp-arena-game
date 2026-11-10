using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The picture behind a screen in whitebox (the interface's pictures become plain shapes and colours too). Over a
    /// backdrop picture (the title art behind the title and the first weapon pick, the choice screens, the weapon card, the ready and
    /// result screens) a cover of three flat blocks shows while Art.Enabled is off: a dark field, a lighter ground band along the bottom
    /// and a violet square turned on its corner for the warp. The picture under it is left as it is, so whatever animates or restores it
    /// (V3TitleBackdrop) carries on, and switching back shows it again at once. The cover sits first among the picture's children: over
    /// the picture, under everything else drawn on it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WhiteboxBackdrop : MonoBehaviour
    {
        public static readonly Color Field = new(.055f, .07f, .105f), Ground = new(.085f, .11f, .16f), Warp = new(.17f, .1f, .28f);
        // The ground band's share of the height, where the warp square stands (anchor) and its side in canvas units.
        const float GroundShare = .3f, WarpSide = 300f;
        static readonly Vector2 WarpAt = new(.76f, .64f);

        RectTransform cover;

        /// <summary>Puts the cover on a backdrop picture (once; a second call finds the first).</summary>
        public static WhiteboxBackdrop Attach(Graphic picture)
        {
            if (!picture) return null;
            if (!picture.TryGetComponent(out WhiteboxBackdrop backdrop)) backdrop = picture.gameObject.AddComponent<WhiteboxBackdrop>();
            backdrop.Apply();
            return backdrop;
        }

        /// <summary>The cover is showing (whitebox).</summary>
        public bool Covering => cover && cover.gameObject.activeSelf;

        void OnEnable() => Apply();

        void LateUpdate() => Apply();

        void Apply()
        {
            bool on = !Art.Enabled;
            if (on && !cover) Build();
            if (!cover) return;
            // Something made later may have been put first (a dim layer): the cover stays under it.
            if (on && cover.GetSiblingIndex() != 0) cover.SetAsFirstSibling();
            if (cover.gameObject.activeSelf != on) cover.gameObject.SetActive(on);
        }

        void Build()
        {
            cover = Block("WhiteboxBackdrop", transform, Field);
            Stretch(cover, Vector2.zero, Vector2.one);
            cover.SetAsFirstSibling();
            Stretch(Block("Ground", cover, Ground), Vector2.zero, new Vector2(1f, GroundShare));
            var warp = Block("Warp", cover, Warp);
            warp.anchorMin = warp.anchorMax = WarpAt;
            warp.sizeDelta = Vector2.one * WarpSide;
            warp.anchoredPosition = Vector2.zero;
            warp.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        static RectTransform Block(string name, Transform parent, Color colour)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.color = colour;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
