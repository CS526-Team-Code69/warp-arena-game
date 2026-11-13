using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>The hint at the top of the battle screen (the snapshot's hintLead, hintKey and hintTail): what the player can do now that no
    /// panel shows, such as a card pick kept for later and the key that opens it, the build phase's walk and the key back to the cards, or
    /// the crate to click while it waits overhead. One small plate, centred under the top centre's plate (the open mode's wave plate, the
    /// story's day plate), shown only while the run has such a prompt: an orange pointer (act on it), the words, and the key they name as
    /// a keycap. The saved hint line over the hands (V2Hud.hintLabel) keeps its words but is not drawn.</summary>
    public sealed partial class V5BattleHud
    {
        // Mock-up pixels. The plate stands where the boss plate does, right under the top centre's plate (which ends 104 down): a boss
        // fight has no card pick waiting and no build phase, so the two do not meet; should they, the hint goes under the boss plate and
        // the banners step down under the hint. It ends above the banners (196 down) and the screen-edge markers along the top (190 canvas
        // units, 253 mock-up pixels), and on a narrow window it keeps its sides clear of the story's goal plate (to 364 across, from 124
        // down) by drawing itself smaller.
        const float HintTop = BossTop, HintH = 38f, HintPadLeft = 14f, HintPadRight = 18f, HintPointerW = 11f, HintPointerH = 13f;
        const float HintPointerGap = 9f, HintCapGap = 7f, HintMaxW = 860f, HintSideClear = 388f, HintStackGap = 12f;
        const float HintFadeIn = .18f, HintFadeOut = .18f;

        RectTransform hintPlate;
        CanvasGroup hintGroup;
        Text hintLeadText, hintTailText;
        V5HudKey hintKeyCap;
        float hintFade, bannerTop = BannerTop;
        bool hintWanted;
        // What the plate was last laid out for: it is laid out again only when one of these changes.
        string hintLeadShown, hintKeyShown, hintTailShown;
        Font hintFontShown;
        float hintKeyWidthShown = -1f, hintRoomShown = -1f;

        void BuildHint()
        {
            hintPlate = V5TitleKit.Rect(root, "V5Hint");
            V5BattleKit.Anchor(hintPlate, new Vector2(.5f, 1f), new Vector2(0f, -HintTop * Css), new Vector2(240f, HintH) * Css);
            hintGroup = hintPlate.gameObject.AddComponent<CanvasGroup>();
            hintGroup.blocksRaycasts = hintGroup.interactable = false;
            var plate = V5HudKit.Plate(hintPlate, "Plate", V5HudKit.PlateLook.Plain, 8f, Css);
            V5TitleKit.Stretch(plate.Rect);
            // The goal plate's pointer: an orange triangle pointing right (made pointing up, then turned).
            var pointer = V5HudKit.Arrow(hintPlate, "Pointer", new Vector2(HintPointerH, HintPointerW) * Css, V5TitleKit.Orange, -90f).rectTransform;
            pointer.anchorMin = pointer.anchorMax = new Vector2(0f, .5f);
            pointer.pivot = new Vector2(.5f, .5f);
            pointer.anchoredPosition = new Vector2((HintPadLeft + HintPointerW * .5f) * Css, 0f);
            hintLeadText = V5BattleKit.Label(hintPlate, "Lead", 17f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            hintKeyCap = V5HudKit.Key(hintPlate, "Key", "", false, Css, 24f, 13f);
            hintTailText = V5BattleKit.Label(hintPlate, "Tail", 17f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            hintPlate.gameObject.SetActive(false);
        }

        /// <summary>Shows the plate while the snapshot has a prompt for it, laid out again when its words, its key, the font or the room
        /// change; keeps the saved hint line from being drawn.</summary>
        void RenderHint(ArenaUiSnapshot s)
        {
            if (hud.hintLabel && hud.hintLabel.enabled) hud.hintLabel.enabled = false;
            if (!hintPlate) return;
            string lead = (s.hintLead ?? "").TrimEnd(), key = (s.hintKey ?? "").Trim(), tail = (s.hintTail ?? "").TrimStart();
            hintWanted = lead.Length + key.Length + tail.Length > 0;
            if (hintWanted)
            {
                if (!hintPlate.gameObject.activeSelf)
                {
                    hintFade = 0f;
                    hintGroup.alpha = 0f;
                    hintPlate.gameObject.SetActive(true);
                }
                LayHint(lead, key, tail);
            }
            PlaceHintPlate();
        }

        /// <summary>The pointer, the words before the key, the keycap, the words after it, left to right; the plate as wide as they are,
        /// drawn smaller as a whole when that is wider than the room (HintRoom).</summary>
        void LayHint(string lead, string key, string tail)
        {
            V5BattleKit.Sync(hintLeadText);
            V5BattleKit.Sync(hintTailText);
            if (hintKeyCap.Label.text != key) hintKeyCap.SetText(key);
            float keyWidth = hintKeyCap.Rect.sizeDelta.x, room = HintRoom();
            if (lead == hintLeadShown && key == hintKeyShown && tail == hintTailShown && hintLeadText.font == hintFontShown
                && Mathf.Abs(keyWidth - hintKeyWidthShown) < .01f && Mathf.Abs(room - hintRoomShown) < .5f) return;
            hintLeadShown = lead;
            hintKeyShown = key;
            hintTailShown = tail;
            hintFontShown = hintLeadText.font;
            hintKeyWidthShown = keyWidth;
            hintRoomShown = room;
            hintLeadText.text = lead;
            hintTailText.text = tail;
            bool hasLead = lead.Length > 0, hasKey = key.Length > 0, hasTail = tail.Length > 0;
            Shown(hintLeadText.gameObject, hasLead);
            Shown(hintKeyCap.gameObject, hasKey);
            Shown(hintTailText.gameObject, hasTail);
            float x = HintPadLeft + HintPointerW + HintPointerGap;
            if (hasLead) x = HintWord(hintLeadText, x);
            if (hasKey)
            {
                if (hasLead) x += HintCapGap;
                var cap = hintKeyCap.Rect;
                cap.anchorMin = cap.anchorMax = cap.pivot = new Vector2(0f, .5f);
                cap.anchoredPosition = new Vector2(x * Css, 0f);
                x += keyWidth / Css;
            }
            if (hasTail) x = HintWord(hintTailText, x + (hasLead || hasKey ? HintCapGap : 0f));
            float width = x + HintPadRight;
            hintPlate.sizeDelta = new Vector2(width, HintH) * Css;
            float k = width > room ? room / width : 1f;
            hintPlate.localScale = new Vector3(k, k, 1f);
        }

        // A run of words on the plate from x (mock-up pixels), on the plate's middle line; where it ends.
        static float HintWord(Text word, float x)
        {
            float width = V5BattleKit.Width(word) / Css;
            var rect = word.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(x * Css, 0f);
            rect.sizeDelta = new Vector2(width + 2f, 30f) * Css;
            return x + width;
        }

        static void Shown(GameObject part, bool shown)
        {
            if (part.activeSelf != shown) part.SetActive(shown);
        }

        /// <summary>The widest the plate is drawn, in mock-up pixels: HintMaxW, and on a window narrower than 16:9 no wider than the
        /// screen less the story's goal plate on either side.</summary>
        float HintRoom()
        {
            if (!canvas && root) canvas = root.GetComponentInParent<Canvas>();
            if (!canvas) return HintMaxW;
            float half = ((RectTransform)canvas.rootCanvas.transform).rect.width * .5f / Css;
            return Mathf.Clamp(2f * (half - HintSideClear), 200f, HintMaxW);
        }

        /// <summary>The plate in its place under the top centre's plate, or under the boss plate should that show too; while the plate
        /// shows, the banners keep below it (in its own place it ends above them, so they stay where they are).</summary>
        void PlaceHintPlate()
        {
            bool bossShown = hud.bossRoot && hud.bossRoot.activeSelf;
            float top = bossShown ? BossTop + BossH + HintStackGap : HintTop;
            hintPlate.anchoredPosition = new Vector2(0f, -top * Css);
            bannerTop = hintPlate.gameObject.activeSelf ? Mathf.Max(BannerTop, top + HintH * hintPlate.localScale.y + HintStackGap) : BannerTop;
        }

        /// <summary>The plate fades in when a prompt comes and out when it goes; it does not move.</summary>
        void TickHint(float dt)
        {
            if (!hintPlate || !hintPlate.gameObject.activeSelf) return;
            hintFade = hintWanted ? Mathf.Min(1f, hintFade + dt / HintFadeIn) : Mathf.Max(0f, hintFade - dt / HintFadeOut);
            hintGroup.alpha = hintFade;
            if (!hintWanted && hintFade <= 0f) hintPlate.gameObject.SetActive(false);
        }
    }
}
