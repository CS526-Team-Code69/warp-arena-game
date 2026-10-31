using UnityEngine;

namespace AnomalyArena
{

    public class V2Hud : MonoBehaviour
    {
        [Header("Run and independent interstellar art")]
        [Tooltip("A MonoBehaviour implementing IArenaUiSource, normally the GameManager.")]
        public MonoBehaviour modelSource;
        public V2ArtSet art;
        [Tooltip("Reference resolution is 1600 × 900; update numeric UI at 10 Hz.")]
        public float refreshInterval = .1f;

        [Header("Scene-authored HUD")]
        public GameObject gameplayRoot;
        public UnityEngine.UI.Text hpLabel, staminaLabel, energyLabel, xpLabel, waveLabel;
        public UnityEngine.UI.Text batchLabel;
        public UnityEngine.UI.Text statusLabel, immunityLabel, hintLabel, bannerLabel;
        public UnityEngine.UI.Text artLabel;
        public UnityEngine.UI.Image hpFill, shieldFill, staminaFill, energyFill, xpFill, chargeFill;
        public GameObject energyRoot, shieldRoot;
        public GameObject bossRoot;
        public UnityEngine.UI.Image bossHpFill, bossShieldFill;
        public UnityEngine.UI.Image[] statusIcons;
        public UnityEngine.UI.Text[] statusCounts;
        [Tooltip("Ten scene-authored markers; counts above ten remain visible in the XP text.")]
        public UnityEngine.UI.Image[] draftPips;
        [Tooltip("Three scene-authored immunity glyphs in the disclosed Tyrant element order.")]
        public UnityEngine.UI.Image[] bossImmunityIcons;
        public UnityEngine.UI.RawImage themeBackground;
        public UnityEngine.UI.Image damageFlash;
        public V2UiSlot[] handSlots, autoSlots, skillSlots;
        public V2UiScreen[] screens;
        [Tooltip("Plays on the picked card when a card is taken; optional.")] public V2CardPickFx cardPickFx;
        public GameObject earlyStartPrompt;
        public UnityEngine.UI.Text earlyStartLabel;
        [Tooltip("Fixed, saved labels reused for floating damage; no runtime UI object creation.")]
        public UnityEngine.UI.Text[] damageLabels;
        [Header("Official Localization")]
        public Canvas localizedCanvas;
        public UnityEngine.UI.Text[] fontLabels;
        Font appliedFont;

        IArenaUiSource source;
        float nextRefresh, transientTime, flash;
        string transientText;
        Vector3[] damageWorld;
        float[] damageAge;
        Color[] damageColor;
        int damageCursor;
        ArenaUiSnapshot lastSnapshot;
        int seenPick = -1;

        GameObject bootCover;

        void Awake()
        {
            source = modelSource as IArenaUiSource;
            if (localizedCanvas) localizedCanvas.enabled = ArenaText.Ready;
            if (!ArenaText.Ready)
            {
                bootCover = new GameObject("BootCover", typeof(Canvas), typeof(UnityEngine.UI.Image));
                var cover = bootCover.GetComponent<Canvas>();
                cover.renderMode = RenderMode.ScreenSpaceOverlay; cover.sortingOrder = short.MaxValue;
                var black = bootCover.GetComponent<UnityEngine.UI.Image>();
                black.color = Color.black; black.raycastTarget = false;
            }
            int n = damageLabels?.Length ?? 0;
            damageWorld = new Vector3[n];
            damageAge = new float[n];
            damageColor = new Color[n];
            for (int i = 0; i < n; i++) { damageAge[i] = 1f; damageLabels[i].gameObject.SetActive(false); }
            if (themeBackground) themeBackground.texture = art ? art.themeBackground : null;
            SimplifyChineseTitle();
            PlaceControlsTables();

            if (screens != null)
                foreach (var screen in screens)
                {
                    if (!screen || screen.state is not (ArenaUiState.Loadout or ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.ReplaceWeapon
                            or ArenaUiState.ReplaceSkill or ArenaUiState.Ready or ArenaUiState.WeaponIntro)) continue;
                    ArenaEmphasis.Style(screen.body);
                    if (screen.state == ArenaUiState.WeaponIntro && screen.body)
                        weaponCardSmallest = Mathf.Max(8, screen.body.resizeTextForBestFit ? screen.body.resizeTextMinSize : screen.body.fontSize);
                }
        }

