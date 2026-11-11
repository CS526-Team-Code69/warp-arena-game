using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The campaign screen layer's feedback (2026-11-10, from the whitebox playtest's notes). Everything the player is told to do stands
    /// in the task panel at the top left: the goal with its way and distance, under it the prompt of the thing stood at (the base door,
    /// an ore, a crate), and a short note (the keys learned so far on Day 0, the night's rule) that shows for a few seconds whenever it
    /// changes and then goes. Nothing of these is drawn at the bottom of the screen any more. Mining shows a bar there instead of a
    /// percentage in words; the lamps left are counted in their own box beside the currencies; and whatever is picked up or mined says
    /// what it was where it was taken, while its number in the corner swells for a moment.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // ───── The task panel's text ─────

        const float NoteSeconds = 8f, PromptOnlyMinHeight = 48f;
        static readonly Color PromptColour = new(1f, .86f, .5f), NoteColour = new(.74f, .82f, .94f);
        string objectiveWay, promptBody = "", noteBody = "", promptOverride = "";
        int promptOverrideFrame = -10;
        float noteUntil;
        // What the panel shows now, part by part: it is written again only when one of them changes.
        string shownGoal, shownWay, shownPrompt, shownNote;
        bool goalWritten;

        /// <summary>The goal panel's text: the goal and its way, then the prompt, then the note. Its backing grows with the text and the
        /// side-quest line follows under it. Called every frame the HUD shows; it writes only when a part has changed.</summary>
        void ComposeGoal()
        {
            if (!objectiveBack) return;
            string goal = objectiveBody ?? "", way = goal.Length > 0 ? objectiveWay ?? "" : "";
            string prompt = Time.frameCount - promptOverrideFrame <= 1 && !string.IsNullOrEmpty(promptOverride) ? promptOverride : promptBody ?? "";
            string note = Time.unscaledTime < noteUntil ? noteBody ?? "" : "";
            if (goalWritten && goal == shownGoal && way == shownWay && prompt == shownPrompt && note == shownNote) return;
            goalWritten = true; shownGoal = goal; shownWay = way; shownPrompt = prompt; shownNote = note;
            var text = new System.Text.StringBuilder();
            if (goal.Length > 0)
            {
                text.Append("<b><color=#").Append(ColorUtility.ToHtmlStringRGB(Accent)).Append(">").Append(ArenaText.Pair("目标：", "GOAL: ")).Append("</color>").Append(goal).Append("</b>");
                if (way.Length > 0) text.Append("\n<b>").Append(way).Append("</b>");
            }
            if (prompt.Length > 0)
            {
                if (text.Length > 0) text.Append("\n");
                text.Append("<b><color=#").Append(ColorUtility.ToHtmlStringRGB(PromptColour)).Append(">").Append(prompt).Append("</color></b>");
            }
            if (note.Length > 0)
            {
                if (text.Length > 0) text.Append("\n");
                text.Append("<size=19><color=#").Append(ColorUtility.ToHtmlStringRGB(NoteColour)).Append(">").Append(note).Append("</color></size>");
            }
            bool any = text.Length > 0;
            objectiveBack.gameObject.SetActive(any);
            objectiveText.text = text.ToString();
            if (any)
                objectiveBack.rectTransform.sizeDelta = new Vector2(ObjectiveWidth, Mathf.Max(goal.Length > 0 ? ObjectiveMinHeight : PromptOnlyMinHeight, objectiveText.preferredHeight + 16f));
            PlaceSideQuest();
        }

        /// <summary>The campaign's note (V2Campaign.CurrentNote): a changed one shows for NoteSeconds from now.</summary>
        void RefreshNote()
        {
            string note = campaign.CurrentNote() ?? "";
            if (note == noteBody) return;
            noteBody = note;
            noteUntil = Time.unscaledTime + NoteSeconds;
        }

        // ───── Building ─────

        RectTransform lampBox, mineBar;
        Image lampIcon, mineFill;
        Text lampCount, lampKey, mineLabel;
        static readonly Color AlloyBar = new(.36f, .62f, .9f), DarkBar = new(.56f, .3f, .86f), LampColour = new(1f, .9f, .55f);

        void BuildFeedback()
        {
            // The lamps left, in their own box beside the currencies (top right, to their left: the void currencies' row lies under
            // them), with the key that sets one down.
            lampBox = Box("Lamps", hud, new Vector2(1f, 1f), new Vector2(-464f, -20f), new Vector2(176f, 44f), new Vector2(1f, 1f), Panel);
            lampIcon = Icon(lampBox, new Vector2(0f, .5f), new Vector2(12f, 0f), 34f);
            lampCount = Label("Count", lampBox, "", 22, Soft, TextAnchor.MiddleLeft);
            Anchor(lampCount.rectTransform, new Vector2(0f, .5f), new Vector2(52f, 0f), new Vector2(64f, 36f), new Vector2(0f, .5f));
            var cap = Box("Key", lampBox, new Vector2(1f, .5f), new Vector2(-10f, 0f), new Vector2(40f, 30f), new Vector2(1f, .5f), new Color(.16f, .22f, .32f, 1f));
            Outline(cap.gameObject);
            // The key that sets a lamp down, as the player has it (ArenaKeys); a longer name than a letter shrinks to stay in the cap.
            lampKey = Label("Text", cap, ArenaKeys.LampKey, 18, Color.white, TextAnchor.MiddleCenter);
            lampKey.resizeTextForBestFit = true; lampKey.resizeTextMinSize = 9; lampKey.resizeTextMaxSize = 18;
            StretchFull(lampKey.rectTransform);
            lampBox.gameObject.SetActive(false);

            // The mining bar, bottom centre just over the weapon's name: what is being mined written inside it, filling while E is held.
            mineBar = Box("MineBar", hud, new Vector2(.5f, 0f), new Vector2(0f, 222f), new Vector2(540f, 32f), new Vector2(.5f, .5f), new Color(.03f, .05f, .08f, .94f));
            Outline(mineBar.gameObject);
            mineFill = Fill(mineBar, AlloyBar);
            mineFill.rectTransform.offsetMin = new Vector2(3f, 3f); mineFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            mineLabel = Label("Label", mineBar, "", 20, Color.white, TextAnchor.MiddleCenter);
            mineLabel.fontStyle = FontStyle.Bold;
            mineLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            StretchFull(mineLabel.rectTransform);
            Edge(mineLabel);
            mineBar.gameObject.SetActive(false);

            // What was just gained: a few texts reused in turn, placed from the screen's bottom-left corner like the marker.
            for (int i = 0; i < gains.Length; i++)
            {
                var gain = new Gain { root = MakeRect("Gain" + i, hud, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(320f, 36f)) };
                gain.icon = Icon(gain.root, new Vector2(.5f, .5f), new Vector2(-96f, 0f), 32f);
                gain.label = Label("Text", gain.root, "", 22, Color.white, TextAnchor.MiddleLeft);
                gain.label.fontStyle = FontStyle.Bold;
                gain.label.horizontalOverflow = HorizontalWrapMode.Overflow;
                Anchor(gain.label.rectTransform, new Vector2(.5f, .5f), new Vector2(-58f, 0f), new Vector2(300f, 36f), new Vector2(0f, .5f));
                Edge(gain.label);
                gain.root.gameObject.SetActive(false);
                gains[i] = gain;
            }
        }

        // ───── The lamp counter ─────

        void RefreshLampCounter()
        {
            bool show = campaign.LampCounterShown;
            if (lampBox.gameObject.activeSelf != show) lampBox.gameObject.SetActive(show);
            if (!show) return;
            // The art's lamp icon; with the art off (or no icon installed) a plain circle in the lamp's yellow: the whitebox lamp's side
            // view is a thin post, too small to read at this size.
            var sprite = Art.Enabled && campaign.art ? campaign.art.lamp : null;
            bool plain = !sprite;
            if (plain) sprite = WhiteboxIcons.Mark(WhiteboxIcons.Glyph.Circle);
            if (lampIcon.sprite != sprite) lampIcon.sprite = sprite;
            lampIcon.color = plain ? LampColour : Color.white;
            int left = campaign.LampStock;
            lampCount.text = "×" + left;
            lampCount.color = left > 0 ? Soft : Warn;
            if (lampKey.text != ArenaKeys.LampKey) lampKey.text = ArenaKeys.LampKey;
        }

        // ───── The mining bar and what was just gained ─────

        sealed class Gain
        {
            public RectTransform root; public Image icon; public Text label;
            public Vector3 world; public float age = 99f; public int which = -1, amount;
        }

        // A gain shows this long, rising this far (canvas units); another of the same kind within MergeSeconds adds to it.
        const float GainSeconds = 1.6f, GainRise = 70f, GainFade = .5f, MergeSeconds = .45f, PulseSeconds = .35f;
        readonly Gain[] gains = new Gain[6];
        readonly float[] currencyPulse = new float[3];
        int gainCursor;

        /// <summary>Says what was just gained where it was taken ("+3 alien energy" with its icon, rising and fading), and lets its number
        /// in the corner swell for a moment. which: 0 alien energy, 1 alien alloy, 2 dark matter.</summary>
        public void ShowGain(int which, int amount, Vector3 world)
        {
            if (amount <= 0 || which < 0 || which > 2 || gains[0] == null) return;
            ApplyFont();
            currencyPulse[which] = 1f;
            Gain gain = null;
            foreach (var live in gains)
                if (live.which == which && live.age < MergeSeconds) { gain = live; break; }
            if (gain == null)
            {
                gain = gains[gainCursor];
                gainCursor = (gainCursor + 1) % gains.Length;
                gain.amount = 0;
            }
            gain.which = which; gain.amount += amount; gain.world = world; gain.age = 0f;
            var sprite = CurrencySprite(which);
            gain.icon.enabled = sprite;
            if (sprite) gain.icon.sprite = sprite;
            gain.label.text = $"+{gain.amount} {CurrencyName(which)}";
            gain.root.gameObject.SetActive(true);
        }

        static string CurrencyName(int which) => which switch
        {
            0 => ArenaText.Pair("异星能量", "alien energy"),
            1 => ArenaText.Pair("异星合金", "alien alloy"),
            _ => ArenaText.Pair("暗物质", "dark matter")
        };

        /// <summary>Every frame: the mining bar follows the mining, the gain texts rise and fade, the corner's numbers settle.</summary>
        void TickFeedback(bool shown)
        {
            if (!campaign || !mineBar) return;
            float dt = Time.unscaledDeltaTime;
            bool mining = campaign.MiningProgress(out float progress, out string what, out bool dark);
            if (mineBar.gameObject.activeSelf != mining) mineBar.gameObject.SetActive(mining);
            if (mining)
            {
                mineFill.fillAmount = progress;
                mineFill.color = dark ? DarkBar : AlloyBar;
                if (mineLabel.text != what) mineLabel.text = what;
            }
            for (int i = 0; i < currencyPulse.Length; i++)
            {
                if (currencyPulse[i] <= 0f) continue;
                currencyPulse[i] = Mathf.Max(0f, currencyPulse[i] - dt / PulseSeconds);
                currencyLabels[i].rectTransform.localScale = Vector3.one * (1f + .4f * currencyPulse[i]);
                currencyLabels[i].color = Color.Lerp(Soft, Accent, currencyPulse[i]);
            }
            if (!shown || !gm || !gm.cam) return;
            float scale = root.lossyScale.x > 0f ? 1f / root.lossyScale.x : 1f;
            foreach (var gain in gains)
            {
                if (gain == null || gain.age >= GainSeconds) continue;
                gain.age += dt;
                if (gain.age >= GainSeconds) { gain.root.gameObject.SetActive(false); gain.which = -1; continue; }
                Vector3 sp = gm.cam.WorldToScreenPoint(gain.world + Vector3.up * 1.4f);
                gain.root.anchoredPosition = new Vector2(sp.x * scale, sp.y * scale + GainRise * gain.age / GainSeconds);
                float alpha = Mathf.Clamp01((GainSeconds - gain.age) / GainFade);
                gain.label.color = new Color(1f, 1f, 1f, alpha);
                gain.icon.color = new Color(1f, 1f, 1f, alpha);
            }
        }
    }
}
