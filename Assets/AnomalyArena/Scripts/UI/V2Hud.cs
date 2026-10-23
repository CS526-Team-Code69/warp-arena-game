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

        void Awake()
        {
            source = modelSource as IArenaUiSource;
            if (localizedCanvas) localizedCanvas.enabled = ArenaText.Ready;
            int n = damageLabels?.Length ?? 0;
            damageWorld = new Vector3[n];
            damageAge = new float[n];
            damageColor = new Color[n];
            for (int i = 0; i < n; i++) { damageAge[i] = 1f; damageLabels[i].gameObject.SetActive(false); }
            if (themeBackground) themeBackground.texture = art ? art.themeBackground : null;
        }

        void Update()
        {
            if (localizedCanvas && localizedCanvas.enabled != ArenaText.Ready) localizedCanvas.enabled = ArenaText.Ready;
            if (!ArenaText.Ready) return;
            if (ArenaText.Font && appliedFont != ArenaText.Font)
            {
                appliedFont = ArenaText.Font;
                if (fontLabels != null) foreach (var label in fontLabels) if (label) label.font = appliedFont;
                nextRefresh = 0f;
            }
            float dt = Time.unscaledDeltaTime;
            transientTime = Mathf.Max(0f, transientTime - dt);
            flash = Mathf.Max(0f, flash - dt * 3f);
            if (damageFlash) damageFlash.color = new Color(1f, .1f, .04f, flash * .15f);
            TickDamage(dt);
                                                                                          
            if (lastSnapshot != null && lastSnapshot.cardPickSequence != seenPick) nextRefresh = 0f;
            if (source == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + Mathf.Max(.02f, refreshInterval);
            Render(source.UiSnapshot);
        }

        public void Dispatch(ArenaUiCommand command, int index = -1) => source?.ExecuteUiCommand(command, index);
        public void RefreshView(){if(source!=null&&ArenaText.Ready)Render(source.UiSnapshot);}

        public void ShowBanner(string message, float seconds = 2.2f)
        {
            transientText = message;
            transientTime = seconds;
        }

        public void FlashDamage() => flash = 1f;

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
            SetFill(shieldFill, s.shield, s.maxHp * .5f);
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
            ShowChargeWindow(s.chargeWindowStart, s.chargeWindowEnd);
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
                    ? ArenaText.Text("ui.return.reward") : s.state == ArenaUiState.LevelUp ? ArenaText.Pair("PICK LATER","PICK LATER") : ArenaText.Text("ui.nextwave"));
            ArenaUiCard[] cards = null;
            switch (s.state)
            {
                case ArenaUiState.Title:
                                                                                                                     
                    SetText(screen.heading, "WARP ARENA");
                    SetText(screen.body, "");
                    break;
                case ArenaUiState.Loadout:
                    cards = s.loadoutChoices;
                    SetText(screen.body, ArenaText.Pair("Pick one familiar form. Which backfire it has is unknown until you take it; the moment you choose, the game pauses and the weapon card appears.\nAim manually; use the terrain.","Pick one familiar form. Which backfire it has is unknown until you take it; the moment you choose, the game pauses and the weapon card appears.\nAim manually; use the terrain."));
                    break;
                case ArenaUiState.Armory:
                    cards = s.armoryOffers;
                    SetText(screen.body, ArenaText.Pair("Pick one of three: a new weapon, or a higher Mk of one you hold (weapons grow stronger through higher Mks from the armory). You may skip. Upgrades stay with the weapon: replace it and they are gone with it.","Pick one of three: a new weapon, or a higher Mk of one you hold (weapons grow stronger through higher Mks from the armory). You may skip. Upgrades stay with the weapon: replace it and they are gone with it.") + PortalHint() + WalkHint());
                    break;
                case ArenaUiState.Draft:
                    cards = s.draftCards;
                    SetText(screen.heading, s.isGreaterReward ? ArenaText.Text("ui.greater.heading") : ArenaText.Text("ui.draft.counter", s.draftIndex, Mathf.Max(1, s.draftCount)));
                    SetText(screen.body, s.isGreaterReward ? ArenaText.Pair("Pick one of three Greater Void skills (or none); they have no further levels. 1 / 2 / 3 select.","Pick one of three Greater Void skills (or none); they have no further levels. 1 / 2 / 3 select.")
                        : ArenaText.Pair("Three cards, one of each: a stat card, a weapon card (an upgrade direction a revealed weapon has not taken yet) and a skill card (a new skill, or one level for a skill you own). 1 / 2 / 3 select; R rerolls all three (once per wave).","Three cards, one of each: a stat card, a weapon card (an upgrade direction a revealed weapon has not taken yet) and a skill card (a new skill, or one level for a skill you own). 1 / 2 / 3 select; R rerolls all three (once per wave).") + WalkHint());
                    if (screen.reroll) screen.reroll.interactable = s.rerolls > 0;
                    SetText(screen.rerollLabel, ArenaText.Text("ui.reroll.counter", s.rerolls));
                    break;
                case ArenaUiState.LevelUp:
                    cards = s.draftCards;
                    SetText(screen.heading, ArenaText.Pair($"LEVEL UP · PICK ONE ({s.bankedDrafts} LEFT)",$"LEVEL UP · PICK ONE ({s.bankedDrafts} LEFT)"));
                    SetText(screen.body, ArenaText.Pair("1 / 2 / 3 select; R rerolls (once per wave); Esc or Space picks later, T opens this again in combat.","1 / 2 / 3 select; R rerolls (once per wave); Esc or Space picks later, T opens this again in combat."));
                    if (screen.reroll) screen.reroll.interactable = s.rerolls > 0;
                    SetText(screen.rerollLabel, ArenaText.Text("ui.reroll.counter", s.rerolls));
                    break;
                case ArenaUiState.ReplaceWeapon:
                    RenderReplacement(screen, s.replacementSlots, s.replacementCount);
                    SetText(screen.body, ArenaText.Pair("Choose the weapon to replace. Upgrades stay with the weapon: replace it and they are gone with it.","Choose the weapon to replace. Upgrades stay with the weapon: replace it and they are gone with it."));
                    break;
                case ArenaUiState.ReplaceSkill:
                    RenderReplacement(screen, s.skillSlots, 3);
                    SetText(screen.body, ArenaText.Pair("With 3 skills already, taking a new one means replacing an old one; the new skill starts at level 1.","With 3 skills already, taking a new one means replacing an old one; the new skill starts at level 1."));
                    break;
                case ArenaUiState.Ready:
                    SetText(screen.body, ArenaText.Pair("Build choices complete. Space starts the next wave.","Build choices complete. Space starts the next wave.") + PortalHint() + WalkHint());
                    break;
                case ArenaUiState.WeaponIntro:
                    SetText(screen.heading, s.weaponIntroTitle);
                    SetText(screen.body, s.weaponIntroBody);
                    SetText(screen.summary, s.weaponIntroHint);
                    if (screen.continueLabel) SetText(screen.continueLabel, s.weaponIntroContinue);
                    if (screen.icon) SetWeaponIcon(screen.icon, art, s.weaponIntroForm);
                    break;
                case ArenaUiState.Paused:
                    SetText(screen.body, ArenaText.Pair("The battlefield and the build clock are paused.","The battlefield and the build clock are paused."));
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

                                                                                                                                          
        static string WalkHint() => ArenaText.Pair("\nTab puts this panel away so you can walk the empty arena; standing on a portal and pressing E also enters the trial.","\nTab puts this panel away so you can walk the empty arena; standing on a portal and pressing E also enters the trial.");

                                                                                                                     
        static string PortalHint() => WorldMap.Active && WorldMap.Active.HasOpenPortal && !WorldMap.Active.TrialWon
            ? ArenaText.Pair("\nA portal is open: press E to enter the Void Trial (failure ends the run).","\nA portal is open: press E to enter the Void Trial (failure ends the run).") : "";

        void RenderReplacement(V2UiScreen screen, ArenaUiSlot[] slots, int count)
        {
            for (int i = 0; i < screen.choices.Length; i++)
            {
                var choice = screen.choices[i];
                bool valid = i < slots.Length && i < count;
                choice.gameObject.SetActive(valid);
                if (!valid) continue;
                SetText(choice.title, slots[i].title); SetText(choice.body, slots[i].detail);
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

        [Tooltip("Colour of the quick-reload window marked on the charge bar (Scatter Blast Tame).")]
        public Color chargeWindowColor = new(.55f, 1f, .75f, .55f);
        UnityEngine.UI.Image chargeWindow;

                                                                                                                                                 
        void ShowChargeWindow(float start, float end)
        {
            bool show = end > start && chargeFill && chargeFill.gameObject.activeSelf;
            if (!show)
            {
                if (chargeWindow && chargeWindow.gameObject.activeSelf) chargeWindow.gameObject.SetActive(false);
                return;
            }

            if (!chargeWindow)
            {
                chargeWindow = Instantiate(chargeFill, chargeFill.transform.parent);
                chargeWindow.name = "ChargeWindowMarker";
                chargeWindow.type = UnityEngine.UI.Image.Type.Simple;
                chargeWindow.raycastTarget = false;
            }

            chargeWindow.color = chargeWindowColor;
            var rect = (RectTransform)chargeWindow.transform;
            rect.anchorMin = new Vector2(Mathf.Clamp01(start), 0f);
            rect.anchorMax = new Vector2(Mathf.Clamp01(end), 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (!chargeWindow.gameObject.activeSelf) chargeWindow.gameObject.SetActive(true);
        }

                                                                                                              
        public UnityEngine.UI.Image ChargeWindowMarker => chargeWindow;

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