        const int WeaponCardTextSize = 24;
        int weaponCardSmallest = 10;

        public const string ChineseEditionScene = "ArenaV2_CN";
        static readonly string[] ChineseTitleHidden = { "InterstellarSubtitle", "ToggleArt", "ChineseTeam", "EnglishShowcase" };

        void SimplifyChineseTitle()
        {
            if (screens == null || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != ChineseEditionScene) return;
            foreach (var screen in screens)
            {
                if (!screen || screen.state != ArenaUiState.Title) continue;
                var content = screen.transform.Find("Content");
                if (!content) continue;
                foreach (var name in ChineseTitleHidden)
                {
                    var element = content.Find(name);
                    if (element) element.gameObject.SetActive(false);
                }
            }
        }

        void Update()
        {
            if (localizedCanvas && localizedCanvas.enabled != ArenaText.Ready) localizedCanvas.enabled = ArenaText.Ready;
            if (!ArenaText.Ready) return;
            if (bootCover) Destroy(bootCover);
            if (ArenaText.Font && appliedFont != ArenaText.Font)
            {
                appliedFont = ArenaText.Font;
                if (fontLabels != null) foreach (var label in fontLabels) if (label) label.font = appliedFont;
                nextRefresh = 0f;
            }
            float dt = Time.unscaledDeltaTime;
            transientTime = Mathf.Max(0f, transientTime - dt);
            flash = Mathf.Max(0f, flash - dt * 3f);
            if (damageFlash)
            {

                if (!edgeVignette) { edgeVignette = EdgeVignette(); damageFlash.sprite = edgeVignette; damageFlash.type = UnityEngine.UI.Image.Type.Simple; }
                damageFlash.color = new Color(1f, .1f, .04f, flash * .45f);
            }
            TickDamage(dt);

            if (lastSnapshot != null && lastSnapshot.cardPickSequence != seenPick) nextRefresh = 0f;
            if (source == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + Mathf.Max(.02f, refreshInterval);
            Render(source.UiSnapshot);
            ShowHowToPlayOnArrival(source.UiSnapshot);
        }

        // A copy of the restart button that opens the how-to-play pages over the pause screen.
        void AddHowToPlayButton(Transform content)
        {
            if (content.Find("HowToPlay") || content.Find("RestartRun") is not { } restart) return;
            var copy = Instantiate(restart.gameObject, content);
            copy.name = "HowToPlay";
            if (copy.GetComponent<ArenaUiButton>() is { } relay) Destroy(relay);
            foreach (var localizer in copy.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true)) Destroy(localizer);
            var button = copy.GetComponent<UnityEngine.UI.Button>();
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.onClick.AddListener(() => V2Tutorial.Show(appliedFont, null));
            howToPlayLabel = copy.GetComponentInChildren<UnityEngine.UI.Text>();
            if (howToPlayLabel) howToPlayLabel.text = "HOW TO PLAY";
        }

        public void Dispatch(ArenaUiCommand command, int index = -1)
        {
            if (command == ArenaUiCommand.Start && source != null && source.UiSnapshot.state == ArenaUiState.Title) howToPlayPending = true;
            source?.ExecuteUiCommand(command, index);
        }

        // START RUN from the title opens the how-to-play pages once the run reaches the arena. Restart and quit go back
        // through the title, so every run gets them; a run started by code (the tests) does not.
        bool howToPlayPending;

        void ShowHowToPlayOnArrival(ArenaUiSnapshot s)
        {
            if (!howToPlayPending || s.state != ArenaUiState.SupplyDrop) return;
            howToPlayPending = false;
            V2Tutorial.Show(appliedFont, null);
        }
        public void RefreshView(){if(source!=null&&ArenaText.Ready)Render(source.UiSnapshot);}

