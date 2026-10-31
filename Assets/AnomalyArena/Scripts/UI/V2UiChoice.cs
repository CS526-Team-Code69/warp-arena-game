using UnityEngine;

namespace AnomalyArena
{
    public class V2UiChoice : MonoBehaviour
    {
        public UnityEngine.UI.Text title, body, kind, badge;
        public UnityEngine.UI.Image icon;
        public UnityEngine.UI.Image weaponIcon;
        public UnityEngine.UI.Image contentIcon;
        public UnityEngine.UI.Button button;
        [Range(.3f, 1f), Tooltip("Smallest body size, as a share of the saved font size, that a long card text may shrink to so it stays inside its box.")]
        public float minBodyScale = .6f;
        [Min(8), Tooltip("Largest size of the card text (owner 2026-10-28: the saved size was easy to skip). Long texts still shrink, no smaller than before.")]
        public int bodyFontSize = 19;

        [System.NonSerialized] public System.Action<CardRarity> rarityApplied;
        int savedBodySize;

        void Awake()
        {
            FitBody();
            ArenaEmphasis.Style(body);
        }

        public void FitBody()
        {
            if (!body || savedBodySize > 0) return;
            GrowBody();
            body.resizeTextMaxSize = body.fontSize;
            body.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(savedBodySize * minBodyScale));
            body.verticalOverflow = UnityEngine.VerticalWrapMode.Truncate;
            body.resizeTextForBestFit = false;
        }

        public void ShowBody(string text)
        {
            FitBody();
            ArenaTextFit.Show(body, text, body.resizeTextMaxSize, body.resizeTextMinSize);
        }

        void GrowBody()
        {
            savedBodySize = body.fontSize;
            body.fontSize = Mathf.Max(body.fontSize, bodyFontSize);
            if (!badge) return;
            RectTransform box = body.rectTransform, below = badge.rectTransform;
            if (below.parent != box.parent || box.pivot.y != 1f || below.pivot.y != 1f || box.anchorMin != below.anchorMin || box.anchorMax != below.anchorMax) return;
            float room = box.anchoredPosition.y - box.sizeDelta.y - below.anchoredPosition.y - 4f;
            if (room > 0f) box.sizeDelta += new Vector2(0f, room);
        }

        public void Render(ArenaUiCard card, V2ArtSet art)
        {
            bool show = card != null && !string.IsNullOrEmpty(card.title);
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
            if (!show) return;
            if (title.text != card.title) title.text = card.title;
            ShowBody(ArenaEmphasis.Apply(card.body));
            if (kind.text != card.kind) kind.text = card.kind;
            if (badge.text != card.badge) badge.text = card.badge;
            ApplyRarity(card.rarity);
            button.interactable = card.available;
            V2Hud.SetElementIcon(icon, art, card.elementIndex);
            V2Hud.SetWeaponIcon(weaponIcon, art, card.formIndex);
            V2Hud.SetContentIcon(contentIcon, art, card.skillIndex, card.statIndex, card.axisIndex);
        }

        public void ApplyRarity(CardRarity rarity)
        {
            Color tint = ArenaRarity.ColorFor(rarity);
            if (title) title.color = tint;
            if (badge) badge.color = tint;
            rarityApplied?.Invoke(rarity);
            if (!button) return;
            var colours = button.colors;
            colours.normalColor = Color.Lerp(Color.white, tint, .20f);
            colours.highlightedColor = Color.Lerp(Color.white, tint, .45f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = Color.Lerp(Color.white, tint, .65f);
            button.colors = colours;
        }
    }
}
