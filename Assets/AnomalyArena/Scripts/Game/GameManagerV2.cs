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
        public OrbPool orbs;
        public float buildTimeLeft;
        [Min(0f), Tooltip("Weapon and skill choice time limit. Zero means unlimited and disables automatic selection.")]
        public float selectionTimeLimit;
        public ArenaUiState RunPhase { get; private set; }=ArenaUiState.Title;
        public event System.Action WeaponSelectionOpened;
        public bool SupplyDropReady { get; private set; }
        ArenaUiState pendingSupplyChoice;
        bool supplyChoiceOpened;
        public void MarkSupplyLanded(){if(RunPhase==ArenaUiState.SupplyDrop)SupplyDropReady=true;}
        public bool IsCombatActive => State==GameState.Playing&&RunPhase is ArenaUiState.WaveActive or ArenaUiState.VoidTrial or ArenaUiState.FinalBoss;
        public bool CanPlayerMove => State==GameState.Playing&&(RunPhase is ArenaUiState.WaveIntro or ArenaUiState.WaveActive or ArenaUiState.VoidTrial or ArenaUiState.FinalBoss||BuildWalking);
                                                                                                                                            
        public bool BuildWalking => buildWalking&&RunPhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready;
                                                                                                                                            
                                                                                                                                        
                                                                                                                                     
                                                                            
        public bool BuildTimeFlows => State==GameState.Playing&&!greaterDraft&&(RunPhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready or ArenaUiState.ReplaceWeapon||RunPhase==ArenaUiState.ReplaceSkill&&replaceReturn==ArenaUiState.Draft);
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
        int levelUpSeen;ArenaUiState replaceReturn=ArenaUiState.Draft;                                                                               
        bool greaterDraft;
        bool menuMouseReleased;
        float phaseOpenedAt;
        int buildDraftCount;
        int eligibilityWave=-2;
        string buildSummary="";
        DraftCard[] greaterCards;
        string runNotice="";

        void V2Start()
        {
                                                                                                                          
            Art.EnsureV2Default(gameObject.scene.name==Art.IllustratedOnlyScene);
            runWeapons??=player.GetComponent<RunWeapons>();progression??=player.GetComponent<RunProgression>();skills??=player.GetComponent<SkillLoadout>();
            State=GameState.Title;RunPhase=ArenaUiState.Title;supplyChoiceOpened=false;SupplyDropReady=false;pendingSupplyChoice=ArenaUiState.Title;LockMenuInput();
            progression.ResetRun();skills.ResetRun();runWeapons.ResetRun();
            progression.HealingRequested+=player.Heal;
            progression.StatChanged+=(_,__)=>ApplyStats();
            progression.LevelGained+=level=>hud?.Banner(ArenaText.Pair("LEVEL "+level,"LEVEL "+level),ArenaText.Pair($"Max HP +{progression.LastLevelHpGain:0.#}, Max Energy +{progression.LastLevelEnergyGain:0.#}.",$"Max HP +{progression.LastLevelHpGain:0.#}, Max Energy +{progression.LastLevelEnergyGain:0.#}."));
            runWeapons.Revealed+=w=>{runNotice=w.Label;hud?.Toast(w.Label,ElementCombat.ColorFor(w.element));};
            skills.CastRefused+=(_,reason)=>hud?.Toast(reason,Color.yellow);
            waves.WaveCompleted+=OnV2WaveEnded;
            waves.EnemyEliminated+=enemy=>{progression.GainDeathEnergy();if(enemy.SuppressRewards||enemy.IsTrialSummon||enemy.DeathCause==DeathCause.FellIntoGap)return;orbs?.Drop(enemy.Position,waves.ExperienceFor(enemy),enemy.Elite&&Random.value<orbs.eliteHpDropChance,enemy.Elite&&progression.EnergyUnlocked&&Random.value<orbs.eliteEnergyDropChance);};
            if(WorldMap.Active){WorldMap.Active.VoidTrialEntered+=EnterVoidTrial;WorldMap.Active.VoidTrialWon+=CompleteVoidTrial;WorldMap.Active.HoleFed+=_=>V2Audio.Instance?.PlayVoidFeed();}
            SkillWorldAPI.TimeSlip=(factor,seconds)=>{slipLeft=seconds<=0f?0f:Mathf.Max(slipLeft,seconds);slipScale=seconds<=0f?1f:factor;};
            SkillWorldAPI.HoleProtection=null;                                                                         
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
        void V2Update()
        {
            var kb=Keyboard.current;
            if(Mouse.current==null||!Mouse.current.leftButton.isPressed)menuMouseReleased=true;
                                                                                                                             
            if(kb!=null)ReadShownKeys(kb);
            if(RunPhase==ArenaUiState.WaveIntro&&waves.Phase==WavePhase.WaveActive)SetPhase(ArenaUiState.WaveActive);
                                                                                                                                               
            if(RunPhase==ArenaUiState.WaveActive&&progression.BankedDrafts>levelUpSeen){levelUpSeen=progression.BankedDrafts;OpenLevelUp();}
            if(IsCombatActive)
            {
                Elapsed+=Time.deltaTime;progression.Tick(PlayerDeltaTime);ElementStatus.TickAll(WorldDeltaTime,PlayerDeltaTime);SkillLoadout.TickAll(PlayerDeltaTime);runWeapons.Tick(PlayerDeltaTime);orbs?.Tick(WorldDeltaTime);
                slipLeft=Mathf.Max(0,slipLeft-PlayerDeltaTime);if(eligibilityWave!=waves.WaveIndex)UpdatePenetrationEligibility();
            }
            else if(RunPhase==ArenaUiState.TrialReturn)TickTrialReturn();
            else if(RunPhase==ArenaUiState.WaveEnd&&Time.unscaledTime>=waveEndAt)OpenBuild();
            else if(RunPhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready or ArenaUiState.ReplaceWeapon or ArenaUiState.ReplaceSkill)
            {if(BuildTimeFlows)TickBuildPhase();if(!greaterDraft&&selectionTimeLimit>0f){buildTimeLeft=Mathf.Max(0,buildTimeLeft-Time.unscaledDeltaTime);if(buildTimeLeft<=0)BeginNextV2Wave(true);}}
            Cursor.visible=true;
        }
                                                                                                                                                
                                                                                                                                         
                                                                                                                                           
        void TickBuildPhase(){progression.Tick(PlayerDeltaTime);ElementStatus.TickAll(WorldDeltaTime,PlayerDeltaTime);SkillLoadout.TickAll(PlayerDeltaTime);runWeapons.TickTimers(PlayerDeltaTime);}
        void V2LateUpdate()
        {
            if(State!=GameState.Playing)return;
                                                                                               
            if(finalWaveCleared){State=GameState.Won;RunAnalytics.RunEnded(this,RunPhase==ArenaUiState.Paused?resumePhase:RunPhase);ClearTransients();skills.ClearWaveEffects();SetPhase(ArenaUiState.Result);}
            else if(!player.IsAlive){LoseCause=player.DeathCause;State=GameState.Lost;RunAnalytics.RunEnded(this,RunPhase==ArenaUiState.Paused?resumePhase:RunPhase);ClearTransients();skills.ClearWaveEffects();SetPhase(ArenaUiState.Result);}
        }
        void OnV2WaveEnded(int wave)
        {
            if(!player.IsAlive)return;
                                                                                                                       
            player.ClearWaveState();skills.ClearWaveEffects();runWeapons.ClearWaveEffects();orbs?.Clear();
            SetPhase(ArenaUiState.WaveEnd);waveEndAt=Time.unscaledTime+1f;
        }
        void OpenBuild(){supplyChoiceOpened=false;buildTimeLeft=Mathf.Max(0f,selectionTimeLimit);buildDraftCount=progression.BankedDrafts;buildSummary="";UpdatePenetrationEligibility();runWeapons.RollArmory(waves.WaveIndex+2);progression.BeginBuildPhase(waves.WaveIndex+1,WeaponCard,SkillCard);SetPhase(ArenaUiState.Armory);}
        void OpenDraft(){supplyChoiceOpened=false;UpdatePenetrationEligibility();pendingDraft=-1;pendingOffer=-1;if(progression.BankedDrafts>0){DrawDraft();SetPhase(ArenaUiState.Draft);}else SetPhase(ArenaUiState.Ready);}
                                                                                                                                                                                                                                      
        void OpenLevelUp(){if(progression.BankedDrafts==0)return;pendingDraft=-1;DrawDraft();SetPhase(ArenaUiState.LevelUp);}
        void DrawDraft()=>progression.DrawCards(waves.WaveIndex+1,WeaponCard,SkillCard);
                                                                                                                                                                                 
        DraftCard WeaponCard(DraftCard replacing)=>runWeapons.RollUpgradeCard(replacing?.Payload as WeaponUpgradeOffer,waves.WaveIndex+1);
        DraftCard SkillCard(DraftCard replacing)=>skills.RollCard(waves.WaveIndex+1,replacing?.Payload is SkillId id?id:(SkillId?)null);
                                                                                                                                               
                                                                                                                     
        void BeginWaveDrafts(){progression.BeginWave();levelUpSeen=0;}
        void CloseLevelUp(){levelUpSeen=progression.BankedDrafts;SetPhase(ArenaUiState.WaveActive);}
        void AfterLevelUpPick(){levelUpSeen=progression.BankedDrafts;if(progression.BankedDrafts>0)OpenLevelUp();else SetPhase(ArenaUiState.WaveActive);}
        void BeginNextV2Wave(bool automatic)
        {
            if(automatic){int count=progression.BankedDrafts;progression.ResolvePendingRandom();if(count>0)buildSummary+=ArenaText.Pair($"\n{count} pending drafts resolved automatically",$"\n{count} pending drafts resolved automatically");}snapshot.earlyStartConfirmation=false;pendingDraft=pendingOffer=-1;
            player.Heal(player.maxHp);player.stamina.Refill();player.ClearWaveState();BeginWaveDrafts();
            if(waves.WaveIndex+1>=waves.WaveCount){SetPhase(ArenaUiState.FinalBoss);waves.BeginFinalBoss();}
            else{SetPhase(ArenaUiState.WaveIntro);waves.BeginNextWave();V2Audio.Instance?.PlayWave();}
        }
                                                                                                                                           
                                                                         
        public void BeginCutscene(){if(RunPhase==ArenaUiState.Cutscene)return;cutsceneResumePhase=RunPhase;SetPhase(ArenaUiState.Cutscene);}
        public void EndCutscene(){if(RunPhase!=ArenaUiState.Cutscene)return;SetPhase(cutsceneResumePhase);}
        public void EnterVoidTrial(){trialResumePhase=RunPhase;SetPhase(ArenaUiState.VoidTrial);skills.FreezeBattlefieldForTrial(true);runWeapons.FreezeBattlefieldForTrial(true);orbs?.FreezeForTrial(true);}
        public void CompleteVoidTrial(){if(!player.IsAlive)return;                                                                                     
            progression.CompleteVoidTrial();
                                                                                                                                      
                                                                                                                                  
            if(WorldMap.Active&&WorldMap.Active.TrialWon){progression.GrantVoidLevel();if(trialResumePhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready)buildDraftCount++;}
            UpdatePenetrationEligibility();player.Heal(player.maxHp);player.stamina.Refill();skills.FreezeBattlefieldForTrial(false);runWeapons.FreezeBattlefieldForTrial(false);orbs?.FreezeForTrial(false);greaterCards=skills.GreaterVoidCards();greaterDraft=true;SetPhase(ArenaUiState.Draft);}
                                                                                                  
        void FinishGreaterReward()
        {
            greaterDraft=false;greaterCards=null;pendingDraft=pendingOffer=-1;
            snapshot.earlyStartConfirmation=false;UpdatePenetrationEligibility();
                                                                                                                                              
                                                                                                             
                                                                                                                       
            if(trialResumePhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready)WorldMap.Active?.RemoveHolesAfterTrial();
            if(trialResumePhase==ArenaUiState.Ready&&progression.BankedDrafts>0){OpenDraft();return;}
            if(trialResumePhase==ArenaUiState.Draft)DrawDraft();
            if(trialResumePhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready){SetPhase(trialResumePhase);return;}
            BeginTrialReturn();
        }
        public void NotifyFinalBossDefeated()=>finalWaveCleared=true;

                                                                                                
        RunWeapon introWeapon;
        System.Action afterIntro;
                                                                                                                  
        void ShowWeaponIntro(RunWeapon weapon,System.Action then)
        {
            if(weapon==null){then?.Invoke();return;}
            runWeapons.RevealNow(weapon);introWeapon=weapon;afterIntro=then;SetPhase(ArenaUiState.WeaponIntro);
        }
                                                                                                                       
        void NotifyCardPicked(ArenaUiState from,int index,CardRarity rarity){snapshot.cardPickSequence++;snapshot.cardPickState=from;snapshot.cardPickIndex=index;snapshot.cardPickRarity=rarity;}
        void DismissWeaponIntro()
        {
            if(RunPhase!=ArenaUiState.WeaponIntro)return;
            var next=afterIntro;afterIntro=null;introWeapon=null;next?.Invoke();
        }
        void FillWeaponIntro()
        {
            var w=introWeapon;if(w==null)return;
            string kind=w.IsAuto?ArenaText.Pair("Auto weapon","Auto weapon"):ArenaText.Pair("Hand weapon","Hand weapon");
            float damage=runWeapons.HitDamage(w),cycle=runWeapons.CycleSeconds(w);
            string damageLine=damage>0f?ArenaText.Pair($"{damage:0.#} per hit · {cycle:0.##} s between uses",$"{damage:0.#} per hit · {cycle:0.##} s between uses"):ArenaText.Pair("Deals no direct damage","Deals no direct damage");
            string element=w.element!=ElementKind.None?ArenaText.Pair("   Element: ","   Element: ")+ArenaTerms.Element(w.element):"";
            snapshot.weaponIntroTitle=w.Label;
            snapshot.weaponIntroBody=$"<color=#9ad8ff>{kind}</color>\n\n<b><color=#ffd36e>{ArenaText.Pair("How it fires","How it fires")}</color></b>\n{WeaponGuide.How(w.form,w.variant)}\n\n<b><color=#ffd36e>{ArenaText.Pair("Damage","Damage")}</color></b>\n{damageLine}{element}\n\n<b><color=#ff8a73>{ArenaText.Pair("Backfire","Backfire")}</color></b>\n{WeaponGuide.Backfire(w.form,w.variant)}";
            snapshot.weaponIntroHint=ArenaText.Pair("Click anywhere to continue","Click anywhere to continue");
            snapshot.weaponIntroContinue=ArenaText.Pair("CONTINUE","CONTINUE");
            snapshot.weaponIntroForm=(int)w.form;
        }
        void UpdatePenetrationEligibility()
        {
            bool anyImmune=false, anyElement=false, allImmune=true;
            bool voidSkill=false;
            eligibilityWave=waves.WaveIndex;
                                                                                                                                      
                                                                                                  
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
                                                                                    
            progression.MayOfferPenetration=PenetrationBossAhead&&anyImmune;
            progression.MustOfferPenetration=progression.MayOfferPenetration&&anyElement&&allImmune&&!progression.Stats.AtCap(StatId.ElementPenetration);
                                                                                                                                                                            
            bool carriesVoid=progression.VoidEnergy&&voidSkill;
            var board=ElementStatus.For(player);if(board)board.CarriesVoid=carriesVoid;
        }
        [Min(1), Tooltip("Damage Penetration card (documents 7.6, 9.5): it may be offered from this wave on, ahead of the Tyrant, and in any wave while a void portal is open, ahead of the Nash Prophet. It is then forced when every element the player deals damage with is one the bosses ignore.")]
        public int penetrationOfferWave=11;
                                                                                                                                             
                                                                                                                           
        bool PenetrationBossAhead=>waves.WaveIndex+1>=penetrationOfferWave||WorldMap.Active&&WorldMap.Active.HasOpenPortal&&!WorldMap.Active.TrialWon;
        public void ExecuteUiCommand(ArenaUiCommand command,int index=-1)
        {
            if(!MenuInputReady||Time.unscaledTime-menuCommandAt<.15f)return;menuCommandAt=Time.unscaledTime;
            var previousPhase=RunPhase;var previousState=State;bool previousArt=Art.Enabled,previousConfirmation=snapshot.earlyStartConfirmation;
            int previousDrafts=progression.BankedDrafts,previousRerolls=progression.RerollsRemaining,previousOffer=pendingOffer,previousDraft=pendingDraft;bool previousWalking=buildWalking;
            switch(command)
            {
                case ArenaUiCommand.Start:if(RunPhase==ArenaUiState.Title)SetPhase(ArenaUiState.Loadout);break;
                case ArenaUiCommand.OpenSupply:
                    if(RunPhase!=ArenaUiState.SupplyDrop||!SupplyDropReady)return;
                    supplyChoiceOpened=true;SupplyDropReady=false;SetPhase(pendingSupplyChoice);pendingSupplyChoice=ArenaUiState.Title;break;
                case ArenaUiCommand.ToggleArt:if(RunPhase==ArenaUiState.Title)ToggleArt();break;
                case ArenaUiCommand.ChooseLoadout:
                    if(RunPhase!=ArenaUiState.Loadout||index<0||index>2)return;runWeapons.GiveStarting(index==0?WeaponForm.RocketLauncher:index==1?WeaponForm.Rifle:WeaponForm.Knife);NotifyCardPicked(ArenaUiState.Loadout,index,CardRarity.Common);ShowWeaponIntro(runWeapons.Hand[0],()=>{State=GameState.Playing;BeginWaveDrafts();SetPhase(ArenaUiState.WaveIntro);waves.StartWaves();V2Audio.Instance?.PlayWave();RunAnalytics.RunStarted(this);});break;
                case ArenaUiCommand.TakeArmory:
                    if(RunPhase!=ArenaUiState.Armory||index<0||index>=runWeapons.Offers.Length||runWeapons.Offers[index]==null)return;var offered=runWeapons.Offers[index];var offeredRarity=ArenaRarity.ForWeapon(offered);string taken=offered.FormName+(offered.upgradeOf!=null?" "+offered.MkLabel:"");if(runWeapons.TakeOffer(index)){buildSummary+=ArenaText.Pair("\nArmory: ","\nArmory: ")+taken;NotifyCardPicked(ArenaUiState.Armory,index,offeredRarity);ShowWeaponIntro(offered.upgradeOf??offered,OpenDraft);}else{pendingOffer=index;SetPhase(ArenaUiState.ReplaceWeapon);}break;
                case ArenaUiCommand.SkipArmory:if(RunPhase==ArenaUiState.Armory)OpenDraft();break;
                case ArenaUiCommand.ReplaceWeapon:if(RunPhase==ArenaUiState.ReplaceWeapon&&pendingOffer>=0){var replacing=runWeapons.Offers[pendingOffer];var replacingRarity=ArenaRarity.ForWeapon(replacing);string replacement=replacing?.FormName;if(runWeapons.TakeOffer(pendingOffer,index)){buildSummary+=ArenaText.Pair("\nArmory: ","\nArmory: ")+replacement;NotifyCardPicked(ArenaUiState.ReplaceWeapon,index,replacingRarity);ShowWeaponIntro(replacing?.upgradeOf??replacing,OpenDraft);}}break;
                case ArenaUiCommand.ChooseCard:
                    if(RunPhase is not (ArenaUiState.Draft or ArenaUiState.LevelUp)||index<0||index>2)return;
                    var cards=greaterDraft?greaterCards:progression.CurrentCards;if(index>=cards.Length)return;
                    if(cards[index].NeedsReplacement){pendingDraft=index;replaceReturn=RunPhase;SetPhase(ArenaUiState.ReplaceSkill);break;}
                    var pickedRarity=cards[index].Rarity;
                    if(greaterDraft){if(cards[index].Apply()){NotifyCardPicked(ArenaUiState.Draft,index,pickedRarity);FinishGreaterReward();}}
                    else if(progression.ApplyCard(index)){buildSummary+="\n"+cards[index].Title;NotifyCardPicked(ArenaUiState.Draft,index,pickedRarity);UpdatePenetrationEligibility();if(RunPhase==ArenaUiState.LevelUp)AfterLevelUpPick();else OpenDraft();}break;
                case ArenaUiCommand.ReplaceSkill:
                    if(RunPhase!=ArenaUiState.ReplaceSkill)return;var skillRarity=(greaterDraft?greaterCards:progression.CurrentCards)[pendingDraft].Rarity;if(greaterDraft){if(greaterCards[pendingDraft].Apply(index)){NotifyCardPicked(ArenaUiState.ReplaceSkill,index,skillRarity);FinishGreaterReward();}}else{string chosen=progression.CurrentCards[pendingDraft].Title;if(progression.ApplyCard(pendingDraft,index)){buildSummary+="\n"+chosen;NotifyCardPicked(ArenaUiState.ReplaceSkill,index,skillRarity);UpdatePenetrationEligibility();if(replaceReturn==ArenaUiState.LevelUp)AfterLevelUpPick();else OpenDraft();}}break;
                case ArenaUiCommand.Reroll:if((RunPhase==ArenaUiState.Draft||RunPhase==ArenaUiState.LevelUp)&&!greaterDraft)progression.Reroll();break;
                case ArenaUiCommand.StartNextWave:
                    if(RunPhase==ArenaUiState.LevelUp){CloseLevelUp();break;}
                    if(greaterDraft&&RunPhase==ArenaUiState.Draft){FinishGreaterReward();break;}
                    if(RunPhase is not (ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready))return;if(progression.BankedDrafts>0||RunPhase==ArenaUiState.Armory)snapshot.earlyStartConfirmation=true;else BeginNextV2Wave(false);break;
                case ArenaUiCommand.ConfirmEarlyStart:if(greaterDraft&&RunPhase==ArenaUiState.Draft)FinishGreaterReward();else if(snapshot.earlyStartConfirmation)BeginNextV2Wave(true);break;
                case ArenaUiCommand.Cancel:snapshot.earlyStartConfirmation=false;if(RunPhase==ArenaUiState.ReplaceWeapon)SetPhase(ArenaUiState.Armory);else if(RunPhase==ArenaUiState.ReplaceSkill)SetPhase(replaceReturn);else if(RunPhase==ArenaUiState.LevelUp)CloseLevelUp();break;
                                                                                                                                             
                                                                                                                              
                case ArenaUiCommand.Pause:if(CanPause){resumePhase=RunPhase;resumeWalking=BuildWalking;snapshot.earlyStartConfirmation=false;SetPhase(ArenaUiState.Paused);}break;
                case ArenaUiCommand.Resume:if(RunPhase==ArenaUiState.Paused){buildWalking=resumeWalking;SetPhase(resumePhase);}break;
                case ArenaUiCommand.Restart:ReportAbandoned("restart");V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);_=ReloadAfterUiFeedback();return;
                case ArenaUiCommand.QuitToTitle:ReportAbandoned("quit_to_title");V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);_=ReloadAfterUiFeedback();return;
                case ArenaUiCommand.DismissWeaponIntro:DismissWeaponIntro();break;
                case ArenaUiCommand.ToggleBuildWalk:
                                                                                                                                                    
                                                                                                                                               
                    if(!CanToggleBuildWalk)return;
                    buildWalking=!buildWalking;snapshot.earlyStartConfirmation=false;pendingDraft=pendingOffer=-1;Time.timeScale=CanPlayerMove||BuildTimeFlows?1:0;if(buildWalking)V2Audio.Instance?.ResumeAll();
                    hud?.v2Hud?.RefreshView();break;
                                                                                                                            
                case ArenaUiCommand.EnterPortal:WorldMap.Active?.TryEnterPortal(player);break;
            }
            bool accepted=previousWalking!=buildWalking||previousPhase!=RunPhase||previousState!=State||previousArt!=Art.Enabled||previousConfirmation!=snapshot.earlyStartConfirmation
                ||previousDrafts!=progression.BankedDrafts||previousRerolls!=progression.RerollsRemaining||previousOffer!=pendingOffer||previousDraft!=pendingDraft;
            if(accepted)V2Audio.Instance?.PlayUI(command==ArenaUiCommand.Cancel?ArenaUiSound.Cancel:
                command is ArenaUiCommand.OpenSupply or ArenaUiCommand.ChooseLoadout or ArenaUiCommand.TakeArmory or ArenaUiCommand.ReplaceWeapon or ArenaUiCommand.ChooseCard or ArenaUiCommand.ReplaceSkill?ArenaUiSound.Select:ArenaUiSound.Confirm);
            if(accepted && command==ArenaUiCommand.Reroll && previousRerolls!=progression.RerollsRemaining)
            {
                V3WorldAudio.Instance?.OnAcceptedReroll();
                                                                                                                                
                if(RunPhase==ArenaUiState.Draft&&HasWeaponChoice()){supplyChoiceOpened=false;SetPhase(ArenaUiState.Draft);}
            }
        }
                                                                                                                                                       
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
                var status=player?ElementStatus.For(player):null;snapshot.shield=status?status.Shield:0;snapshot.playerStatuses=status?ArenaText.Pair($"Fire {status.Fire:0.#} · Poison {status.Poison:0.#} · Frost {status.Frost:0.#} · Lightning {status.Lightning:0.#}",$"Fire {status.Fire:0.#} · Poison {status.Poison:0.#} · Frost {status.Frost:0.#} · Lightning {status.Lightning:0.#}"):"";
                snapshot.wave=waves?Mathf.Max(1,waves.WaveIndex+1):1;snapshot.maxWaves=waves?waves.WaveCount:14;snapshot.waveTime=waves?waves.TimeLeft:0;snapshot.buildTime=buildTimeLeft;snapshot.selectionUnlimited=selectionTimeLimit<=0f;snapshot.kills=waves?waves.Kills:0;snapshot.charge01=runWeapons?runWeapons.Charge01:0;if(!runWeapons||!runWeapons.ChargeWindow(out snapshot.chargeWindowStart,out snapshot.chargeWindowEnd))snapshot.chargeWindowStart=snapshot.chargeWindowEnd=0f;snapshot.hint=BuildWalking?(WorldMap.Active&&WorldMap.Active.HasOpenPortal&&!WorldMap.Active.TrialWon?ArenaText.Pair("WALKING · TAB BACK TO THE CARDS · STAND ON THE PORTAL AND PRESS E","WALKING · TAB BACK TO THE CARDS · STAND ON THE PORTAL AND PRESS E"):ArenaText.Pair("WALKING · TAB BACK TO THE CARDS","WALKING · TAB BACK TO THE CARDS")):RunPhase==ArenaUiState.WaveActive&&progression&&progression.BankedDrafts>0?ArenaText.Pair($"CARD PICKS ×{progression.BankedDrafts} · PRESS T",$"CARD PICKS ×{progression.BankedDrafts} · PRESS T"):runNotice;
                snapshot.waveTimeExpired=waves&&waves.Phase==WavePhase.WaveActive&&waves.TimeExpired;snapshot.waveBatch=waves?waves.BatchIndex:0;snapshot.waveLiving=waves?waves.LivingCount:0;snapshot.wavePending=waves?waves.PendingCount:0;
                snapshot.draftCount=greaterDraft?1:buildDraftCount;snapshot.draftIndex=greaterDraft?1:Mathf.Clamp(buildDraftCount-snapshot.bankedDrafts+1,1,Mathf.Max(1,buildDraftCount));snapshot.buildSummary=buildSummary.Trim();
                snapshot.bossImmunities="";
                                                                                                                                                
                for(int i=0;i<snapshot.bossImmunityIndices.Length;i++)
                {
                    bool known=waves.Phase==WavePhase.FinalBoss&&i<waves.TyrantImmunities.Length;var immune=known?waves.TyrantImmunities[i]:ElementKind.None;
                    snapshot.bossImmunityIndices[i]=known?ElementIndex(immune):-1;
                    if(known)snapshot.bossImmunities+=(i==0?ArenaText.Pair("Tyrant King immunities: ","Tyrant King immunities: "):" · ")+ArenaTerms.Element(immune);
                }
                snapshot.banner=RunPhase==ArenaUiState.WaveIntro?ArenaText.Pair("WAVE "+snapshot.wave,"WAVE "+snapshot.wave):"";
                snapshot.resultTitle=State==GameState.Won?ArenaText.Pair("EARTH IS WITHIN REACH","EARTH IS WITHIN REACH"):ArenaText.Pair("EXPLORER LOST","EXPLORER LOST");snapshot.resultDetails=ArenaText.Pair($"Wave {snapshot.wave} · Level {snapshot.level} · {snapshot.kills} kills\n{Elapsed:0} seconds on the Interstellar Battlefield",$"Wave {snapshot.wave} · Level {snapshot.level} · {snapshot.kills} kills\n{Elapsed:0} seconds on the Interstellar Battlefield");
                if(runWeapons){for(int i=0;i<2;i++)WriteSlot(snapshot.handSlots[i],runWeapons.Hand[i],i==runWeapons.ActiveIndex);for(int i=0;i<snapshot.autoSlots.Length;i++)WriteSlot(snapshot.autoSlots[i],i<runWeapons.Auto.Length?runWeapons.Auto[i]:null,false);for(int i=0;i<snapshot.armoryOffers.Length;i++){var w=i<runWeapons.Offers.Length?runWeapons.Offers[i]:null;WriteArmoryOffer(snapshot.armoryOffers[i],w);}}
                for(int i=0;i<snapshot.armoryOffers.Length;i++)
                {
                    var weapon=runWeapons?runWeapons.Offers[i]:null;var offer=snapshot.armoryOffers[i];
                    offer.rarity=ArenaRarity.ForWeapon(weapon);offer.badge=weapon==null?"":ArenaTerms.Rarity(offer.rarity);
                    offer.elementIndex=weapon==null?-1:ElementIndex(weapon.element);
                }
                for(int i=0;i<3;i++)
                {
                    var loadout=snapshot.loadoutChoices[i];loadout.title=ArenaTerms.WeaponForm(i==0?WeaponForm.RocketLauncher:i==1?WeaponForm.Rifle:WeaponForm.Knife);loadout.body=ArenaText.Pair("Which backfire it has is unknown until you take it; it is revealed the moment you choose.","Which backfire it has is unknown until you take it; it is revealed the moment you choose.");loadout.formIndex=i==0?2:i==1?0:1;loadout.elementIndex=-1;
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
                    else if(card?.Payload is WeaponUpgradeOffer upgrade){view.formIndex=(int)upgrade.Weapon.form;view.elementIndex=ElementIndex(upgrade.Axis==UpgradeAxis.Infusion?upgrade.Element:upgrade.Weapon.element);view.axisIndex=UpgradeIndex(upgrade.Axis);}
                }
                if(RunPhase==ArenaUiState.ReplaceWeapon&&pendingOffer>=0&&runWeapons.Offers[pendingOffer]!=null){var slots=runWeapons.Offers[pendingOffer].IsAuto?runWeapons.Auto:runWeapons.Hand;snapshot.replacementCount=slots.Length;for(int i=0;i<slots.Length;i++)WriteSlot(snapshot.replacementSlots[i],slots[i],false);}else if(RunPhase==ArenaUiState.ReplaceSkill){snapshot.replacementCount=3;for(int i=0;i<3;i++)WriteSkillSlot(snapshot.replacementSlots[i],skills.Slots[i]);}
                snapshot.bossHp=snapshot.bossMaxHp=snapshot.bossShield=0;foreach(var e in waves.Alive)if(e&&e.IsBoss&&e.IsAlive){snapshot.bossHp=e.Hp;snapshot.bossMaxHp=e.maxHp;snapshot.bossShield=ElementStatus.For(e)?.Shield??0;break;}
                return snapshot;
            }
        }
        void WriteArmoryOffer(ArenaUiCard card,RunWeapon w)
        {
            card.available=w!=null;card.formIndex=w==null?-1:(int)w.form;
            if(w==null){card.title=ArenaText.Pair("No eligible weapon","No eligible weapon");card.kind="";card.body="";return;}
            string hand=w.IsAuto?ArenaText.Pair("AUTO","AUTO"):ArenaText.Pair("HAND","HAND");
            if(w.upgradeOf!=null)
            {
                float gain=runWeapons.MkDamage(w)/Mathf.Max(.01f,runWeapons.MkDamage(w.upgradeOf))-1f;
                                                                                                                                            
                card.title=w.FormName+" "+w.upgradeOf.MkLabel+" → "+w.MkLabel;card.kind=ArenaText.Pair("STRONGER · ","STRONGER · ")+hand;
                card.body=ArenaText.Pair($"Replaces your {w.upgradeOf.Label}: Mk damage +{gain*100f:0}%. Its backfire is revealed when taken; the old upgrades and infusion are lost.",$"Replaces your {w.upgradeOf.Label}: Mk damage +{gain*100f:0}%. Its backfire is revealed when taken; the old upgrades and infusion are lost.");
                return;
            }
            card.title=w.FormName;card.kind=hand;
            card.body=ArenaText.Pair($"New weapon · backfire unrevealed · {w.MkLabel} (damage ×{runWeapons.MkDamage(w):0.##})",$"New weapon · backfire unrevealed · {w.MkLabel} (damage ×{runWeapons.MkDamage(w):0.##})");
        }
        void WriteSlot(ArenaUiSlot slot,RunWeapon w,bool active)
        {
            slot.title=w?.Label??ArenaText.Pair("Empty slot","Empty slot");slot.detail=w==null?ArenaText.Pair("No weapon","No weapon"):ArenaText.Pair($"Lv {w.Level} · ",$"Lv {w.Level} · ")+ArenaTerms.Element(w.element)+(w.reload>0?ArenaText.Pair($" · Reload {w.reload:0.0}s",$" · Reload {w.reload:0.0}s"):"")+(w.jammed?ArenaText.Pair(" · JAMMED"," · JAMMED"):"");slot.formIndex=w==null?-1:(int)w.form;slot.active=active;slot.elementIndex=w==null?-1:ElementIndex(w.element);slot.skillIndex=-1;slot.available=true;
            slot.rarity=ArenaRarity.ForWeapon(w);if(w!=null)slot.detail=ArenaTerms.Rarity(slot.rarity)+" · "+slot.detail;
                                                                          
            if(w!=null&&w.chainStoredJumps>0&&w.form==WeaponForm.ArcCoil&&w.variant==0)slot.detail+=ArenaText.Pair($" · +{w.chainStoredJumps} jumps",$" · +{w.chainStoredJumps} jumps");
                                                                               
            if(w!=null&&w.revealed&&w.form==WeaponForm.Railgun&&w.variant==1){float line=runWeapons.ExecuteThreshold(w)*100f;slot.detail+=ArenaText.Pair($" · Execute {line:0}%",$" · Execute {line:0}%");}
                                                                                                                                     
            if(w!=null&&w.revealed)slot.detail+=runWeapons.AnomalySlotDetail(w);
                                                                                                          
            if(w!=null&&w.revealed&&w.form==WeaponForm.Minigun&&w.variant==1){float heat=Mathf.Clamp01(w.heat/4f)*100f;slot.detail+=ArenaText.Pair($" · Heat {heat:0}%",$" · Heat {heat:0}%");}
            if(w!=null&&w.turretLeft>0f)slot.detail+=ArenaText.Pair($" · Deployed {w.turretLeft:0.0}s",$" · Deployed {w.turretLeft:0.0}s");
            float cooldown=runWeapons.GetCooldownDuration(w);
            slot.cooldown01=w==null?0:Mathf.Clamp01(w.cooldown/Mathf.Max(.01f,cooldown));
        }
        void WriteSkillSlot(ArenaUiSlot view,SkillSlot skill)
        {
            view.title=skill.Name;view.detail=skill.Empty?ArenaText.Pair("Empty","Empty"):ArenaText.Pair($"Lv {skill.Level} · {skill.Cost:0} Energy",$"Lv {skill.Level} · {skill.Cost:0} Energy");view.cooldown01=skill.CooldownLeft/Mathf.Max(.01f,skill.Cooldown);view.elementIndex=skill.Empty?-1:ElementIndex(skill.Definition.element);view.skillIndex=skill.Empty?-1:(int)skill.Id;view.formIndex=-1;view.active=false;view.available=!skill.Empty&&skill.CooldownLeft<=0&&progression.Energy>=skill.Cost;
            view.rarity=ArenaRarity.ForSkill(skill.Definition,skill.Level);if(!skill.Empty)view.detail=ArenaTerms.Rarity(view.rarity)+" · "+view.detail;
                                                                
            if(!skill.Empty&&skill.Id==SkillId.Blink&&skills.BlinkChargesMax>1){view.detail+=ArenaText.Pair($" · Charges {skills.BlinkCharges}/{skills.BlinkChargesMax}",$" · Charges {skills.BlinkCharges}/{skills.BlinkChargesMax}");view.available=skills.BlinkCharges>0&&progression.Energy>=skill.Cost;}
                                                                                                                                   
            if(!skill.Empty&&skills.EffectActive(skill.Id))view.available=false;
        }
        static int UpgradeIndex(UpgradeAxis axis)=>axis switch{UpgradeAxis.Infusion=>0,UpgradeAxis.Power=>1,UpgradeAxis.Tempo=>2,UpgradeAxis.Reach=>3,UpgradeAxis.Twist=>4,UpgradeAxis.Tame=>5,_=>-1};
        static int ElementIndex(ElementKind e)=>e==ElementKind.None?-1:e==ElementKind.Light?5:e==ElementKind.Void?6:(int)e-1;
    }
}