        public void ShowBanner(string message, float seconds = 2.2f)
        {
            transientText = message;
            transientTime = seconds;
        }

        public void FlashDamage() => flash = 1f;

        Sprite edgeVignette;

        public static Sprite EdgeVignette()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HitEdgeVignette", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float t = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - .45f) / .55f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * t * t * (3f - 2f * t)));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        public void DamageNumber(Vector3 world, float amount, Color color, bool heavy)
        {
            if (damageLabels == null || damageLabels.Length == 0) return;
            int i = damageCursor++ % damageLabels.Length;
            damageWorld[i] = world;
            damageAge[i] = 0f;
            damageColor[i] = color;
            var label = damageLabels[i];
            label.text = $"-{Mathf.RoundToInt(amount)}";
            label.fontSize = heavy ? 30 : 22;
            label.gameObject.SetActive(true);
        }

        void TickDamage(float dt)
        {
            var cam = GameManager.Instance ? GameManager.Instance.cam : null;
            if (!cam || damageLabels == null) return;
            for (int i = 0; i < damageLabels.Length; i++)
            {
                if (damageAge[i] >= .8f) continue;
                damageAge[i] += dt;
                var label = damageLabels[i];
                if (damageAge[i] >= .8f) { label.gameObject.SetActive(false); continue; }
                Vector3 sp = cam.WorldToScreenPoint(damageWorld[i] + cam.transform.up * (.6f + damageAge[i] * 2f));
                if (sp.z <= 0f) { label.gameObject.SetActive(false); continue; }
                label.rectTransform.position = sp;
                float pop = 1f + .5f * Mathf.Clamp01(1f - damageAge[i] / .12f);
                label.rectTransform.localScale = Vector3.one * pop;
                Color c = damageColor[i]; c.a *= 1f - damageAge[i] / .8f;
                label.color = c;
            }
        }

