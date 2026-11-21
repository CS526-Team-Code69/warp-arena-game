using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The campaign screen layer's feedback (2026-11-10, from the whitebox playtest's notes). Everything the player is told to do stands
    /// in the task panel at the top left: the goal with its way and distance, under it the prompt of the thing stood at (the base door,
    /// an ore, a crate), and a short note (the keys learned so far on Day 0, the night's rule) that shows for a few seconds whenever it
    /// changes and then goes. Nothing of these is drawn at the bottom of the screen any more. Mining shows a bar there instead of a
    /// percentage in words, with the resource's icon before its words and its fill in the resource's colour; the lamps left are counted
    /// in their own box beside the currencies; and whatever is picked up or mined says what it was where it was taken, while its icon
    /// flies from over the player's head into its cell of the wallet, which swells for a moment as it lands.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // ───── The task panel's text ─────

        const float NoteSeconds = 8f, PromptOnlyMinHeight = 38f;
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
                text.Append("<size=15><color=#").Append(ColorUtility.ToHtmlStringRGB(NoteColour)).Append(">").Append(note).Append("</color></size>");
            }
            bool any = text.Length > 0;
            if (V5HudKit.On)
            {
                // The fifth pass's goal plate lays out the parts itself (V5StoryGoal.cs); the old text stays up to date, hidden.
                objectiveText.text = text.ToString();
                storyGoal = goal; storyWay = way; storyPrompt = prompt; storyNote = note;
                LayOutStoryGoal();
                return;
            }
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
        Image lampIcon, mineFill, mineIcon;
        Text lampCount, lampKey, mineLabel;
        static readonly Color LampColour = new(1f, .9f, .55f);
        // The mining bar's icon (canvas units, from the bar's left edge) and where its words start after it.
        const float MineIconLeft = 6f, MineIconSize = 26f, MineLabelLeft = 40f;

        void BuildFeedback()
        {
            // The lamps left, in their own box beside the currencies (top right, to their left: the void currencies' row lies under
            // them), with the key that sets one down.
            lampBox = Box("Lamps", hud, new Vector2(1f, 1f), new Vector2(-24f - CurrencyWidth - 10f, -20f), new Vector2(160f, CurrencyHeight), new Vector2(1f, 1f), Panel);
            lampIcon = Icon(lampBox, new Vector2(0f, .5f), new Vector2(10f, 0f), 30f);
            lampCount = Label("Count", lampBox, "", 19, Soft, TextAnchor.MiddleLeft);
            Anchor(lampCount.rectTransform, new Vector2(0f, .5f), new Vector2(46f, 0f), new Vector2(58f, 32f), new Vector2(0f, .5f));
            var cap = Box("Key", lampBox, new Vector2(1f, .5f), new Vector2(-9f, 0f), new Vector2(36f, 28f), new Vector2(1f, .5f), new Color(.16f, .22f, .32f, 1f));
            Outline(cap.gameObject);
            // The key that sets a lamp down, as the player has it (ArenaKeys); a longer name than a letter shrinks to stay in the cap.
            lampKey = Label("Text", cap, ArenaKeys.LampKey, 16, Color.white, TextAnchor.MiddleCenter);
            lampKey.resizeTextForBestFit = true; lampKey.resizeTextMinSize = 9; lampKey.resizeTextMaxSize = 16;
            StretchFull(lampKey.rectTransform);
            lampBox.gameObject.SetActive(false);

            // The mining bar, bottom centre just over the weapon's name: the resource's icon, then what is being mined ("采集 合金 +5")
            // written inside it, filling in the resource's colour while E is held.
            mineBar = Box("MineBar", hud, new Vector2(.5f, 0f), new Vector2(0f, 222f), new Vector2(540f, 32f), new Vector2(.5f, .5f), new Color(.03f, .05f, .08f, .94f));
            Outline(mineBar.gameObject);
            mineFill = Fill(mineBar, MineColour(V2Campaign.NodeKind.Ore));
            mineFill.rectTransform.offsetMin = new Vector2(3f, 3f); mineFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            mineIcon = Icon(mineBar, new Vector2(0f, .5f), new Vector2(MineIconLeft, 0f), MineIconSize);
            mineLabel = Label("Label", mineBar, "", 20, Color.white, TextAnchor.MiddleLeft);
            mineLabel.fontStyle = FontStyle.Bold;
            mineLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(mineLabel.rectTransform, MineLabelLeft, 0f, 8f, 0f);
            Edge(mineLabel);
            mineBar.gameObject.SetActive(false);

            // The icons that fly from over the player's head into the wallet: a few reused in turn, placed like the gain texts.
            for (int i = 0; i < flights.Length; i++)
            {
                var flight = new Flight { rect = MakeRect("Flight" + i, hud, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(FlightIconSize, FlightIconSize)) };
                flight.icon = flight.rect.gameObject.AddComponent<Image>();
                flight.icon.preserveAspect = true;
                flight.icon.raycastTarget = false;
                flight.rect.gameObject.SetActive(false);
                flights[i] = flight;
            }

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
            if (V5HudKit.On) { RefreshStoryLamps(); return; } // the fifth pass's lamp box (V5StoryWallet.cs)
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

        /// <summary>Says what was just gained where it was taken ("+3 alien energy" with its icon, rising and fading), and sends its icon
        /// from over the player's head into its cell of the wallet, whose number swells as it lands (at once when it cannot fly). which:
        /// 0 alien energy, 1 alloy, 2 dark matter.</summary>
        public void ShowGain(int which, int amount, Vector3 world)
        {
            if (amount <= 0 || which < 0 || which > 2 || gains[0] == null) return;
            ApplyFont();
            if (!StartFlight(which)) currencyPulse[which] = 1f;
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
            1 => ArenaText.Pair("合金", "alloy"),
            _ => ArenaText.Pair("暗物质", "dark matter")
        };

        // ───── The icon that flies into the wallet ─────

        sealed class Flight
        {
            public RectTransform rect; public Image icon;
            public Vector2 from; public float age = 99f; public int which = -1;
        }

        // An icon flies this long, from this far over the player's feet (world units), arching this high (canvas units) on its way; a gain
        // of the same kind within FlightMergeSeconds of a flight's start rides with it instead of flying on its own.
        const float FlightSeconds = .5f, FlightMergeSeconds = .3f, FlightHead = 2.4f, FlightArc = 60f, FlightIconSize = 34f;
        readonly Flight[] flights = new Flight[4];

        /// <summary>A flight of this kind is on its way to the wallet (its number waits for it to land before it swells).</summary>
        bool Flying(int which)
        {
            foreach (var flight in flights)
                if (flight != null && flight.which == which) return true;
            return false;
        }

        /// <summary>Sends a currency's icon from over the player's head to its wallet cell; false when it cannot fly (no player, camera or
        /// cell on the screen, every flight busy).</summary>
        bool StartFlight(int which)
        {
            var cell = WalletCell(which);
            if (!gm || !gm.cam || !gm.player || !cell || !cell.gameObject.activeInHierarchy || !hud || !hud.gameObject.activeInHierarchy) return false;
            foreach (var live in flights)
                if (live != null && live.which == which && live.age < FlightMergeSeconds) return true;
            Flight flight = null;
            foreach (var free in flights)
                if (free != null && free.which < 0) { flight = free; break; }
            if (flight == null) return false;
            Vector3 sp = gm.cam.WorldToScreenPoint(gm.player.Position + Vector3.up * FlightHead);
            if (sp.z <= 0f) return false;
            float scale = root.lossyScale.x > 0f ? 1f / root.lossyScale.x : 1f;
            flight.from = new Vector2(sp.x, sp.y) * scale;
            flight.which = which;
            flight.age = 0f;
            var sprite = CurrencySprite(which);
            flight.icon.sprite = sprite ? sprite : WhiteSprite();
            flight.icon.color = sprite ? Color.white : ResourceColours.Of(which);
            flight.rect.anchoredPosition = flight.from;
            flight.rect.localScale = Vector3.one;
            flight.rect.SetAsLastSibling();
            flight.rect.gameObject.SetActive(true);
            return true;
        }

        /// <summary>Every frame: each flight eases from its start to its cell (the cell's place read again, as the wallet may move) on a
        /// low arc, shrinking a little, and lands when its time is up, the HUD is hidden or its cell has gone.</summary>
        void TickFlights(float dt, bool shown)
        {
            float scale = root.lossyScale.x > 0f ? 1f / root.lossyScale.x : 1f;
            foreach (var flight in flights)
            {
                if (flight == null || flight.which < 0) continue;
                flight.age += dt;
                var cell = WalletCell(flight.which);
                if (!shown || !cell || !cell.gameObject.activeInHierarchy || flight.age >= FlightSeconds) { Land(flight); continue; }
                Vector2 to = RectTransformUtility.WorldToScreenPoint(null, cell.position) * scale;
                float t = Mathf.Clamp01(flight.age / FlightSeconds), eased = t * t * (3f - 2f * t);
                flight.rect.anchoredPosition = Vector2.LerpUnclamped(flight.from, to, eased) + Vector2.up * (FlightArc * Mathf.Sin(t * Mathf.PI));
                flight.rect.localScale = Vector3.one * Mathf.Lerp(1.15f, .75f, eased);
            }
        }

        /// <summary>A flight reaches its cell: the icon goes, the number swells and brightens, and the fifth pass's medal swells with it.</summary>
        void Land(Flight flight)
        {
            int which = flight.which;
            flight.which = -1;
            flight.age = 99f;
            flight.rect.gameObject.SetActive(false);
            if (which < 0 || which >= currencyPulse.Length) return;
            currencyPulse[which] = 1f;
            if (V5HudKit.On && storyBuilt && storyWalletMedals[which]) PulseOnce(storyWalletMedals[which].transform);
        }

        /// <summary>Every frame: the mining bar follows the mining, the gain texts rise and fade, the icons fly to the wallet, the corner's
        /// numbers settle.</summary>
        void TickFeedback(bool shown)
        {
            if (!campaign || !mineBar) return;
            float dt = Time.unscaledDeltaTime;
            bool mining = campaign.MiningProgress(out float progress, out string what, out V2Campaign.NodeKind kind);
            if (mineBar.gameObject.activeSelf != mining) mineBar.gameObject.SetActive(mining);
            if (mining)
            {
                mineFill.fillAmount = progress;
                mineFill.color = MineColour(kind);
                // Dark matter shows its own icon; alloy and void alloy the alloy's (the void wallet does the same).
                var sprite = CurrencySprite(kind == V2Campaign.NodeKind.Dark ? 2 : 1);
                mineIcon.enabled = sprite;
                if (sprite && mineIcon.sprite != sprite) mineIcon.sprite = sprite;
                if (mineLabel.text != what) mineLabel.text = what;
            }
            TickFlights(dt, shown);
            for (int i = 0; i < currencyPulse.Length; i++)
            {
                if (currencyPulse[i] <= 0f) continue;
                currencyPulse[i] = Mathf.Max(0f, currencyPulse[i] - dt / PulseSeconds);
                if (V5HudKit.On) { StoryPulseLabel(currencyLabels[i], WalletColour(i), currencyPulse[i]); continue; }
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
