using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The row of key hints at the foot of a pick screen (the mock-up's foot plate): light keycaps and what they do, "1 2 3 选择",
    /// "Esc 不拿". Only keys that really work on the screen are given to it. With the art on, the gunmetal plate; in whitebox, a flat plate
    /// with the thin edge (V5HudPlate and V5HudKey dress themselves). It is as wide as its items need and is placed by its centre.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V5PickHints : MonoBehaviour
    {
        // The mock-up's foot: padding 12 / 22, cut 10; keycaps 28 tall in 15-pixel letters, 6 apart; 8 between the keys and their word,
        // 26 between items; the words 16 pixels in Ink2.
        const float PadY = 12f, PadX = 22f, KeyH = 28f, KeyFont = 15f, KeyGap = 6f, WordGap = 8f, ItemGap = 26f, WordPx = 16f, Cut = 10f;

        float css = V5HudKit.HudCss;
        V5HudPlate plate;
        readonly List<V5HudKey> keys = new();
        readonly List<Text> words = new();
        readonly List<int> keyCounts = new();
        string shown;
        float centreX, top;

        public RectTransform Rect => (RectTransform)transform;
        public float Width => Rect.sizeDelta.x;
        public float Height => (PadY * 2f + KeyH) * css;

        public static V5PickHints Make(Transform parent, string name, float css)
        {
            var root = V5TitleKit.Rect(parent, name);
            var hints = root.gameObject.AddComponent<V5PickHints>();
            hints.css = css;
            hints.plate = V5HudKit.Plate(root, "Plate", V5HudKit.PlateLook.Plain, Cut, css);
            V5TitleKit.Stretch(hints.plate.Rect);
            return hints;
        }

        /// <summary>The items: each a list of keys and the word after them. Rebuilt only when they change.</summary>
        public void Set(List<(string[] keys, string word)> items)
        {
            var signature = new StringBuilder();
            foreach (var (itemKeys, word) in items)
            {
                foreach (var key in itemKeys) signature.Append(key).Append('\u0001');
                signature.Append(word).Append('\u0002');
            }
            string now = signature.ToString();
            if (now == shown) return;
            shown = now;
            int k = 0;
            keyCounts.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var (itemKeys, word) = items[i];
                foreach (var key in itemKeys)
                {
                    if (k >= keys.Count) keys.Add(V5HudKit.Key(transform, "Key" + (k + 1), key, false, css, KeyH, KeyFont));
                    keys[k].SetText(key);
                    V5PickKit.Show(keys[k], true);
                    k++;
                }
                keyCounts.Add(itemKeys.Length);
                if (i >= words.Count) words.Add(V5PickKit.Label(transform, "Word" + (i + 1), WordPx, css, V5TitleKit.Ink2, TextAnchor.MiddleLeft));
                V5PickKit.SetText(words[i], word);
                V5PickKit.Show(words[i], true);
            }
            for (int i = k; i < keys.Count; i++) V5PickKit.Show(keys[i], false);
            for (int i = items.Count; i < words.Count; i++) V5PickKit.Show(words[i], false);
            Layout();
        }

        /// <summary>Puts the row's centre at centreX and its top at y, measured from the parent's top-left corner.</summary>
        public void PlaceCentre(float x, float y)
        {
            centreX = x;
            top = y;
            Layout();
        }

        void LateUpdate() => Layout();

        void Layout()
        {
            float x = PadX * css, keyTop = PadY * css;
            int k = 0;
            for (int i = 0; i < keyCounts.Count; i++)
            {
                for (int j = 0; j < keyCounts[i]; j++, k++)
                {
                    var key = keys[k];
                    float w = key.Rect.sizeDelta.x;
                    V5PickKit.Place(key.Rect, x, keyTop, w, KeyH * css);
                    x += w + KeyGap * css;
                }
                if (keyCounts[i] > 0) x += (WordGap - KeyGap) * css;
                var word = words[i];
                V5PickKit.SyncFont(word);
                float wordW = Mathf.Ceil(word.preferredWidth);
                V5PickKit.Place(word.rectTransform, x, keyTop, wordW, KeyH * css);
                x += wordW + ItemGap * css;
            }
            float width = keyCounts.Count > 0 ? x - ItemGap * css + PadX * css : 0f;
            V5PickKit.Place(Rect, centreX - width * .5f, top, width, Height);
            V5PickKit.Show(plate, keyCounts.Count > 0);
        }
    }
}