        void Render(ArenaUiSnapshot s)
        {
            if (s == null) return;
            lastSnapshot = s;

            if (seenPick < 0) seenPick = s.cardPickSequence;
            else if (s.cardPickSequence != seenPick) { seenPick = s.cardPickSequence; PlayCardPick(s); }

            bool gameplay = s.state is ArenaUiState.SupplyDrop or ArenaUiState.WaveIntro or ArenaUiState.WaveActive or ArenaUiState.WaveEnd
                or ArenaUiState.VoidTrial or ArenaUiState.FinalBoss or ArenaUiState.TrialReturn or ArenaUiState.Cutscene or ArenaUiState.LevelUp || s.buildWalking;
            gameplayRoot.SetActive(gameplay);
            if (waveLabel) waveLabel.gameObject.SetActive(s.state != ArenaUiState.SupplyDrop);
            if (themeBackground) themeBackground.gameObject.SetActive(s.state is ArenaUiState.Title or ArenaUiState.Loadout);
            SetText(hpLabel, ArenaText.Text("ui.hp", Mathf.CeilToInt(s.hp), Mathf.CeilToInt(s.maxHp)));
            SetFill(hpFill, s.hp, s.maxHp);
            shieldRoot.SetActive(s.shield > 0f);

            SetFill(shieldFill, s.shield, s.maxHp * .6f);
            SetText(staminaLabel, ArenaText.Text("ui.stamina"));
            SetFill(staminaFill, s.stamina, 1f);
            if (staminaFill) staminaFill.color = s.staminaExhausted ? new Color(.48f, .55f, .63f) : ElementColors[3];
            energyRoot.SetActive(s.energyUnlocked);
            SetText(energyLabel, ArenaText.Text(s.voidEnergy ? "ui.energy.void" : "ui.energy", Mathf.CeilToInt(s.energy), Mathf.CeilToInt(s.maxEnergy)));
            SetFill(energyFill, s.energy, s.maxEnergy);
            energyFill.color = s.voidEnergy ? ElementColors[6] : new Color(.22f, .68f, .93f);
            string level = s.level >= s.maxLevel ? ArenaText.Text("ui.level.max") : s.level.ToString();
            SetText(xpLabel, ArenaText.Text("ui.xp", level, Mathf.FloorToInt(s.xp), Mathf.CeilToInt(s.xpNeeded), s.bankedDrafts));

            xpLabel.color = s.voidLevel ? ElementColors[6] : Color.white;
            SetFill(xpFill, s.xp, s.xpNeeded);
            if (draftPips != null)
                for (int i = 0; i < Mathf.Min(10, draftPips.Length); i++)
                {
                    if (!draftPips[i]) continue;
                    bool visible = i < s.bankedDrafts;
                    if (draftPips[i].gameObject.activeSelf != visible) draftPips[i].gameObject.SetActive(visible);
                }
            SetText(waveLabel, s.state is ArenaUiState.FinalBoss or ArenaUiState.VoidTrial
                ? ArenaText.Text("ui.boss.hp", s.state == ArenaUiState.FinalBoss ? ArenaText.Pair("THE TYRANT KING","THE TYRANT KING") : ArenaText.Pair("THE NASH PROPHET","THE NASH PROPHET"), Mathf.CeilToInt(s.bossHp), Mathf.CeilToInt(s.bossMaxHp), Mathf.CeilToInt(s.bossShield))
                : s.waveTimeExpired ? ArenaText.Pair($"WAVE {s.wave:00} / {s.maxWaves:00} · CLEAR SURVIVORS",$"WAVE {s.wave:00} / {s.maxWaves:00} · CLEAR SURVIVORS")
                : ArenaText.Text("ui.wave", s.wave.ToString("00"), s.maxWaves.ToString("00"), Mathf.CeilToInt(s.waveTime).ToString("00")));
            if (batchLabel)
            {
                batchLabel.gameObject.SetActive(s.state == ArenaUiState.WaveActive);
                batchLabel.supportRichText = true;                                   
                SetText(batchLabel, s.waveTimeExpired
                    ? ArenaText.Pair($"SPAWNING STOPPED · <color=#FF5F4F>{s.waveLiving} ALIVE</color>",$"SPAWNING STOPPED · <color=#FF5F4F>{s.waveLiving} ALIVE</color>")
                    : ArenaText.Pair($"<color=#FF5F4F>{s.waveLiving} ALIVE</color> · <color=#FFD24A>{s.wavePending} PENDING</color>",$"<color=#FF5F4F>{s.waveLiving} ALIVE</color> · <color=#FFD24A>{s.wavePending} PENDING</color>"));
            }
            bool boss = s.state is ArenaUiState.VoidTrial or ArenaUiState.FinalBoss;
            waveLabel.color = !boss && s.waveTime <= 5f ? new Color(1f, .53f, .31f) : Color.white;
            if (bossRoot) bossRoot.SetActive(boss);
            SetFill(bossHpFill, s.bossHp, s.bossMaxHp);
            SetFill(bossShieldFill, s.bossShield, s.bossMaxHp);
            if (bossShieldFill) bossShieldFill.transform.parent.gameObject.SetActive(s.bossShield > 0f);
            RenderPlayerStatuses(s.playerStatuses);
            SetText(immunityLabel, s.bossImmunities);
            if (bossImmunityIcons != null)
                for (int i = 0; i < bossImmunityIcons.Length; i++)
                {
                    int index = s.wave >= 11 && s.bossImmunityIndices != null && i < s.bossImmunityIndices.Length ? s.bossImmunityIndices[i] : -1;
                    SetElementIcon(bossImmunityIcons[i], art, index);
                }
            SetText(hintLabel, s.hint);
            SetText(bannerLabel, transientTime > 0f ? transientText : s.banner);
            SetText(artLabel, ArenaText.Text(s.illustrated ? "ui.art.illustrated" : "ui.art.whitebox"));
            chargeFill.gameObject.SetActive(s.charge01 > 0f);
            SetFill(chargeFill, s.charge01, 1f);
            RenderSlots(handSlots, s.handSlots); RenderSlots(autoSlots, s.autoSlots); RenderSlots(skillSlots, s.skillSlots);
            foreach (var screen in screens)
            {

                bool active = (screen.state == s.state || screen.state == ArenaUiState.Draft && s.state == ArenaUiState.LevelUp) && !(s.buildWalking && screen.state is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready);
                if (screen.gameObject.activeSelf != active) screen.gameObject.SetActive(active);
                if (active) RenderScreen(screen, s);
            }
            earlyStartPrompt.SetActive(s.earlyStartConfirmation);
            SetText(earlyStartLabel, ArenaText.Text("ui.early.question", s.bankedDrafts));
        }

