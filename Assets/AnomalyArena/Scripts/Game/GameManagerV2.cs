using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AnomalyArena
{
    public partial class GameManager : IArenaUiSource
    {
        [Header("V2 components")]
        public RunWeapons runWeapons;
        public RunProgression progression;
        public SkillLoadout skills;
        /// <summary>The open mode's starting weapons, card by card: the rocket launcher, the rifle, the knife.</summary>
        public static WeaponForm LoadoutForm(int index) => index == 0 ? WeaponForm.RocketLauncher : index == 1 ? WeaponForm.Rifle : WeaponForm.Knife;
        public OrbPool orbs;
        public float buildTimeLeft;
        [Min(0f), Tooltip("武器与技能选择限时；0代表不限时，不自动处理卡牌。")]
        public float selectionTimeLimit;
        public ArenaUiState RunPhase { get; private set; }=ArenaUiState.Title;
        public event System.Action WeaponSelectionOpened;
        public bool SupplyDropReady { get; private set; }
        ArenaUiState pendingSupplyChoice;
        bool supplyChoiceOpened;
        public void MarkSupplyLanded(){if(RunPhase==ArenaUiState.SupplyDrop)SupplyDropReady=true;}
        /// <summary>Weapons, skills and the battlefield run. The campaign's night and plain afternoon count too: there is nothing to fight
        /// (the night's shadows cannot be hurt), but a held weapon that would not fire only looked broken.</summary>
        public bool IsCombatActive => State==GameState.Playing&&RunPhase is ArenaUiState.WaveActive or ArenaUiState.VoidTrial or ArenaUiState.FinalBoss or ArenaUiState.Night;
        public bool CanPlayerMove => State==GameState.Playing&&(RunPhase is ArenaUiState.WaveIntro or ArenaUiState.WaveActive or ArenaUiState.VoidTrial or ArenaUiState.FinalBoss or ArenaUiState.Night||BuildWalking||BuildCrateWalking);
        /// <summary>Build phase with the panel put away (Tab): the player walks the empty arena and may step onto an open portal.</summary>
        public bool BuildWalking => buildWalking&&RunPhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready;
        /// <summary>The build phase's supply crate (document 2.2: the build phase's time flows, only without enemies):
        /// while the crate comes down and waits above the player's head to be clicked open, the player walks the empty arena and may step
        /// onto an open portal. The crate before the first weapon (Loadout) still holds the run's start frozen.</summary>
        public bool BuildCrateWalking => State==GameState.Playing&&RunPhase==ArenaUiState.SupplyDrop&&pendingSupplyChoice is ArenaUiState.Armory or ArenaUiState.Draft;
        /// <summary>Build phase (document 2.2): time flows as in a wave, only the arena has no enemies, with a panel open
        /// (armory, cards, a replacement, ready) as while walking with it put away, and while its supply crate comes down.
        /// The weapon card, the greater void reward and a skill replacement opened from a mid-wave pick keep their freeze.
        /// A panel still keeps the player from moving, firing and casting (CanPlayerMove, CanUseCombatInput).</summary>
        public bool BuildTimeFlows => State==GameState.Playing&&!greaterDraft&&(RunPhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready or ArenaUiState.ReplaceWeapon||RunPhase==ArenaUiState.ReplaceSkill&&replaceReturn==ArenaUiState.Draft||BuildCrateWalking);
        public float WorldTimeScale => RunPhase==ArenaUiState.TrialReturn ? 0f : IsCombatActive && slipLeft > 0 ? slipScale : 1f;
        public float WorldDeltaTime => Time.deltaTime * WorldTimeScale;
        public float WorldFixedDeltaTime => Time.fixedDeltaTime * WorldTimeScale;
        public float PlayerDeltaTime => Time.deltaTime;
        public bool CanUseCombatInput => IsCombatActive && MenuInputReady && (!skills || !skills.ActionLocked);
        bool MenuInputReady => ArenaText.Ready && Time.unscaledTime - phaseOpenedAt >= .4f && menuMouseReleased;
        readonly ArenaUiSnapshot snapshot=new();
        ArenaUiState resumePhase;
        ArenaUiState trialResumePhase;
        float waveEndAt,menuCommandAt,slipLeft,slipScale=1;
        bool buildWalking;
        ArenaUiState cutsceneResumePhase;
        int pendingOffer=-1,pendingDraft=-1;
        int levelUpSeen;ArenaUiState replaceReturn=ArenaUiState.Draft; // banked drafts already shown this wave; where a skill replacement returns to
        bool greaterDraft;
        bool draftRerolled;
        bool menuMouseReleased;
        float phaseOpenedAt;
        int buildDraftCount;
        int eligibilityWave=-2;
        string buildSummary="";
        DraftCard[] greaterCards;

        void V2Start()
        {
            // The art's default is set in Awake (GameManager.cs), with the language SceneLocalePreset decided just before.
            runWeapons??=player.GetComponent<RunWeapons>();progression??=player.GetComponent<RunProgression>();skills??=player.GetComponent<SkillLoadout>();CampaignAwake();
            State=GameState.Title;RunPhase=ArenaUiState.Title;supplyChoiceOpened=false;SupplyDropReady=false;pendingSupplyChoice=ArenaUiState.Title;LockMenuInput();
            progression.ResetRun();skills.ResetRun();runWeapons.ResetRun();
            progression.HealingRequested+=player.Heal;
            player.DamageTaken+=(info,amount)=>RunAnalytics.PlayerDamaged(this,info,amount);
            player.DamageTaken+=OnPlayerDamagedForNotice; // the player's own weapon cost them HP: its slot flashes and a notice names it
            progression.StatChanged+=(_,__)=>ApplyStats();
            progression.LevelGained+=level=>hud?.Banner(ArenaText.Pair("升到 "+level+" 级", "LEVEL "+level),ArenaText.Pair($"最大生命 +{progression.LastLevelHpGain:0.#}，充能上限 +{progression.LastLevelEnergyGain:0.#}。", $"Max HP +{progression.LastLevelHpGain:0.#}, Max Charge +{progression.LastLevelEnergyGain:0.#}."));
            runWeapons.Revealed+=w=>hud?.Toast(w.Label,ElementCombat.ColorFor(w.element));
            skills.CastRefused+=(_,reason)=>hud?.Toast(reason,Color.yellow);
            skills.CastSucceeded+=(id,_,__,___)=>NoteSkillCast(id);
            skills.VoidWalkEnded+=runWeapons.VoidEnchantHands;
            waves.WaveCompleted+=OnV2WaveEnded;
            // V2 day campaign: kills give no XP, so no XP orb drops (OrbPool.Drop skips a zero); an elite's HP and energy orbs stay.
            waves.EnemyEliminated+=enemy=>{progression.GainDeathEnergy();if(enemy.SuppressRewards||enemy.IsTrialSummon||enemy.DeathCause==DeathCause.FellIntoGap)return;orbs?.Drop(enemy.Position,CampaignRunning?0f:waves.ExperienceFor(enemy),enemy.Elite&&Random.value<orbs.eliteHpDropChance,enemy.Elite&&progression.EnergyUnlocked&&Random.value<orbs.eliteEnergyDropChance);};
            if(WorldMap.Active){WorldMap.Active.VoidTrialEntered+=EnterVoidTrial;WorldMap.Active.VoidTrialWon+=CompleteVoidTrial;WorldMap.Active.HoleFed+=_=>V2Audio.Instance?.PlayVoidFeed();}
            SkillWorldAPI.TimeSlip=(factor,seconds)=>{slipLeft=seconds<=0f?0f:Mathf.Max(slipLeft,seconds);slipScale=seconds<=0f?1f:factor;};
            SkillWorldAPI.HoleProtection=null; // Blink protects its own two holes only, never every hole in the world.
            SkillWorldAPI.Teleport=(c,p)=>{c.CancelDisplacement();c.GetComponent<Rigidbody>().position=p;c.transform.position=p;};
            SkillWorldAPI.Pull=(c,to,d)=>{if(c is Enemy{Immovable:true})return;c.SetMovePosition(c.Position+Query.Flat(to-c.Position).normalized*Mathf.Min(d,Query.Flat(to-c.Position).magnitude));};
            SkillWorldAPI.IsOnScreen=p=>{Vector3 v=cam.WorldToViewportPoint(p);return v.z>0&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1;};
            SkillWorldAPI.ReflectProjectiles=(point,radius,c)=>{foreach(var bolt in waves.preparedBolts)if(bolt&&bolt.gameObject.activeInHierarchy&&Vector3.Distance(point,bolt.transform.position)<radius)bolt.Reflect(c);};
            DisableUiSubmit();
            ApplyStats();Time.timeScale=0;
        }
        void ApplyStats(){var s=progression.Stats;player.maxHp=s.maxHp;player.moveSpeed=s.moveSpeed;player.protectionTime=s.protection;player.stamina.sprintDuration=s.sprintDuration;player.stamina.recoverDuration=s.sprintRecovery;}
        void LockMenuInput(){phaseOpenedAt=Time.unscaledTime;menuMouseReleased=Mouse.current==null||!Mouse.current.leftButton.isPressed;}
        void SetPhase(ArenaUiState phase)
        {
            bool needsSupply=!supplyChoiceOpened&&(phase is ArenaUiState.Loadout or ArenaUiState.Armory || phase==ArenaUiState.Draft&&!greaterDraft&&HasWeaponChoice());
            if(needsSupply){pendingSupplyChoice=phase;SupplyDropReady=false;phase=ArenaUiState.SupplyDrop;}
            if(phase is not (ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready))buildWalking=false;
            RunPhase=phase;LockMenuInput();Time.timeScale=CanPlayerMove||BuildTimeFlows?1:0;
            if(phase==ArenaUiState.Paused)V2Audio.Instance?.PauseAll();else if(phase==ArenaUiState.Result)V2Audio.Instance?.StopAll();else if(CanPlayerMove||BuildTimeFlows)V2Audio.Instance?.ResumeAll();
            hud?.v2Hud?.RefreshView();
            if(needsSupply)WeaponSelectionOpened?.Invoke();
        }
        bool HasWeaponChoice()
        {
            foreach(var card in progression.CurrentCards)if(card!=null&&card.Kind==DraftCardKind.Weapon)return true;
            return false;
        }
        // ───── The open mode's map pick (the course asks for a level select) ─────

        /// <summary>The open mode's maps by the title's map pick (ArenaUiCommand.ChooseMap's index): the Dunes, the Ruins, any map in
        /// rotation. The open mode is the classic 15-wave run.</summary>
        public static readonly string[] OpenModeMaps = { Biome.Dunes, Biome.Ruins, null };
        // A map pick reloaded the scene for its map: the classic run starts there by itself, without showing the title again. The
        // GameManager that asked never takes it: its scene still runs for the click's sound before the reload (ReloadAfterUiFeedback), and
        // the first test run of 2026-11-10 caught it starting the run there, on the map it was leaving.
        static bool openModeStartPending;
        static GameManager openModeRequester;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOpenModeStart() { openModeStartPending = false; openModeRequester = null; }

#if UNITY_INCLUDE_TESTS
        /// <summary>Tests only: forgets a pick's pending start, so a test that stopped half way leaves the next scene on its title.</summary>
        public static void ForgetOpenModeStartForTests() => ResetOpenModeStart();
#endif

        /// <summary>The map is laid as the scene loads (WorldMap.Awake), so only a family other than the laid map's needs the scene again;
        /// a random pick keeps the map laid, which was picked at random as before.</summary>
        static bool NeedsMapReload(string family)
        {
            var world = WorldMap.Active;
            return !string.IsNullOrEmpty(family) && world && world.biome && !world.biome.InFamily(family);
        }

        /// <summary>After a map pick's reload: the run starts once the title's text is ready, so the title never shows (GameManager updates
        /// before the HUD draws). The HUD shows the first-time pages on arrival, as after the title's own button.</summary>
        void StartPendingOpenMode()
        {
            if(!openModeStartPending||ReferenceEquals(openModeRequester,this)||RunPhase!=ArenaUiState.Title||!ArenaText.Ready)return;
            openModeStartPending=false;openModeRequester=null;
            hud?.v2Hud?.ExpectTutorialOnArrival();
            SetPhase(ArenaUiState.Loadout);
        }

        void V2Update()
        {
            StartPendingContinue(); // a continued save's run starts here (GameManager.Save.cs)
            StartPendingOpenMode();
            var kb=Keyboard.current;
            if(Mouse.current==null||!Mouse.current.leftButton.isPressed)menuMouseReleased=true;
            // Only the keys the game shows do anything; Space and Enter close the weapon card, and Space is the void dash once the
            // void trial is won (document 2.2, GameManagerFlow.cs).
            if(kb!=null)ReadShownKeys(kb);
            if(RunPhase==ArenaUiState.WaveIntro&&waves.Phase==WavePhase.WaveActive)SetPhase(ArenaUiState.WaveActive);
            // A level gained during the wave opens its card pick at once; a pick put off with Esc or its button waits for T or the next level.
            if(RunPhase==ArenaUiState.WaveActive&&progression.BankedDrafts>levelUpSeen){levelUpSeen=progression.BankedDrafts;OpenLevelUp();}
            if(IsCombatActive)
            {
                // The run's clock counts the fights, not the campaign's night and plain afternoon (where weapons work too since 2026-11-10).
                if(RunPhase!=ArenaUiState.Night)Elapsed+=Time.deltaTime;
                progression.Tick(PlayerDeltaTime);ElementStatus.TickAll(WorldDeltaTime,PlayerDeltaTime);SkillLoadout.TickAll(PlayerDeltaTime);runWeapons.Tick(PlayerDeltaTime);orbs?.Tick(WorldDeltaTime);
                slipLeft=Mathf.Max(0,slipLeft-PlayerDeltaTime);if(eligibilityWave!=waves.WaveIndex)UpdatePenetrationEligibility();
            }
            else if(RunPhase==ArenaUiState.TrialReturn){TickTrialReturn();if(CampaignRunning)CampaignAfterTrialReturn();}
            else if(RunPhase==ArenaUiState.WaveEnd&&Time.unscaledTime>=waveEndAt)OpenBuild();
            else if(RunPhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready or ArenaUiState.ReplaceWeapon or ArenaUiState.ReplaceSkill||BuildCrateWalking)
            {if(BuildTimeFlows)TickBuildPhase();if(!greaterDraft&&selectionTimeLimit>0f){buildTimeLeft=Mathf.Max(0,buildTimeLeft-Time.unscaledDeltaTime);if(buildTimeLeft<=0){RunAnalytics.SupplyResolved(this,RunAnalytics.SupplyTimeout);BeginNextV2Wave(true);}}}
            TickVoidDashNotice(); // the void dash's notice once play is back after the win (GameManagerFlow.cs)
            Cursor.visible=true;
        }
        /// <summary>Build phase clock (BuildTimeFlows): the player's statuses, void energy and HP regeneration, and the skill and weapon timers
        /// run as in a wave. Nothing that belongs to combat does: the wave clock and spawns stay stopped (WaveManager waits for the next
        /// wave), and weapons only count their timers down (RunWeapons.TickTimers), so no auto weapon fires and nothing charges.</summary>
        void TickBuildPhase(){progression.Tick(PlayerDeltaTime);ElementStatus.TickAll(WorldDeltaTime,PlayerDeltaTime);SkillLoadout.TickAll(PlayerDeltaTime);runWeapons.TickTimers(PlayerDeltaTime);}
        void V2LateUpdate()
        {
            if(State!=GameState.Playing)return;
            // Resolve both deaths once at frame end; the agreed final-boss double KO is a win.
            if(finalWaveCleared){State=GameState.Won;RunEndedForSaves();RunAnalytics.RunEnded(this,RunPhase==ArenaUiState.Paused?resumePhase:RunPhase);ClearTransients();skills.ClearWaveEffects();SetPhase(ArenaUiState.Result);}
            else if(!player.IsAlive&&!(CampaignRunning&&campaign.CatchesPlayerDeath())||CampaignRunning&&campaign.BaseDestroyed){LoseCause=player.DeathCause;NoteDefeat(RunPhase==ArenaUiState.Paused?resumePhase:RunPhase);State=GameState.Lost;RunEndedForSaves();RunAnalytics.RunEnded(this,RunPhase==ArenaUiState.Paused?resumePhase:RunPhase);ClearTransients();skills.ClearWaveEffects();SetPhase(ArenaUiState.Result);}
        }
        void OnV2WaveEnded(int wave)
        {
            if(!player.IsAlive)return;
            RunAnalytics.FlushDamage(this);
            // Document 11.3: pickups left on the ground vanish at the end of the wave instead of flying to the player.
            player.ClearWaveState();skills.ClearWaveEffects();runWeapons.ClearWaveEffects();orbs?.Clear();
            SetPhase(ArenaUiState.WaveEnd);waveEndAt=Time.unscaledTime+1f;
        }
        // V2 day campaign: a wave's end opens the lull or the dusk instead (V2Campaign.InterceptBuild); only the dawn opens this build.
        void OpenBuild(){if(CampaignRunning&&campaign.InterceptBuild())return;supplyChoiceOpened=false;buildTimeLeft=Mathf.Max(0f,selectionTimeLimit);buildDraftCount=progression.BankedDrafts;buildSummary="";UpdatePenetrationEligibility();runWeapons.RollArmory(waves.WaveIndex+2);RunAnalytics.SupplyOpened(runWeapons.Offers);progression.BeginBuildPhase(waves.WaveIndex+1,WeaponCard,SkillCard);if(CampaignSkipsArmory){OpenDraft();return;}SetPhase(ArenaUiState.Armory);}
        // V2 day campaign: the dawn has a second air drop (a day holds two waves), and a pick whose card kinds no room unlocks yet waits.
        void OpenDraft(){supplyChoiceOpened=false;UpdatePenetrationEligibility();pendingDraft=-1;pendingOffer=-1;if(CampaignRunning&&campaign.TakeArmoryRound()){runWeapons.RollArmory(waves.WaveIndex+2);if(!CampaignSkipsArmory){SetPhase(ArenaUiState.Armory);return;}}if(progression.BankedDrafts>0){DrawDraft();if(progression.CurrentCards.Length>0){SetPhase(ArenaUiState.Draft);return;}}SetPhase(ArenaUiState.Ready);}
        /// <summary>Mid-wave card pick: freeze the world and show the banked draft. Cards already drawn come back unchanged when the pick was put off (here or in the build phase), so putting it off is not a free reroll.</summary>
        void OpenLevelUp(){if(progression.BankedDrafts==0)return;pendingDraft=-1;DrawDraft();if(progression.CurrentCards.Length==0)return;SetPhase(ArenaUiState.LevelUp);}
        void DrawDraft()=>progression.DrawCards(waves.WaveIndex+1,WeaponCard,SkillCard);
        /// <summary>Draft card factories. replacing is the card a reroll swaps out (or one the build has outgrown); it is offered again only when nothing else can be.</summary>
        DraftCard WeaponCard(DraftCard replacing)=>runWeapons.RollUpgradeCard(replacing?.Payload as WeaponUpgradeOffer,waves.WaveIndex+1);
        DraftCard SkillCard(DraftCard replacing)=>CampaignRunning&&!campaign.SkillCards?null:skills.RollCard(waves.WaveIndex+1,replacing?.Payload is SkillId id?id:(SkillId?)null);
        /// <summary>Document 9.4: one reroll per wave, shared by its mid-wave picks and its build phase. Reset only when a wave really starts,
        /// so resuming the wave intro or the final boss from a pause or a cutscene grants no fresh reroll.</summary>
        void BeginWaveDrafts(){progression.BeginWave();levelUpSeen=0;draftRerolled=false;}
        void CloseLevelUp(){levelUpSeen=progression.BankedDrafts;SetPhase(ArenaUiState.WaveActive);}
        // Document 2.2 (the 10-27 doc): a mid-wave pick ends with one card; a pick still banked waits for T (the HUD says so) or the next level.
        void AfterLevelUpPick(){levelUpSeen=progression.BankedDrafts;SetPhase(ArenaUiState.WaveActive);}
        void BeginNextV2Wave(bool automatic)
        {
            RunAnalytics.SupplyResolved(this,RunAnalytics.SupplySkip);
            // V2 day campaign: no card picks; the summary counts the upgrades really applied (a pick no unlocked kind can fill stays).
            if(automatic){int count=progression.BankedDrafts;progression.ResolvePendingRandom();int applied=count-progression.BankedDrafts;
                if(CampaignRunning){if(applied>0)buildSummary+=ArenaText.Pair($"\n自动完成的升级：{applied} 次", $"\nUpgrades applied automatically: {applied}");}
                else if(count>0)buildSummary+=ArenaText.Pair($"\n{count} 组待选卡已随机替你选好", $"\n{count} pending card picks were made for you at random");}
            snapshot.earlyStartConfirmation=false;pendingDraft=pendingOffer=-1;
            player.Heal(player.maxHp);player.stamina.Refill();player.ClearWaveState();BeginWaveDrafts();
            if(!CampaignRunning)SaveOpenRun(); // the open mode saves just before every wave and the Tyrant (GameManager.Save.cs)
            if(CampaignRunning){SetPhase(ArenaUiState.WaveIntro);campaign.StartNextWave();V2Audio.Instance?.PlayWave();}
            else if(waves.WaveIndex+1>=waves.WaveCount){SetPhase(ArenaUiState.FinalBoss);waves.BeginFinalBoss();}
            else{SetPhase(ArenaUiState.WaveIntro);waves.BeginNextWave();V2Audio.Instance?.PlayWave();}
        }
        /// <summary>World freeze for a scripted moment the player only watches (first black hole seen); nothing the player presses ends it
        /// early (document 3.2), it hands play back by itself.</summary>
        public void BeginCutscene(){if(RunPhase==ArenaUiState.Cutscene)return;cutsceneResumePhase=RunPhase;SetPhase(ArenaUiState.Cutscene);}
        public void EndCutscene(){if(RunPhase!=ArenaUiState.Cutscene)return;SetPhase(cutsceneResumePhase);}
        // Entered from the build phase's supply crate, the trial comes back to the panel that crate was bringing, so the crate comes
        // down again (SetPhase) and the build goes on as entered from that panel.
        public void EnterVoidTrial(){trialResumePhase=RunPhase==ArenaUiState.SupplyDrop?pendingSupplyChoice:RunPhase;SetPhase(ArenaUiState.VoidTrial);skills.FreezeBattlefieldForTrial(true);runWeapons.FreezeBattlefieldForTrial(true);orbs?.FreezeForTrial(true);}
        public void CompleteVoidTrial(){if(!player.IsAlive){if(CampaignRunning)CampaignTrialEndsUnrewarded();return;} // the Hollow can die in the same frame as the player: the run is lost, not rewarded
            voidDashNoticePending=true; // a win gives the void dash, which says so once play is back (GameManagerFlow.cs)
            // V2 day campaign (since 2026-11-10): the win pays the void currencies instead (V2Campaign.Void.cs, on WorldMap.VoidTrialWon), so
            // it gives no void energy regeneration, no extra level and no greater void pick; the player is healed and play returns as it
            // does after that pick.
            if(CampaignRunning){UpdatePenetrationEligibility();player.Heal(player.maxHp);player.stamina.Refill();skills.FreezeBattlefieldForTrial(false);runWeapons.FreezeBattlefieldForTrial(false);orbs?.FreezeForTrial(false);FinishGreaterReward();return;}
            progression.CompleteVoidTrial();
            // A real win (the enemy lab's never sets TrialWon) is also one extra level beside the XP path. Its pick
            // waits behind the greater reward: in the build phase it joins the drafts, mid-wave it pops up once the wave resumes.
            if(WorldMap.Active&&WorldMap.Active.TrialWon){progression.GrantVoidLevel();if(trialResumePhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready)buildDraftCount++;}
            UpdatePenetrationEligibility();player.Heal(player.maxHp);player.stamina.Refill();skills.FreezeBattlefieldForTrial(false);runWeapons.FreezeBattlefieldForTrial(false);orbs?.FreezeForTrial(false);greaterCards=skills.GreaterVoidCards();greaterDraft=true;SetPhase(ArenaUiState.Draft);}
        // This reward returns to the suspended wave; it never advances the normal build sequence.
        void FinishGreaterReward()
        {
            greaterDraft=false;greaterCards=null;pendingDraft=pendingOffer=-1;
            snapshot.earlyStartConfirmation=false;UpdatePenetrationEligibility();
            // Document 12.5: entered from the build phase, the run returns to it; entered mid-wave, the battlefield and the player hold still
            // for a moment while the player absorbs the void's power (BeginTrialReturn, GameManagerFlow.cs).
            // A finished build reopens the drafts for the trial's extra pick; an open draft comes back with its cards.
            if(trialResumePhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready)WorldMap.Active?.RemoveHolesAfterTrial();
            if(trialResumePhase==ArenaUiState.Ready&&progression.BankedDrafts>0){OpenDraft();return;}
            if(trialResumePhase==ArenaUiState.Draft)DrawDraft();
            if(trialResumePhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready){SetPhase(trialResumePhase);return;}
            if(CampaignRunning)CampaignSettleVoidLevel();
            BeginTrialReturn();
            if(CampaignRunning)CampaignAfterTrialReturn();
        }

        // ───── V2 day campaign: the void trial behind the black hole's hidden gate (since 2026-11-06) ─────
        // The campaign enters the trial by day (the wave's phase, WaveActive) or in the afternoon (Night, or WaveActive while the afternoon
        // holds a fight). The reward is the classic one; only the way back is the campaign's: the phase it came from returns, and a fall
        // in the afternoon ends the trial without a win so the campaign's death rule can wake the player at the base.

        /// <summary>The phase a campaign trial hands back: the wave by day, the afternoon as it was, the night at night.</summary>
        ArenaUiState CampaignTrialResumePhase=>campaign.Stage switch
        {
            CampaignStage.Day or CampaignStage.Lull=>ArenaUiState.WaveActive,
            CampaignStage.Dusk=>trialResumePhase==ArenaUiState.WaveActive?ArenaUiState.WaveActive:ArenaUiState.Night,
            _=>ArenaUiState.Night,
        };

        /// <summary>After the frozen moment of the return (TickTrialReturn hands play back as a wave): the campaign's own phase comes back.</summary>
        void CampaignAfterTrialReturn()
        {
            if(RunPhase!=ArenaUiState.WaveActive)return;
            var back=CampaignTrialResumePhase;
            if(back!=ArenaUiState.WaveActive)SetPhase(back);
        }

        /// <summary>The trial's extra level banks one pick like a level-up (RunProgression.GrantVoidLevel); it opens once the fight goes
        /// on, as in the classic run. While no room sells any kind of upgrade nothing can be offered, and the pick is taken back so none
        /// waits that no key opens (the level's Max HP and Max Energy stay).</summary>
        void CampaignSettleVoidLevel()
        {
            if(progression.BankedDrafts<=0)return;
            DrawDraft();
            if(progression.CurrentCards.Length==0)progression.ConsumeDraft();
        }

        /// <summary>The Nash Prophet fell in the frame the player did: no reward, the gate closes as after a win, the battlefield thaws and
        /// the campaign's phase comes back (its death rule, or the lost run by day, takes over).</summary>
        void CampaignTrialEndsUnrewarded()
        {
            skills.FreezeBattlefieldForTrial(false);runWeapons.FreezeBattlefieldForTrial(false);orbs?.FreezeForTrial(false);
            WorldMap.Active?.RemoveHolesAfterTrial();
            if(RunPhase==ArenaUiState.VoidTrial)SetPhase(CampaignTrialResumePhase);
        }

        /// <summary>V2 day campaign: the player fell in the void trial in the afternoon, a death the campaign catches. The trial ends
        /// without a win (WorldMap.AbortTrial: no reward, the gate stays open), the battlefield thaws and the afternoon's phase comes back;
        /// the campaign's death rule then wakes the player at the base.</summary>
        internal void CampaignAbortVoidTrial()
        {
            var world=WorldMap.Active;
            if(!CampaignRunning||!world||!world.TrialActive)return;
            world.AbortTrial();
            skills.FreezeBattlefieldForTrial(false);runWeapons.FreezeBattlefieldForTrial(false);orbs?.FreezeForTrial(false);
            if(RunPhase==ArenaUiState.VoidTrial)SetPhase(CampaignTrialResumePhase);
        }
        // V2 day campaign: the Tyrant drops the dark matter core first; the ending then wins the run (CampaignWin).
        public void NotifyFinalBossDefeated(){if(CampaignRunning){campaign.OnTyrantDefeated(waves.tyrant?waves.tyrant.Position:player.Position);return;}finalWaveCleared=true;}

        // ───── Weapon card: the moment a weapon is taken, the run pauses on what it does ─────
        RunWeapon introWeapon;
        System.Action afterIntro;
        /// <summary>Reveals the weapon, pauses on its card, and runs then once the player dismisses it.</summary>
        void ShowWeaponIntro(RunWeapon weapon,System.Action then)
        {
            if(weapon==null){then?.Invoke();return;}
            // Read before the reveal records it: a trait the player has never seen holds its card a moment longer.
            introHold=WeaponCardHold(weapon.IsAnomalous&&!ArenaCodexProgress.AnomalyRevealed(weapon.form,weapon.variant));
            runWeapons.RevealNow(weapon);introWeapon=weapon;afterIntro=then;SetPhase(ArenaUiState.WeaponIntro);
        }
        /// <summary>Marks a taken card so the view plays its flourish; call it before the next screen opens.</summary>
        void NotifyCardPicked(ArenaUiState from,int index,CardRarity rarity){snapshot.cardPickSequence++;snapshot.cardPickState=from;snapshot.cardPickIndex=index;snapshot.cardPickRarity=rarity;}
        void DismissWeaponIntro()
        {
            if(RunPhase!=ArenaUiState.WeaponIntro||IntroHeld)return;
            var next=afterIntro;afterIntro=null;introWeapon=null;next?.Invoke();
        }
        void FillWeaponIntro()
        {
            var w=introWeapon;if(w==null)return;
            snapshot.weaponIntroTitle=w.Label;
            // The card opens with its lead (WeaponCardLead), then how the weapon attacks. It has no damage section (hit damage, use
            // interval); the Drain's shield wait is not damage, so it stays and follows the attack's description.
            string lead=WeaponCardLead(w.IsAuto,w.element,CampaignRunning&&w.IsAnomalous,BuildEdition.Current);
            // The trait's heading says what kind of cost it is: one that can cost HP, one that only moves the player, or neither.
            string traitHead=WeaponGuide.HurtsYou(w.form,w.variant)?ArenaText.Pair("特性 · 会伤到你","Trait · can hurt you")
                :WeaponGuide.MovesYou(w.form,w.variant)?ArenaText.Pair("特性 · 会推动你","Trait · moves you"):ArenaText.Pair("特性","Trait");
            string trait=w.IsAnomalous?$"\n\n<b><color=#ff8a73>{traitHead}</color></b>\n{WeaponGuide.Backfire(w.form,w.variant)}":"";
            float wait=w.form==WeaponForm.ShieldRing&&w.variant==1?runWeapons.DrainShieldWait(w):0f;
            string drainWait=wait>0f?"\n"+ArenaText.Pair($"连续 {wait:0.#} 秒没受到敌人攻击，就会生成能量盾。",$"The Energy Shield forms after {wait:0.#} s without an enemy hit."):"";
            snapshot.weaponIntroBody=$"{lead}<b><color=#ffd36e>{ArenaText.Pair("攻击方式","How it attacks")}</color></b>\n{WeaponGuide.How(w.form,w.variant)}{drainWait}{trait}";
            // While a new trait's card holds, the button is dim and the line under the card says why nothing closes it yet.
            bool held=NewTraitHeld;
            snapshot.weaponIntroHint=held?ArenaText.Pair("这把武器的特性你还没见过，读完再继续。","You have not seen this trait before. Read it, then continue.")
                :ArenaText.Pair("点击任意处、按空格或回车继续","Click anywhere or press Space or Enter to continue");
            string continueWord=ArenaText.Pair("继续","CONTINUE");
            snapshot.weaponIntroContinue=held?$"<color=#f2faff59>{continueWord}</color>":continueWord;
            snapshot.weaponIntroForm=(int)w.form;
        }
        /// <summary>The lines a weapon card opens with, a blank line after them: on the English Alpha, whether the weapon is held or
        /// fires by itself and its element ("none" included); in the story, for an anomaly weapon, the note that it is one (the pistol is
        /// none and has no trait). Elsewhere the slots and the collection already tell hand or auto and the element, so the card starts
        /// with how the weapon attacks.</summary>
        public static string WeaponCardLead(bool auto,ElementKind element,bool anomalyNote,BuildEdition.Edition edition)
        {
            var lines=new System.Collections.Generic.List<string>(2);
            if(edition==BuildEdition.Edition.EnglishAlpha)
                lines.Add($"<color=#9ad8ff>{(auto?ArenaText.Pair("自动武器","Auto weapon"):ArenaText.Pair("手持武器","Hand weapon"))}</color>{ArenaText.Pair("　属性：","   Element: ")}{ArenaTerms.Element(element)}");
            if(anomalyNote)lines.Add($"<color=#c9a6ff>{ArenaText.Pair("异常武器：攻击方式和特性都与众不同，多用几次才能摸清它的脾气。","Anomaly weapon: its attack and trait are out of the ordinary. Use it a few times to get a feel for it.")}</color>");
            return lines.Count>0?string.Join("\n",lines)+"\n\n":"";
        }

        // ───── A trait never seen before holds its card ─────

        /// <summary>Seconds a weapon card stays before it can be closed: NewTraitCardSeconds when its trait is one the player has never
        /// seen, so the card is read and not clicked away; every other card keeps the usual input lock (CardLockSeconds).</summary>
        public const float NewTraitCardSeconds=2f,CardLockSeconds=.4f;
        /// <summary>Tests: every weapon card keeps the usual lock (the tests' set-up fixture turns this on), as DifficultyScales.Neutral
        /// does for the difficulty scales.</summary>
        public static bool ShortWeaponCardHold;
        public static float WeaponCardHold(bool newTrait)=>newTrait&&!ShortWeaponCardHold?NewTraitCardSeconds:CardLockSeconds;
        float introHold=CardLockSeconds;
        bool IntroHeld=>RunPhase==ArenaUiState.WeaponIntro&&Time.unscaledTime-phaseOpenedAt<introHold;
        /// <summary>The card of a new trait is still holding (not the usual short lock every card has).</summary>
        public bool NewTraitHeld=>introHold>CardLockSeconds&&IntroHeld;

        // ───── The player's own weapon cost them HP: said at the hit ─────

        /// <summary>The notice names a weapon this many times a run at most (each weapon kind on its own); its slot flashes every time.</summary>
        public const int SelfHitNoticesPerWeapon=2;
        public const float SelfHitNoticeSeconds=4.5f;
        readonly System.Collections.Generic.Dictionary<(WeaponForm form,int variant),int> selfHitNotices=new();
        (WeaponForm form,int variant) selfHitShownKey;
        float selfHitShownUntil=-1f;
        bool selfHitShownWall;
        // The last damage to the player: whether their own weapon dealt it, and which (null when unknown), for the result screen.
        bool lastHurtSelf;
        RunWeapon lastHurtWeapon;

        /// <summary>How many notices this run has shown for a weapon kind (tests).</summary>
        public int SelfHitNoticesShown(WeaponForm form,int variant)=>selfHitNotices.TryGetValue((form,variant==0?0:1),out int shown)?shown:0;

        /// <summary>Any damage that cost the player HP or Shield. When their own weapon dealt it (a hit or a burn whose dealer is the
        /// player, or a wall slam their own knock or flight started), the weapon's slot flashes red every time, and the first
        /// SelfHitNoticesPerWeapon times a run for each weapon a notice names it with one plain line (WeaponGuide.SelfHit). A wall slam the
        /// same blast's knock ends in a moment later turns the notice shown into the wall's and does not count again.</summary>
        void OnPlayerDamagedForNotice(DamageInfo info,float amount)
        {
            bool self=player&&info.By==player;
            var weapon=self?SelfHitWeapon(info):null;
            lastHurtSelf=self;lastHurtWeapon=weapon;
            if(!self||amount<=0f)return;
            bool wall=info.source==DamageSource.Wall;
            NoteSelfHit(weapon);
            var key=weapon!=null?(weapon.form,weapon.variant==0?0:1):((WeaponForm)(-1),0);
            float now=Time.unscaledTime;
            if(now<selfHitShownUntil&&selfHitShownKey==key)
            {
                if(wall&&!selfHitShownWall){selfHitShownWall=true;selfHitShownUntil=now+SelfHitNoticeSeconds;ShowSelfHitNotice(weapon,true);}
                return;
            }
            selfHitNotices.TryGetValue(key,out int shown);
            if(shown>=SelfHitNoticesPerWeapon)return;
            selfHitNotices[key]=shown+1;
            selfHitShownKey=key;selfHitShownUntil=now+SelfHitNoticeSeconds;selfHitShownWall=wall;
            ShowSelfHitNotice(weapon,wall);
        }

        /// <summary>Which of the player's weapons dealt them this damage: the hit's own weapon, the one whose knock or flight ended in the
        /// wall (Combatant.LastDamageWeapon), or for a burn or poison tick the one that put the status on them (RunWeapons.SelfStatusWeapon).</summary>
        RunWeapon SelfHitWeapon(DamageInfo info)
        {
            if(info.weapon!=null)return info.weapon;
            if(player.LastDamageWeapon!=null)return player.LastDamageWeapon;
            bool dot=info.source is DamageSource.Burn or DamageSource.Poison;
            return dot&&runWeapons?runWeapons.SelfStatusWeapon:null;
        }

        /// <summary>The slot holding this weapon flashes red (ArenaUiSlot.selfHits, read by the dock's slot every frame).</summary>
        void NoteSelfHit(RunWeapon weapon)
        {
            if(!runWeapons||weapon==null)return;
            for(int i=0;i<runWeapons.Hand.Length&&i<snapshot.handSlots.Length;i++)if(runWeapons.Hand[i]==weapon)snapshot.handSlots[i].selfHits++;
            for(int i=0;i<runWeapons.Auto.Length&&i<snapshot.autoSlots.Length;i++)if(runWeapons.Auto[i]==weapon)snapshot.autoSlots[i].selfHits++;
        }

        void ShowSelfHitNotice(RunWeapon weapon,bool wall)
        {
            string name=weapon!=null?weapon.FormName:ArenaText.Pair("武器","weapon");
            string title=wall?ArenaText.Pair($"被自己的{name}推去撞了墙",$"YOUR OWN {name.ToUpperInvariant()} THREW YOU INTO A WALL")
                :ArenaText.Pair($"被自己的{name}伤到了",$"YOUR OWN {name.ToUpperInvariant()} HIT YOU");
            hud?.v2Hud?.ShowBanner(title+"\n"+WeaponGuide.SelfHit(weapon?.form,weapon?.variant??0,wall),SelfHitNoticeSeconds);
        }

        // ───── What ended the run: the result screen's first line ─────

        string defeatCause="";
        EnemyKind? defeatEnemy;
        DamageSource defeatSource;
        RunWeapon defeatWeapon;
        bool defeatBase, defeatOwnVoid;

        /// <summary>Keeps what ended a lost run at the moment it ends, in RunAnalytics' causes (the same the run_end row sends).</summary>
        void NoteDefeat(ArenaUiState phase)
        {
            var by=player.LastDamagedBy;var enemy=by as Enemy;bool known=enemy&&enemy.definition;
            defeatEnemy=known?enemy.definition.kind:(EnemyKind?)null;
            defeatSource=player.LastDamageSource;
            defeatCause=RunAnalytics.DefeatCause(phase,player.DeathCause,defeatSource,by&&by==player,defeatEnemy,known&&enemy.Elite).cause;
            defeatWeapon=lastHurtSelf?lastHurtWeapon:null;
            defeatBase=CampaignRunning&&campaign.BaseDestroyed;
            defeatOwnVoid=defeatCause=="black_hole"&&WorldMap.Active&&WorldMap.Active.PlayerInOwnVoid;
        }

        /// <summary>The result screen's first line for a lost run, in both modes: what ended it and, where the player can learn from it,
        /// how to keep clear of it. Empty for a won run and for a cause the game cannot name.</summary>
        public string ResultCauseLine()=>State==GameState.Lost
            ?ResultCauseLine(defeatCause,defeatEnemy,defeatSource,defeatWeapon?.form,defeatWeapon?.variant??0,defeatSource==DamageSource.Wall,defeatBase,defeatOwnVoid):"";

        /// <summary>The line for one cause, by RunAnalytics.DefeatCause's names: the base, the Void Trial, the black hole, the player's own
        /// weapon (a wall slam it started included), a wall slam an enemy started, an enemy's burn or poison, the Tyrant King, any other
        /// enemy (with the collection's note on it). ownVoid: the black hole that took the player was their own Pocket Void, whose pull is
        /// slower than a walk.</summary>
        public static string ResultCauseLine(string cause,EnemyKind? enemy,DamageSource source,WeaponForm? weapon,int variant,bool wall,bool baseDestroyed,bool ownVoid=false)
        {
            // The cause on the first line and what to know about it on the next: one long line left its last mark alone on a line.
            const string gap="\n";
            if(baseDestroyed)return ArenaText.Pair("怪物拆毁了基地。上午要先清掉正在攻击基地的怪物。","The monsters tore the base down. In the morning, clear the ones attacking the base first.");
            switch(cause)
            {
                case "void_trial":return ArenaText.Pair("你倒在了虚空试炼里。","You fell in the Void Trial.");
                case "black_hole" when ownVoid:
                    return ArenaText.Pair($"你掉进了自己开的口袋虚空。{gap}它的引力比走路慢，被拉住时朝外走就能脱身；别把它开在自己脚边。",
                        $"Your own Pocket Void swallowed you.{gap}Its pull is slower than your walk, so walk outward when it catches you, and don't open it at your feet.");
                case "black_hole":
                {
                    string key=ArenaKeys.SprintKey;
                    return ArenaText.Pair($"你掉进了黑洞。{gap}它的引力比走路快，被拉住时要按住 {key} 朝外冲刺。",$"You fell into the black hole.{gap}Its pull is faster than your walk; when it catches you, hold {key} and sprint outward.");
                }
                case "backfire":
                {
                    string name=weapon.HasValue?ArenaTerms.WeaponForm(weapon.Value):ArenaText.Pair("武器","weapon");
                    string what=wall?ArenaText.Pair($"你被自己的{name}推到墙上，撞倒了。",$"A wall ended this run: your own {name} threw you into it.")
                        :ArenaText.Pair($"你被自己的{name}打倒了。",$"Your own {name} ended this run.");
                    return what+gap+WeaponGuide.SelfHit(weapon,variant,wall);
                }
                case "wall_slam":
                    return enemy.HasValue?ArenaText.Pair($"{MonsterName(enemy.Value)}把你撞到墙上，你倒下了。",$"A wall ended this run: {AMonster(enemy.Value)} knocked you into it.")
                        :ArenaText.Pair("你撞到墙上，倒下了。","A wall ended this run.");
                case "status":
                {
                    string status=source==DamageSource.Burn?ArenaText.Pair("燃烧","burn"):ArenaText.Pair("中毒","poison");
                    return enemy.HasValue?ArenaText.Pair($"{MonsterName(enemy.Value)}留在你身上的{status}让你倒下了。",$"The {status} {AMonster(enemy.Value)} left on you ended this run.")
                        :ArenaText.Pair($"你身上的{status}让你倒下了。",$"The {status} on you ended this run.");
                }
                case "tyrant":return ArenaText.Pair("你被暴虐君王打倒了。","The Tyrant King ended this run.");
                case "enemy" when enemy.HasValue:
                {
                    string note=ArenaCodex.MonsterNote(enemy.Value);
                    string what=ArenaText.Pair($"你被{MonsterName(enemy.Value)}打倒了。",$"{Capital(AMonster(enemy.Value))} ended this run.");
                    return string.IsNullOrEmpty(note)?what:what+gap+note;
                }
                default:return "";
            }
        }

        /// <summary>A monster's name as the collection gives it; the two bosses by their titles.</summary>
        static string MonsterName(EnemyKind kind)=>kind switch
        {
            EnemyKind.Tyrant=>ArenaText.Pair("暴虐君王","The Tyrant King"),
            EnemyKind.Hollow=>ArenaText.Pair("纳什先知","The Nash Prophet"),
            _=>ArenaText.Text("enemy."+kind),
        };

        /// <summary>The name inside an English sentence, with its article ("a Grunt", "an ..."); a boss's title and a Chinese name as they are.</summary>
        static string AMonster(EnemyKind kind)
        {
            string name=MonsterName(kind);
            if(ArenaText.IsChinese||kind is EnemyKind.Tyrant or EnemyKind.Hollow||name.Length==0)return name;
            return ("AEIOU".IndexOf(char.ToUpperInvariant(name[0]))>=0?"an ":"a ")+name;
        }

        static string Capital(string text)=>string.IsNullOrEmpty(text)?text:char.ToUpperInvariant(text[0])+text.Substring(1);

        // ───── The black hole's pull: the hint plate names the key that gets the player out ─────

        /// <summary>The player stands in a black hole's pull outside the void trial; exhausted: their stamina is spent, so they walk out first.</summary>
        bool InHolePull(out bool exhausted)
        {
            exhausted=false;
            var world=WorldMap.Active;
            if(!world||!player||!player.IsAlive||!CanPlayerMove||RunPhase==ArenaUiState.VoidTrial||world.TrialActive)return false;
            if(world.GetPull(player.Position,player).sqrMagnitude<.01f)return false;
            // The story's first pull of a run is taught by its own spotlight with the same key; the plate does not say it twice.
            if(CampaignRunning&&campaign&&campaign.TeachingHoleEscape)return false;
            exhausted=player.stamina.Exhausted;
            return true;
        }

        void UpdatePenetrationEligibility()
        {
            bool anyImmune=false, anyElement=false, allImmune=true;
            bool voidSkill=false;
            eligibilityWave=waves.WaveIndex;
            // Documents 7.6 and 9.5: what counts is an element both bosses ignore (Frost, Lightning), carried by something that deals
            // that element's damage: Damage Penetration lets only damage through, never a status.
            foreach(var slots in new[]{runWeapons.Hand,runWeapons.Auto})foreach(var weapon in slots)
            {
                if(weapon==null||weapon.element==ElementKind.None)continue;
                bool immune=ElementImmunity.BossImmuneTo(weapon.element);
                anyElement=true;anyImmune|=immune;allImmune&=immune;
            }
            foreach(var slot in skills.Slots)
            {
                if(slot.Empty||slot.Definition.element==ElementKind.None)continue;
                voidSkill|=slot.Definition.element==ElementKind.Void;
                if(!ElementImmunity.SkillHitsWithElement(slot.Id))continue;
                bool immune=ElementImmunity.BossImmuneTo(slot.Definition.element);
                anyElement=true;anyImmune|=immune;allImmune&=immune;
            }
            // It may come once one of those bosses is ahead (PenetrationBossAhead).
            progression.MayOfferPenetration=PenetrationBossAhead&&anyImmune;
            progression.MustOfferPenetration=progression.MayOfferPenetration&&anyElement&&allImmune&&!progression.Stats.AtCap(StatId.ElementPenetration);
            // Design document 12.5: the player carries void only after taking the trial's power AND with a void skill equipped; a void-infused weapon alone does not count.
            bool carriesVoid=progression.VoidEnergy&&voidSkill;
            var board=ElementStatus.For(player);if(board)board.CarriesVoid=carriesVoid;
        }
        [Min(1), Tooltip("Damage Penetration card (documents 7.6, 9.5): it may be offered from this wave on, ahead of the Tyrant, and in any wave while a void portal is open, ahead of the Nash Prophet. It is then forced when every element the player deals damage with is one the bosses ignore.")]
        public int penetrationOfferWave=11;
        /// <summary>A boss that ignores Frost and Lightning is ahead: the Nash Prophet while a void portal is open (until the trial is won),
        /// the Tyrant from penetrationOfferWave on. The void trial can open in any wave, long before the Tyrant.</summary>
        bool PenetrationBossAhead=>waves.WaveIndex+1>=penetrationOfferWave||WorldMap.Active&&WorldMap.Active.HasOpenPortal&&!WorldMap.Active.TrialWon;
        public void ExecuteUiCommand(ArenaUiCommand command,int index=-1)
        {
            if(!MenuInputReady||Time.unscaledTime-menuCommandAt<.15f)return;menuCommandAt=Time.unscaledTime;
            var previousPhase=RunPhase;var previousState=State;bool previousArt=Art.Enabled,previousConfirmation=snapshot.earlyStartConfirmation;
            int previousDrafts=progression.BankedDrafts,previousRerolls=progression.RerollsRemaining,previousOffer=pendingOffer,previousDraft=pendingDraft;bool previousWalking=buildWalking;
            switch(command)
            {
                case ArenaUiCommand.Start:if(RunPhase==ArenaUiState.Title)SetPhase(ArenaUiState.Loadout);break;
                case ArenaUiCommand.StartCampaign:if(RunPhase==ArenaUiState.Title&&campaign&&!BuildEdition.IsAlpha)campaign.Begin();break; // the Alpha has no story mode
                case ArenaUiCommand.ChooseMap:
                    // A pick already reloading the scene takes no second one (a second reload would come on top of it).
                    if(RunPhase!=ArenaUiState.Title||index<0||index>=OpenModeMaps.Length||openModeStartPending)return;
                    if(!NeedsMapReload(OpenModeMaps[index])){SetPhase(ArenaUiState.Loadout);break;}
                    WorldMap.NextMapFamily=OpenModeMaps[index];openModeStartPending=true;openModeRequester=this;V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);_=ReloadAfterUiFeedback();return;
                case ArenaUiCommand.OpenSupply:
                    if(RunPhase!=ArenaUiState.SupplyDrop||!SupplyDropReady)return;
                    supplyChoiceOpened=true;SupplyDropReady=false;SetPhase(pendingSupplyChoice);pendingSupplyChoice=ArenaUiState.Title;break;
                case ArenaUiCommand.ToggleArt:if(RunPhase==ArenaUiState.Title)ToggleArt();break;
                case ArenaUiCommand.ChooseLoadout:
                    if(RunPhase!=ArenaUiState.Loadout||index<0||index>2)return;runWeapons.GiveStarting(LoadoutForm(index));NotifyCardPicked(ArenaUiState.Loadout,index,CardRarity.Common);ShowWeaponIntro(runWeapons.Hand[0],CampaignRunning?(System.Action)campaign.AfterLoadout:()=>{State=GameState.Playing;BeginWaveDrafts();OpenRunBegins();SetPhase(ArenaUiState.WaveIntro);waves.StartWaves();V2Audio.Instance?.PlayWave();RunAnalytics.RunStarted(this);});break;
                case ArenaUiCommand.TakeArmory:
                    if(RunPhase!=ArenaUiState.Armory||index<0||index>=runWeapons.Offers.Length||runWeapons.Offers[index]==null)return;var offered=runWeapons.Offers[index];var offeredRarity=ArenaRarity.ForWeapon(offered);string taken=offered.FormName+(offered.upgradeOf!=null?" "+offered.MkLabel:"");if(runWeapons.TakeOffer(index)){RunAnalytics.SupplyResolved(this,offered.upgradeOf!=null?RunAnalytics.SupplyMerge:RunAnalytics.SupplyTakeFree,offered);buildSummary+=ArenaText.Pair("\n军械库：", "\nArmory: ")+taken;NotifyCardPicked(ArenaUiState.Armory,index,offeredRarity);ShowWeaponIntro(offered.upgradeOf??offered,OpenDraft);}else{pendingOffer=index;SetPhase(ArenaUiState.ReplaceWeapon);}break;
                case ArenaUiCommand.SkipArmory:if(RunPhase==ArenaUiState.Armory){RunAnalytics.SupplyResolved(this,RunAnalytics.SupplySkip);OpenDraft();}break;
                case ArenaUiCommand.ReplaceWeapon:if(RunPhase==ArenaUiState.ReplaceWeapon&&pendingOffer>=0){var replacing=runWeapons.Offers[pendingOffer];var replacingRarity=ArenaRarity.ForWeapon(replacing);string replacement=replacing?.FormName;var replacedSlots=replacing!=null&&replacing.IsAuto?runWeapons.Auto:runWeapons.Hand;var replacedWeapon=index>=0&&index<replacedSlots.Length?replacedSlots[index]:null;if(runWeapons.TakeOffer(pendingOffer,index)){RunAnalytics.SupplyResolved(this,RunAnalytics.SupplyReplace,replacing,replacedWeapon);buildSummary+=ArenaText.Pair("\n军械库：", "\nArmory: ")+replacement;NotifyCardPicked(ArenaUiState.ReplaceWeapon,index,replacingRarity);ShowWeaponIntro(replacing?.upgradeOf??replacing,OpenDraft);}}break;
                case ArenaUiCommand.ChooseCard:
                    if(RunPhase is not (ArenaUiState.Draft or ArenaUiState.LevelUp)||index<0||index>2)return;
                    var cards=greaterDraft?greaterCards:progression.CurrentCards;if(index>=cards.Length)return;
                    if(cards[index].NeedsReplacement){pendingDraft=index;replaceReturn=RunPhase;SetPhase(ArenaUiState.ReplaceSkill);break;}
                    var pickedRarity=cards[index].Rarity;
                    if(greaterDraft){if(cards[index].Apply()){progression.NoteTaken(cards[index]);NotifyCardPicked(ArenaUiState.Draft,index,pickedRarity);FinishGreaterReward();}}
                    else{var offeredIds=RunAnalytics.CardIds(cards);bool atLevelUp=RunPhase==ArenaUiState.LevelUp;string pickedTitle=cards[index].Title;
                        if(progression.ApplyCard(index)){RunAnalytics.UpgradePicked(this,atLevelUp,offeredIds,index,draftRerolled);draftRerolled=false;buildSummary+="\n"+pickedTitle;NotifyCardPicked(ArenaUiState.Draft,index,pickedRarity);UpdatePenetrationEligibility();if(RunPhase==ArenaUiState.LevelUp)AfterLevelUpPick();else OpenDraft();}}break;
                case ArenaUiCommand.ReplaceSkill:
                    if(RunPhase!=ArenaUiState.ReplaceSkill)return;var skillRarity=(greaterDraft?greaterCards:progression.CurrentCards)[pendingDraft].Rarity;if(greaterDraft){if(greaterCards[pendingDraft].Apply(index)){progression.NoteTaken(greaterCards[pendingDraft]);NotifyCardPicked(ArenaUiState.ReplaceSkill,index,skillRarity);FinishGreaterReward();}}else{string chosen=progression.CurrentCards[pendingDraft].Title;var offeredIds=RunAnalytics.CardIds(progression.CurrentCards);int pickedDraft=pendingDraft;if(progression.ApplyCard(pendingDraft,index)){RunAnalytics.UpgradePicked(this,replaceReturn==ArenaUiState.LevelUp,offeredIds,pickedDraft,draftRerolled);draftRerolled=false;buildSummary+="\n"+chosen;NotifyCardPicked(ArenaUiState.ReplaceSkill,index,skillRarity);UpdatePenetrationEligibility();if(replaceReturn==ArenaUiState.LevelUp)AfterLevelUpPick();else OpenDraft();}}break;
                case ArenaUiCommand.Reroll:if((RunPhase==ArenaUiState.Draft||RunPhase==ArenaUiState.LevelUp)&&!greaterDraft&&progression.Reroll())draftRerolled=true;break;
                case ArenaUiCommand.StartNextWave:
                    if(RunPhase==ArenaUiState.LevelUp){CloseLevelUp();break;}
                    if(greaterDraft&&RunPhase==ArenaUiState.Draft){FinishGreaterReward();break;}
                    if(RunPhase is not (ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready))return;if(progression.BankedDrafts>0)snapshot.earlyStartConfirmation=true;else BeginNextV2Wave(false);break; // document 2.3: only unpicked cards ask first; armory weapons not taken are gone
                case ArenaUiCommand.ConfirmEarlyStart:if(greaterDraft&&RunPhase==ArenaUiState.Draft)FinishGreaterReward();else if(snapshot.earlyStartConfirmation)BeginNextV2Wave(true);break;
                case ArenaUiCommand.Cancel:snapshot.earlyStartConfirmation=false;if(RunPhase==ArenaUiState.ReplaceWeapon){RunAnalytics.SupplyReplaceCancelled();SetPhase(ArenaUiState.Armory);}else if(RunPhase==ArenaUiState.ReplaceSkill)SetPhase(replaceReturn);else if(RunPhase==ArenaUiState.LevelUp)CloseLevelUp();break;
                // A build panel pauses too (CanPause, GameManagerFlow.cs). The early-start question closes, so nothing can
                // start the wave from the pause menu; Resume brings back the same panel, or the walk with the panel put away.
                case ArenaUiCommand.Pause:if(CanPause){resumePhase=RunPhase;resumeWalking=BuildWalking;snapshot.earlyStartConfirmation=false;SetPhase(ArenaUiState.Paused);}break;
                case ArenaUiCommand.Resume:if(RunPhase==ArenaUiState.Paused){buildWalking=resumeWalking;SetPhase(resumePhase);}break;
                case ArenaUiCommand.Restart:ReportAbandoned("restart");V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);_=ReloadAfterUiFeedback();return;
                case ArenaUiCommand.QuitToTitle:ReportAbandoned("quit_to_title");V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);_=ReloadAfterUiFeedback();return;
                case ArenaUiCommand.DismissWeaponIntro:DismissWeaponIntro();break;
                case ArenaUiCommand.ToggleBuildWalk:
                    // Build phase: Tab puts the panel away so the player can walk the empty arena and step onto an open portal; Tab brings it back.
                    // Never on the greater void reward (CanToggleBuildWalk, GameManagerFlow.cs): a mid-wave win leaves the live wave under it.
                    if(!CanToggleBuildWalk)return;
                    buildWalking=!buildWalking;snapshot.earlyStartConfirmation=false;pendingDraft=pendingOffer=-1;Time.timeScale=CanPlayerMove||BuildTimeFlows?1:0;if(buildWalking)V2Audio.Instance?.ResumeAll();
                    hud?.v2Hud?.RefreshView();break;
                // Only while standing on the portal, in every phase; no screen enters it from a distance.
                case ArenaUiCommand.EnterPortal:WorldMap.Active?.TryEnterPortal(player);break;
            }
            bool accepted=previousWalking!=buildWalking||previousPhase!=RunPhase||previousState!=State||previousArt!=Art.Enabled||previousConfirmation!=snapshot.earlyStartConfirmation
                ||previousDrafts!=progression.BankedDrafts||previousRerolls!=progression.RerollsRemaining||previousOffer!=pendingOffer||previousDraft!=pendingDraft;
            if(accepted)V2Audio.Instance?.PlayUI(command==ArenaUiCommand.Cancel?ArenaUiSound.Cancel:
                command is ArenaUiCommand.OpenSupply or ArenaUiCommand.ChooseLoadout or ArenaUiCommand.TakeArmory or ArenaUiCommand.ReplaceWeapon or ArenaUiCommand.ChooseCard or ArenaUiCommand.ReplaceSkill?ArenaUiSound.Select:ArenaUiSound.Confirm);
            if(accepted && command==ArenaUiCommand.Reroll && previousRerolls!=progression.RerollsRemaining)
            {
                V3WorldAudio.Instance?.OnAcceptedReroll();
                // The crate landing only belongs to the build-phase draft; a mid-wave pick shows its weapon card straight away.
                if(RunPhase==ArenaUiState.Draft&&HasWeaponChoice()){supplyChoiceOpened=false;SetPhase(ArenaUiState.Draft);}
            }
        }
        /// <summary>Restart or Quit to title from the pause menu ends a run still in progress; from the result screen the run already ended.</summary>
        void ReportAbandoned(string cause){if(State==GameState.Playing)RunAnalytics.RunEnded(this,RunPhase==ArenaUiState.Paused?resumePhase:RunPhase,cause);}
        async Awaitable ReloadAfterUiFeedback()
        {
            try
            {
                float until=Time.realtimeSinceStartup+.14f;
                while(Time.realtimeSinceStartup<until)await Awaitable.NextFrameAsync(destroyCancellationToken);
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            catch(System.OperationCanceledException){ }
        }
        public ArenaUiSnapshot UiSnapshot
        {
            get
            {
                if(RunPhase==ArenaUiState.WeaponIntro)FillWeaponIntro();
                snapshot.state=RunPhase;snapshot.buildWalking=BuildWalking;snapshot.supplyLanded=SupplyDropReady;snapshot.isGreaterReward=greaterDraft;snapshot.illustrated=Art.Enabled;snapshot.hp=player?player.Hp:50;snapshot.maxHp=player?player.maxHp:50;snapshot.stamina=player?player.stamina.Value:1;snapshot.staminaExhausted=player&&player.stamina.Exhausted;
                if(progression){snapshot.level=progression.Level;snapshot.xp=progression.Xp;snapshot.xpNeeded=progression.XpRequired;snapshot.energy=progression.Energy;snapshot.maxEnergy=progression.Stats.maxEnergy;snapshot.energyUnlocked=progression.EnergyUnlocked;snapshot.voidEnergy=progression.VoidEnergy;snapshot.voidLevel=progression.VoidBadge;snapshot.maxLevel=progression.Rules?progression.Rules.maxLevel:12;snapshot.bankedDrafts=progression.BankedDrafts;snapshot.rerolls=progression.RerollsRemaining;}
                var status=player?ElementStatus.For(player):null;snapshot.shield=status?status.Shield:0;snapshot.playerStatuses=status?ArenaText.Pair($"火焰 {status.Fire:0.#} · 毒素 {status.Poison:0.#} · 冰冻 {status.Frost:0.#} · 雷电 {status.Lightning:0.#}", $"Fire {status.Fire:0.#} · Poison {status.Poison:0.#} · Frost {status.Frost:0.#} · Lightning {status.Lightning:0.#}"):"";
                snapshot.wave=waves?Mathf.Max(1,waves.WaveIndex+1):1;snapshot.maxWaves=waves?waves.WaveCount:14;snapshot.waveTime=waves?waves.TimeLeft:0;snapshot.buildTime=buildTimeLeft;snapshot.selectionUnlimited=selectionTimeLimit<=0f;snapshot.kills=waves?waves.Kills:0;snapshot.charge01=runWeapons?runWeapons.Charge01:0;WriteHint();
                snapshot.waveTimeExpired=waves&&waves.Phase==WavePhase.WaveActive&&waves.TimeExpired;snapshot.waveBatch=waves?waves.BatchIndex:0;snapshot.waveLiving=waves?waves.LivingCount:0;snapshot.wavePending=waves?waves.PendingCount:0;
                snapshot.draftCount=greaterDraft?1:buildDraftCount;snapshot.draftIndex=greaterDraft?1:Mathf.Clamp(buildDraftCount-snapshot.bankedDrafts+1,1,Mathf.Max(1,buildDraftCount));snapshot.buildSummary=buildSummary.Trim();
                snapshot.bossImmunities="";
                // The Tyrant's immunities are fixed (Frost and Lightning, document 7.6), so no wave announces them; they show during its fight.
                for(int i=0;i<snapshot.bossImmunityIndices.Length;i++)
                {
                    bool known=waves.Phase==WavePhase.FinalBoss&&i<waves.TyrantImmunities.Length;var immune=known?waves.TyrantImmunities[i]:ElementKind.None;
                    snapshot.bossImmunityIndices[i]=known?ElementIndex(immune):-1;
                    if(known)snapshot.bossImmunities+=(i==0?ArenaText.Pair("暴虐君王免疫：", "Tyrant King immunities: "):" · ")+ArenaTerms.Element(immune);
                }
                snapshot.banner=RunPhase==ArenaUiState.WaveIntro?ArenaText.Pair("第 "+snapshot.wave+" 波", "WAVE "+snapshot.wave):"";
                snapshot.resultTitle=State==GameState.Won?ArenaText.Pair("即将重返地球", "EARTH IS WITHIN REACH"):ArenaText.Pair("探索者陨落", "EXPLORER LOST");string causeLine=ResultCauseLine();snapshot.resultDetails=(causeLine.Length>0?causeLine+"\n":"")+ArenaText.Pair($"第 {snapshot.wave} 波 · 等级 {snapshot.level} · 消灭 {snapshot.kills} 个敌人\n在星际战场坚持了 {Elapsed:0} 秒", $"Wave {snapshot.wave} · Level {snapshot.level} · {snapshot.kills} kills\nYou held out on the Interstellar Battlefield for {Elapsed:0} seconds");snapshot.resultCause=causeLine;snapshot.resultWhere=ArenaText.Pair($"第 {snapshot.wave} 波 · 等级 {snapshot.level}",$"Wave {snapshot.wave} · Level {snapshot.level}");snapshot.resultStats=ArenaText.Pair($"消灭 {snapshot.kills} 个敌人\n在星际战场坚持了 {Elapsed:0} 秒",$"{snapshot.kills} kills\nYou held out on the Interstellar Battlefield for {Elapsed:0} seconds");
                if(runWeapons){for(int i=0;i<2;i++)WriteSlot(snapshot.handSlots[i],runWeapons.Hand[i],i==runWeapons.ActiveIndex);for(int i=0;i<snapshot.autoSlots.Length;i++)WriteSlot(snapshot.autoSlots[i],i<runWeapons.Auto.Length?runWeapons.Auto[i]:null,false);for(int i=0;i<snapshot.armoryOffers.Length;i++){var w=i<runWeapons.Offers.Length?runWeapons.Offers[i]:null;WriteArmoryOffer(snapshot.armoryOffers[i],w);}}
                for(int i=0;i<snapshot.armoryOffers.Length;i++)
                {
                    var weapon=runWeapons?runWeapons.Offers[i]:null;var offer=snapshot.armoryOffers[i];
                    offer.rarity=ArenaRarity.ForWeapon(weapon);offer.badge=weapon==null?"":ArenaTerms.Rarity(offer.rarity);
                    offer.elementIndex=weapon==null?-1:ElementIndex(weapon.element);
                }
                for(int i=0;i<3;i++)
                {
                    var loadout=snapshot.loadoutChoices[i];loadout.title=ArenaTerms.WeaponForm(i==0?WeaponForm.RocketLauncher:i==1?WeaponForm.Rifle:WeaponForm.Knife);loadout.body=ArenaText.Pair("它的攻击方式和特性要到你选中的那一刻才会揭晓。", "Its attack and trait stay hidden until the moment you choose it.");loadout.formIndex=i==0?2:i==1?0:1;loadout.elementIndex=-1;
                    loadout.rarity=CardRarity.Common;loadout.badge=ArenaTerms.Rarity(loadout.rarity);loadout.kind=ArenaTerms.CardKind(DraftCardKind.Weapon);
                    if(skills)WriteSkillSlot(snapshot.skillSlots[i],skills.Slots[i]);
                }
                var draft=greaterDraft?greaterCards:progression?.CurrentCards;
                for(int i=0;i<3;i++)
                {
                    var card=draft!=null&&i<draft.Length?draft[i]:null;var view=snapshot.draftCards[i];
                    view.title=card?.Title??"";view.body=card?.Description??"";view.kind=card==null?"":ArenaTerms.CardKind(card.Kind);view.rarity=card?.Rarity??CardRarity.Common;view.badge=card==null?"":ArenaTerms.Rarity(view.rarity);view.available=card!=null;view.formIndex=view.elementIndex=view.skillIndex=view.statIndex=view.axisIndex=-1;
                    if(card?.Payload is SkillId id){view.skillIndex=(int)id;var definition=skills.Catalog.Find(id);if(definition)view.elementIndex=ElementIndex(definition.element);}
                    else if(card?.Payload is StatCardOffer stat)view.statIndex=(int)stat.Stat;
                    else if(card?.Payload is WeaponUpgradeOffer upgrade){view.formIndex=(int)upgrade.Weapon.form;view.elementIndex=ElementIndex(upgrade.Element);view.axisIndex=UpgradeIndex(upgrade.Axis);}
                }
                if(RunPhase==ArenaUiState.ReplaceWeapon&&pendingOffer>=0&&runWeapons.Offers[pendingOffer]!=null){var slots=runWeapons.Offers[pendingOffer].IsAuto?runWeapons.Auto:runWeapons.Hand;snapshot.replacementCount=slots.Length;for(int i=0;i<slots.Length;i++)WriteSlot(snapshot.replacementSlots[i],slots[i],false);}else if(RunPhase==ArenaUiState.ReplaceSkill){snapshot.replacementCount=3;for(int i=0;i<3;i++)WriteSkillSlot(snapshot.replacementSlots[i],skills.Slots[i]);}
                snapshot.bossHp=snapshot.bossMaxHp=snapshot.bossShield=snapshot.bossLine2=snapshot.bossLine3=snapshot.bossShift01=0;snapshot.bossPhase=0;foreach(var e in waves.Alive)if(e&&e.IsBoss&&e.IsAlive){snapshot.bossHp=e.Hp;snapshot.bossMaxHp=e.maxHp;snapshot.bossShield=ElementStatus.For(e)?.Shield??0;snapshot.bossPhase=e.BossPhase;snapshot.bossLine2=e.PhaseLine(2);snapshot.bossLine3=e.PhaseLine(3);snapshot.bossShift01=e.PhaseShifting?Mathf.Max(.001f,e.PhaseShift01):0f;break;}
                // The fifth pass's dock (V5BattleHud) shows the auto slots once an auto weapon is held and the skill slots once a skill is
                // learned, in the open mode as in the story (V2Campaign.FillSnapshot); the English Alpha keeps every slot from the start.
                if(CampaignRunning)campaign.FillSnapshot(snapshot);else{snapshot.campaignLabel="";bool fifth=V5HudKit.On;snapshot.hideAutoSlots=fifth&&!HoldsAutoWeapon;snapshot.hideSkillSlots=fifth&&!(skills&&skills.OwnedCount>0);}
                return snapshot;
            }
        }
        /// <summary>The hint line as the saved HUD shows it over the hands (snapshot.hint), and the same prompt in three parts for the fifth
        /// pass's plate at the top of the screen, which draws the key it names as a keycap: the words before the key, the key (ArenaKeys'
        /// name for it) and the words after it. The parts stay empty where the line only names the weapon in hand: its slot in the dock
        /// shows it. Both carry the same words, each a sentence that ends in what the key does.</summary>
        void WriteHint()
        {
            string lead="",key="",tail="",line;
            // In a black hole's pull the way out comes first, over the card-pick prompt (the pull is faster than a walk).
            if(InHolePull(out bool exhausted))
            {
                key=ArenaKeys.SprintKey;
                if(exhausted){lead=ArenaText.Pair("体力耗尽了。先朝外走，等体力回满再按住 ","Out of stamina. Walk outward, then hold ");tail=ArenaText.Pair(" 冲刺。"," to sprint once the bar is full.");}
                else{lead=ArenaText.Pair("黑洞的引力正在把你拉进去，按住 ","The black hole is pulling you in. Hold ");tail=ArenaText.Pair(" 朝外冲刺才能逃开。"," and sprint outward to break free.");}
                line=lead+key+tail;
            }
            else if(BuildWalking){lead=ArenaText.Pair("整备时可以走动 · 按 ", "Walking between waves · Press ");key=ArenaKeys.Label(Key.Tab);tail=ArenaText.Pair(" 回到选卡", " to return to the cards");line=lead+key+tail;}
            else if(BuildCrateWalking)line=lead=ArenaText.Pair("整备时可以走动。点击头顶的箱子打开它。", "You can walk around. Click the crate above you to open it.");
            else if(RunPhase==ArenaUiState.WaveActive&&progression&&progression.BankedDrafts>0)
            {
                int picks=progression.BankedDrafts;key=ArenaKeys.CardPickKey;
                lead=ArenaText.Pair($"待选卡 ×{picks} · 按 ", $"Card picks ×{picks} · Press ");
                tail=ArenaText.Pair(" 打开", " to open");
                line=lead+key+tail;
            }
            else line=runWeapons&&runWeapons.Active!=null?runWeapons.Active.Label:"";
            snapshot.hint=line;snapshot.hintLead=lead;snapshot.hintKey=key;snapshot.hintTail=tail;
        }
        /// <summary>An auto weapon sits in one of the auto slots.</summary>
        bool HoldsAutoWeapon{get{if(!runWeapons)return false;foreach(var weapon in runWeapons.Auto)if(weapon!=null)return true;return false;}}
        void WriteArmoryOffer(ArenaUiCard card,RunWeapon w)
        {
            card.available=w!=null;card.formIndex=w==null?-1:(int)w.form;
            if(w==null){card.title=ArenaText.Pair("没有可选的武器", "No weapon to offer");card.kind="";card.body="";return;}
            string hand=w.IsAuto?ArenaText.Pair("自动", "AUTO"):ArenaText.Pair("手持", "HAND");
            if(w.upgradeOf!=null)
            {
                float gain=runWeapons.MkDamage(w)/Mathf.Max(.01f,runWeapons.MkDamage(w.upgradeOf))-1f;
                // A stronger version upgrades the held weapon itself; its pairing, upgrades and infusion stay.
                card.title=w.FormName+" "+w.upgradeOf.MkLabel+" → "+w.MkLabel;card.kind=ArenaText.Pair("强化 · ", "UPGRADE · ")+hand;
                card.body=ArenaText.Pair($"强化你现有的{w.upgradeOf.Label}，型号伤害提高 {gain*100f:0}%。它的攻击方式和特性不变，已有的强化和附魔也都保留。", $"Upgrades the {w.upgradeOf.Label} you already have, raising its Mk damage by {gain*100f:0}%. Its attack and trait stay the same, and it keeps its upgrades and infusion.");
                return;
            }
            card.title=w.FormName;card.kind=hand;
            card.body=ArenaText.Pair($"一把新武器，搭配未揭晓。{w.MkLabel}，伤害 ×{runWeapons.MkDamage(w):0.##}。", $"A new weapon whose pairing is still hidden. {w.MkLabel}, damage ×{runWeapons.MkDamage(w):0.##}.");
        }
        void WriteSlot(ArenaUiSlot slot,RunWeapon w,bool active)
        {
            slot.title=w?.Label??ArenaText.Pair("空栏位", "Empty slot");slot.detail=w==null?ArenaText.Pair("没有武器", "No weapon"):ArenaText.Pair($"等级 {w.Level} · ", $"Lv {w.Level} · ")+ArenaTerms.Element(w.element)+(w.reload>0?ArenaText.Pair($" · 换弹 {w.reload:0.0} 秒", $" · Reload {w.reload:0.0} s"):"");slot.formIndex=w==null?-1:(int)w.form;slot.active=active;slot.elementIndex=w==null?-1:ElementIndex(w.element);slot.skillIndex=-1;slot.available=true;
            slot.rarity=ArenaRarity.ForWeapon(w);if(w!=null)slot.detail=ArenaTerms.Rarity(slot.rarity)+" · "+slot.detail;
            // Chain Tame shows the jumps it holds for the next discharge.
            if(w!=null&&w.chainStoredJumps>0&&w.form==WeaponForm.ArcCoil&&w.variant==0)slot.detail+=ArenaText.Pair($" · +{w.chainStoredJumps} 跳",$" · +{w.chainStoredJumps} jumps");
            // Execute shows its threshold (fixed; its armed next shot shows in AnomalySlotDetail).
            if(w!=null&&w.revealed&&w.form==WeaponForm.Railgun&&w.variant==1){float line=runWeapons.ExecuteThreshold(w)*100f;slot.detail+=ArenaText.Pair($" · 处决线 {line:0}%",$" · Execute {line:0}%");}
            // Card states: Throwing Knife Tame's caught knife (Scatter Blast Tame's quick-reload window is drawn on the charge bar).
            if(w!=null&&w.revealed)slot.detail+=runWeapons.AnomalySlotDetail(w);
            // Meltdown shows its heat; a Spin-up minigun left firing in place shows how long it has left.
            if(w!=null&&w.revealed&&w.form==WeaponForm.Minigun&&w.variant==1){float heat=Mathf.Clamp01(w.heat/4f)*100f;slot.detail+=ArenaText.Pair($" · 热量 {heat:0}%",$" · Heat {heat:0}%");}
            if(w!=null&&w.turretLeft>0f)slot.detail+=ArenaText.Pair($" · 架设中 {w.turretLeft:0.0} 秒",$" · Deployed {w.turretLeft:0.0} s");
            // Document 5.4: a stowed Capacitor shows the charge it is losing; in hand the charge bar shows it.
            if(w!=null&&w.revealed&&w.form==WeaponForm.ArcCoil&&w.variant==1&&!active&&w.charge>0f){float stored=Mathf.Ceil(Mathf.Clamp01(w.charge/Mathf.Max(1f,runWeapons.upgrades.capacitorMax))*100f);slot.detail+=ArenaText.Pair($" · 电量 {stored:0}%",$" · Charge {stored:0}%");}
            float cooldown=runWeapons.GetCooldownDuration(w);
            slot.cooldown01=w==null?0:Mathf.Clamp01(w.cooldown/Mathf.Max(.01f,cooldown));
        }
        void WriteSkillSlot(ArenaUiSlot view,SkillSlot skill)
        {
            // The greater void skills have no level: their slot shows only the energy cost.
            view.title=skill.Name;view.detail=skill.Empty?ArenaText.Pair("空槽", "Empty"):skill.Definition.greaterVoid?ArenaText.Pair($"{skill.Cost:0} 充能", $"{skill.Cost:0} Charge"):ArenaText.Pair($"等级 {skill.Level} · {skill.Cost:0} 充能", $"Lv {skill.Level} · {skill.Cost:0} Charge");view.cooldown01=skill.CooldownLeft/Mathf.Max(.01f,skill.Cooldown);view.elementIndex=skill.Empty?-1:ElementIndex(skill.Definition.element);view.skillIndex=skill.Empty?-1:(int)skill.Id;view.formIndex=-1;view.active=false;view.available=!skill.Empty&&skill.CooldownLeft<=0&&progression.Energy>=skill.Cost;
            view.rarity=ArenaRarity.ForSkill(skill.Definition,skill.Level);if(!skill.Empty)view.detail=ArenaTerms.Rarity(view.rarity)+" · "+view.detail;
            // Blink level 3 carries two charges (document 8.4).
            if(!skill.Empty&&skill.Id==SkillId.Blink&&skills.BlinkChargesMax>1){view.detail+=ArenaText.Pair($" · 次数 {skills.BlinkCharges}/{skills.BlinkChargesMax}",$" · Uses {skills.BlinkCharges}/{skills.BlinkChargesMax}");view.available=skills.BlinkCharges>0&&progression.Energy>=skill.Cost;}
            // A skill cannot be cast again while its own effect lasts, so its slot shows as not ready meanwhile.
            if(!skill.Empty&&skills.EffectActive(skill.Id))view.available=false;
        }
        static int UpgradeIndex(UpgradeAxis axis)=>axis switch{UpgradeAxis.Infusion=>0,UpgradeAxis.Power=>1,UpgradeAxis.Tempo=>2,UpgradeAxis.Reach=>3,UpgradeAxis.Twist=>4,UpgradeAxis.Tame=>5,_=>-1};
        static int ElementIndex(ElementKind e)=>e==ElementKind.None?-1:e==ElementKind.Light?5:e==ElementKind.Void?6:(int)e-1;
    }
}
