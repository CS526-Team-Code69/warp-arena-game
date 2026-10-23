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

        void Awake() => FitBody();

        public void FitBody()
        {
            if (!body || body.resizeTextForBestFit) return;
            body.resizeTextMaxSize = body.fontSize;
            body.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(body.fontSize * minBodyScale));
            body.verticalOverflow = UnityEngine.VerticalWrapMode.Truncate;
            body.resizeTextForBestFit = true;
        }

        public void Render(ArenaUiCard card, V2ArtSet art)
        {
            bool show = card != null && !string.IsNullOrEmpty(card.title);
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
            if (!show) return;
            FitBody();
            if (title.text != card.title) title.text = card.title;
            if (body.text != card.body) body.text = card.body;
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