        void PlayCardPick(ArenaUiSnapshot s)
        {
            V2Audio.Instance?.PlayCardPick(s.cardPickRarity);
            if (!cardPickFx || screens == null || s.cardPickIndex < 0) return;
            foreach (var screen in screens)
            {
                if (!screen || screen.state != s.cardPickState || screen.choices == null || s.cardPickIndex >= screen.choices.Length) continue;
                var choice = screen.choices[s.cardPickIndex];
                if (choice && choice.gameObject.activeInHierarchy) cardPickFx.Play((RectTransform)choice.transform, s.cardPickRarity);
                return;
            }
        }

        void RenderSlots(V2UiSlot[] views, ArenaUiSlot[] values)
        {
            if (views == null || values == null) return;
            for (int i = 0; i < views.Length; i++)
            {

                bool used = i < values.Length;
                if (views[i] && views[i].gameObject.activeSelf != used) views[i].gameObject.SetActive(used);
                if (used && views[i]) views[i].Render(values[i], art);
            }
        }

        void RenderScreen(V2UiScreen screen, ArenaUiSnapshot s)
        {
            bool build = s.state is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.ReplaceWeapon
                or ArenaUiState.ReplaceSkill or ArenaUiState.Ready;
            if (screen.clock)
            {
                screen.clock.gameObject.SetActive(build && !s.isGreaterReward);
                SetText(screen.clock, build && !s.isGreaterReward ? s.selectionUnlimited ? ArenaText.Pair("NO TIME LIMIT","NO TIME LIMIT") : ArenaText.Text("ui.build.clock", Mathf.CeilToInt(s.buildTime).ToString("00")) : "");
                screen.clock.color = !s.selectionUnlimited && s.buildTime <= 5f ? ElementColors[0] : ElementColors[2];
            }
            if (screen.summary) SetText(screen.summary, s.buildSummary);
            if (screen.reroll) screen.reroll.gameObject.SetActive(!s.isGreaterReward);
            if (screen.startNext)
                SetText(screen.startNext.GetComponentInChildren<UnityEngine.UI.Text>(), s.isGreaterReward
                    ? ArenaText.Text("ui.return.reward") : s.state == ArenaUiState.LevelUp ? ArenaText.Pair("PICK LATER [ESC]","PICK LATER [ESC]") : ArenaText.Text("ui.nextwave"));
            ArenaUiCard[] cards = null;
            switch (s.state)
            {
                case ArenaUiState.Title:

                    SetText(screen.heading, "WARP ARENA");
                    SetText(screen.body, "");
                    break;
                case ArenaUiState.Loadout:
                    cards = s.loadoutChoices;
                    SetBody(screen.body, ArenaText.Pair("Pick one familiar form. Its attack and trait are unknown until you take it; the moment you choose, the game pauses and the weapon card appears.\nAim manually; use the terrain.","Pick one familiar form. Its attack and trait are unknown until you take it; the moment you choose, the game pauses and the weapon card appears.\nAim manually; use the terrain."));
                    break;
                case ArenaUiState.Armory:
                    cards = s.armoryOffers;
                    SetBody(screen.body, ArenaText.Pair("Pick one of three: a new weapon, or a higher Mk for one you hold (its attack, trait and upgrades stay). You may skip. A new weapon that replaces an old one leaves the old one's upgrades behind.","Pick one of three: a new weapon, or a higher Mk for one you hold (its attack, trait and upgrades stay). You may skip. A new weapon that replaces an old one leaves the old one's upgrades behind.") + PortalHint() + WalkHint());
                    break;
                case ArenaUiState.Draft:
                    cards = s.draftCards;
                    SetText(screen.heading, s.isGreaterReward ? ArenaText.Text("ui.greater.heading") : ArenaText.Text("ui.draft.counter", s.draftIndex, Mathf.Max(1, s.draftCount)));
                    SetBody(screen.body, s.isGreaterReward ? ArenaText.Pair("Pick one of three Greater Void skills (or none); they have no further levels. 1 / 2 / 3 select.","Pick one of three Greater Void skills (or none); they have no further levels. 1 / 2 / 3 select.")
                        : ArenaText.Pair("Three cards, one of each: a stat card, a weapon card (an upgrade direction a revealed weapon has not taken yet) and a skill card (a new skill, or a higher level for one you own; its colour shows the level it gives). 1 / 2 / 3 select; R rerolls all three (once per wave).","Three cards, one of each: a stat card, a weapon card (an upgrade direction a revealed weapon has not taken yet) and a skill card (a new skill, or a higher level for one you own; its colour shows the level it gives). 1 / 2 / 3 select; R rerolls all three (once per wave).") + WalkHint());
                    if (screen.reroll) screen.reroll.interactable = s.rerolls > 0;
                    SetText(screen.rerollLabel, ArenaText.Text("ui.reroll.counter", s.rerolls));
                    break;
                case ArenaUiState.LevelUp:
                    cards = s.draftCards;
                    SetText(screen.heading, ArenaText.Pair($"LEVEL UP · PICK ONE ({s.bankedDrafts} LEFT)",$"LEVEL UP · PICK ONE ({s.bankedDrafts} LEFT)"));
                    SetBody(screen.body, ArenaText.Pair("1 / 2 / 3 select; R rerolls (once per wave); Esc picks later, T opens this again in combat.","1 / 2 / 3 select; R rerolls (once per wave); Esc picks later, T opens this again in combat."));
                    if (screen.reroll) screen.reroll.interactable = s.rerolls > 0;
                    SetText(screen.rerollLabel, ArenaText.Text("ui.reroll.counter", s.rerolls));
                    break;
                case ArenaUiState.ReplaceWeapon:
                    RenderReplacement(screen, s.replacementSlots, s.replacementCount);
                    SetBody(screen.body, ArenaText.Pair("Choose the weapon to replace. Upgrades stay with the weapon: replace it and they are gone with it.","Choose the weapon to replace. Upgrades stay with the weapon: replace it and they are gone with it."));
                    break;
                case ArenaUiState.ReplaceSkill:
                    RenderReplacement(screen, s.skillSlots, 3);
                    SetBody(screen.body, ArenaText.Pair("With 3 skills already, taking a new one means replacing an old one; the new skill starts at the level this card gives.","With 3 skills already, taking a new one means replacing an old one; the new skill starts at the level this card gives."));
                    break;
                case ArenaUiState.Ready:
                    SetBody(screen.body, ArenaText.Pair("Build choices complete. Click START NEXT WAVE to go on.","Build choices complete. Click START NEXT WAVE to go on.") + PortalHint() + WalkHint());
                    break;
                case ArenaUiState.WeaponIntro:
                    SetText(screen.heading, s.weaponIntroTitle);
                    ArenaTextFit.Show(screen.body, ArenaEmphasis.Apply(s.weaponIntroBody), WeaponCardTextSize, weaponCardSmallest);
                    SetText(screen.summary, s.weaponIntroHint);
                    if (screen.continueLabel) SetText(screen.continueLabel, s.weaponIntroContinue);
                    if (screen.icon) SetWeaponIcon(screen.icon, art, s.weaponIntroForm);
                    break;
                case ArenaUiState.Paused:
                    SetText(screen.body, ArenaText.Pair("The battlefield and the build clock are paused.","The battlefield and the build clock are paused."));
                    if (pauseControls) pauseControls.SetFont(screen.body ? screen.body.font : null);
                    if (howToPlayLabel && screen.body && howToPlayLabel.font != screen.body.font) howToPlayLabel.font = screen.body.font;
                    break;
                case ArenaUiState.Result:
                    SetText(screen.heading, s.resultTitle);
                    SetText(screen.body, s.resultDetails);
                    break;
            }
            if (cards == null || screen.choices == null) return;
            for (int i = 0; i < screen.choices.Length; i++)
                screen.choices[i].Render(i < cards.Length ? cards[i] : null, art);
        }

        // GDD section 7, row for row: action, then input.
        public static readonly (string action, string input)[] Controls =
        {
            ("Move", "WASD"),
            ("Aim", "Mouse"),
            ("Use the held weapon", "Left mouse button (hold to charge or to keep firing, depending on the weapon)"),
            ("Swap the two hand slots", "Right mouse button"),
            ("Sprint", "Hold left Shift"),
            ("Cast a skill / select a card while a card panel is open", "1, 2, 3"),
            ("Open the banked card pick during combat / switch art mode on the title screen", "T"),
            ("Put the build panel away to walk the empty arena, and bring it back", "Tab"),
            ("Enter the void trial", "E while standing on the portal"),
            ("Start, open the crate, close the weapon card, start the next wave; pick later on a mid-wave card panel", "Space or Enter"),
            ("Pause / resume; pick later on a mid-wave card panel", "Esc"),
            ("Reroll while a card panel is open (once per wave) / restart on the result screen", "R"),
        };

        // The pause screen's table sits under the paused line; V5MenuSkin puts the buttons under PauseTableBottom.
        public const float PauseTableTop = 150f, PauseTableRow = 28f;
        public static float PauseTableBottom => PauseTableTop + Controls.Length * PauseTableRow;
        public static readonly string[] PauseButtons = { "ResumeRun", "RestartRun", "HowToPlay", "QuitToTitle" };
        V2ControlsTable pauseControls;
        UnityEngine.UI.Text howToPlayLabel;

        void PlaceControlsTables()
        {
            if (screens == null) return;
            foreach (var screen in screens)
            {
                if (!screen) continue;
                var content = screen.transform.Find("Content");
                if (!content) continue;
                if (screen.state == ArenaUiState.Paused)
                {
                    AddHowToPlayButton(content);
                    pauseControls = V2ControlsTable.Create(content, 1, PauseTableRow, 190f, 15);
                    var rect = pauseControls.Rect;
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(.5f, 1f);
                    rect.offsetMin = new Vector2(64f, -PauseTableBottom);
                    rect.offsetMax = new Vector2(-64f, -PauseTableTop);
                    // Without the menu skin (the Editor's Play button keeps the loaded scene): paused line, table, then the buttons in a row.
                    if (screen.body) PlaceTopLeft(screen.body.rectTransform, 44f, 100f, 1012f, 40f);
                    float left = 44f, top = PauseTableBottom + 24f;
                    foreach (var name in PauseButtons)
                    {
                        if (content.Find(name) is RectTransform button) PlaceTopLeft(button, left, top, 237f, 65f);
                        left += 255f;
                    }
                    if (screen.summary) PlaceTopLeft(screen.summary.rectTransform, 44f, top + 80f, 1012f, 60f);
                }
            }
        }

        static void PlaceTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        static string WalkHint() => ArenaText.Pair("\nTab puts this panel away so you can walk the empty arena; Tab again brings it back.","\nTab puts this panel away so you can walk the empty arena; Tab again brings it back.");

        static string PortalHint() => WorldMap.Active && WorldMap.Active.HasOpenPortal && !WorldMap.Active.TrialWon
            ? ArenaText.Pair(                "\nPortal open: stand on it and press E for the Void Trial (losing ends the run; entering clears statuses and shield).",                "\nPortal open: stand on it and press E for the Void Trial (losing ends the run; entering clears statuses and shield).") : "";

        void RenderReplacement(V2UiScreen screen, ArenaUiSlot[] slots, int count)
        {
            for (int i = 0; i < screen.choices.Length; i++)
            {
                var choice = screen.choices[i];
                bool valid = i < slots.Length && i < count;
                choice.gameObject.SetActive(valid);
                if (!valid) continue;
                SetText(choice.title, slots[i].title); choice.ShowBody(ArenaEmphasis.Apply(slots[i].detail));
                SetText(choice.kind, ArenaText.Text("ui.slot", i + 1)); SetText(choice.badge, ArenaTerms.Rarity(slots[i].rarity));
                choice.ApplyRarity(slots[i].rarity);
                choice.button.interactable = true;
                SetElementIcon(choice.icon, art, slots[i].elementIndex);
                SetWeaponIcon(choice.weaponIcon, art, slots[i].formIndex);
                SetContentIcon(choice.contentIcon, art, slots[i].skillIndex, -1, -1);
            }
        }

        void RenderPlayerStatuses(string fallback)
        {
            var gm = GameManager.Instance;
            var status = gm && gm.player ? ElementStatus.For(gm.player) : null;
            if (statusIcons == null || statusCounts == null || statusIcons.Length < 4 || statusCounts.Length < 4)
            { SetText(statusLabel, fallback); return; }
            for (int i = 0; i < 4; i++)
            {
                float count = status ? i switch { 0 => status.Fire, 1 => status.Poison, 2 => status.Frost, _ => status.Lightning } : 0f;
                bool locked = status && (i == 2 ? status.Frozen : i == 3 && status.Paralysed);
                bool show = count >= 1f || locked;
                SetElementIcon(statusIcons[i], art, show ? i : -1);
                if (statusCounts[i])
                {
                    statusCounts[i].gameObject.SetActive(show);
                    SetText(statusCounts[i], locked ? ArenaText.Text(i == 2 ? "ui.freeze.short" : "ui.paralysis.short") : Mathf.FloorToInt(count).ToString());
                }
            }
            SetText(statusLabel, status && status.Frozen ? ArenaText.Text("ui.frozen") : status && status.Paralysed ? ArenaText.Text("ui.paralysed") : "");
        }

        static void SetText(UnityEngine.UI.Text label, string value)
        {
            if (label && label.text != (value ?? "")) label.text = value ?? "";
        }

        static void SetBody(UnityEngine.UI.Text label, string value) => SetText(label, ArenaEmphasis.Apply(value));

        static void SetFill(UnityEngine.UI.Image image, float amount, float max)
        {
            if (image) image.fillAmount = max > 0f ? Mathf.Clamp01(amount / max) : 0f;
        }

        public static readonly Color[] ElementColors =
        {
            new(1f,.57f,.31f), new(.65f,.87f,.36f), new(.47f,.84f,.95f), new(1f,.84f,.36f),
            new(.80f,.65f,.51f), new(1f,.94f,.68f), new(.76f,.60f,1f)
        };

        public static void SetElementIcon(UnityEngine.UI.Image image, V2ArtSet set, int index)
        {
            if (!image) return;
            bool valid = set && set.elementIcons != null && index >= 0 && index < set.elementIcons.Length && set.elementIcons[index];
            image.gameObject.SetActive(valid);
            if (valid) { image.sprite = set.elementIcons[index]; image.color = Color.white; }
        }

        public static void SetContentIcon(UnityEngine.UI.Image image, V2ArtSet set, int skill, int stat, int axis)
        {
            if (!image) return;
            Sprite[] sprites = !set ? null : skill >= 0 ? set.skillIcons : stat >= 0 ? set.statIcons : axis >= 0 ? set.axisIcons : null;
            int index = skill >= 0 ? skill : stat >= 0 ? stat : axis;
            bool valid = sprites != null && index >= 0 && index < sprites.Length && sprites[index];
            image.gameObject.SetActive(valid);
            if (valid) { image.sprite = sprites[index]; image.color = Color.white; }
        }

        public static void SetWeaponIcon(UnityEngine.UI.Image image, V2ArtSet set, int index)
        {
            if (!image) return;
            bool valid = set && set.weaponSprites != null && index >= 0 && index < set.weaponSprites.Length && set.weaponSprites[index];
            image.gameObject.SetActive(valid);
            if (valid) { image.sprite = set.weaponSprites[index]; image.color = Color.white; }
        }
    }
}
