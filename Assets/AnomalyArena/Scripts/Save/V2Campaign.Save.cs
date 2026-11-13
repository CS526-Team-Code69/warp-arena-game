using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The story mode's saves (ArenaSaves). A run bound to a slot (GameManager.RunSaveSlot) saves at the start of each morning, afternoon
    /// and night from day 1 on, each time just before the phase's entry does its one-shot things (the day counted up, the HP filled, the
    /// base's upkeep, the ore and the crate placed, the shadows raised): the morning in EndNight, the afternoon in InterceptBuild, the
    /// night in EnterNight. A save is that moment's state, so continuing it (BeginFromSave) goes through the same entry again: the morning
    /// through EndNight, the afternoon through BeginDusk, the night through BeginNight. Drops still lying on the ground are saved as
    /// picked up, lamps set down in the afternoon as back in the stock, and the afternoon's ore left unmined is not kept. The map and the
    /// black hole's place come back from the saved map and seed; ore, crate offers and the monsters' affixes are rolled again. Nothing is
    /// saved once the ignition has started.
    /// </summary>
    public sealed partial class V2Campaign
    {
        // The terrain's seed for the next SetupCampaignMap: a continued save's, or 0 for a new one.
        int pinnedMapSeed;

        /// <summary>The campaign can take a continued save: its screen is made (in Start), or it never will be.</summary>
        internal bool ReadyToContinue => ui || !gm || !rules;

        /// <summary>The save at the start of a phase, into the run's slot (none when the run is bound to no slot, and none once the
        /// ignition has started).</summary>
        void SaveStoryPhase(SavePhase phase)
        {
            if (!Running || !gm || gm.RunSaveSlot < 0 || gm.RunSaveMode != SaveMode.Story) return;
            if (EndingDone || Stage is CampaignStage.Story or CampaignStage.FinalBoss or CampaignStage.Core or CampaignStage.Ending) return;
            try
            {
                var save = CaptureStory(phase);
                if (ArenaSaves.Store(SaveMode.Story, gm.RunSaveSlot, save)) Debug.Log($"[Saves] story, slot {gm.RunSaveSlot + 1}: day {save.summary.day}, {phase}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Saves] the story's {phase} save failed; the run goes on without it. {e}");
            }
        }

        SaveData CaptureStory(SavePhase phase)
        {
            var save = new SaveData { run = gm.CaptureRun(IsVoidEnchanted) };
            var s = save.story;
            s.day = Day;
            s.phase = phase;
            s.overtime = Overtime;
            s.waveOfDay = WaveOfDay;
            s.mapSeed = MapSeed;

            // Drops still lying on the ground count as picked up.
            int lyingAlloy = 0, lyingEnergy = 0;
            foreach (var pickup in pickups)
                if (pickup.kind == PickupKind.Energy) lyingEnergy += pickup.amount;
                else lyingAlloy += pickup.amount;
            s.alloy = alloy + lyingAlloy;
            s.energy = energy + lyingEnergy;
            s.dark = dark;
            s.earnedAlloy = EarnedAlloy + lyingAlloy;
            s.earnedEnergy = EarnedEnergy + lyingEnergy;
            s.earnedDark = EarnedDark;
            // Lamps standing count as in the stock: only the afternoon's can stand at a save (the night's are gone by dawn).
            s.lampStock = lampStock + lamps.Count;
            s.nightAlloy = nightAlloy;
            s.nightDark = nightDark;
            s.voidEnergy = voidEnergy;
            s.voidAlloy = voidAlloy;
            s.voidWalletSeen = voidWalletSeen;
            s.prophetBeaten = prophetBeaten;
            s.voidArmor = voidArmor;
            s.hasVoidOreCentre = voidOreCentre.HasValue;
            s.voidOreCentre = voidOreCentre ?? Vector3.zero;

            s.roomLevels = new int[rooms.Length];
            s.roomDamaged = new bool[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                s.roomLevels[i] = rooms[i].level;
                s.roomDamaged[i] = rooms[i].damaged;
            }
            s.durability = Durability;
            s.shieldLevel = ShieldLevel;
            s.baseHp = BaseHp;
            s.baseShield = BaseShield;
            s.engineFixed = engineFixed;
            s.armorFixed = armorFixed;
            s.fuel = fuel;

            s.newWeaponsBought = newWeaponsBought;
            s.upgradesBought = (int[])upgradesBought.Clone();
            s.teamMk = teamMk;
            s.shipOreFromDay = shipOreFromDay;
            s.researchBought = (int[])researchBought.Clone();
            s.statsBoughtStat = new int[statsBought.Count];
            s.statsBoughtRarity = new int[statsBought.Count];
            int bought = 0;
            foreach (var (stat, rarity) in statsBought)
            {
                s.statsBoughtStat[bought] = (int)stat;
                s.statsBoughtRarity[bought] = (int)rarity;
                bought++;
            }
            s.overloadShieldBuys = overloadShieldBuys;
            s.overloadDamageBuys = overloadDamageBuys;
            s.overloadShieldGiven = overloadShieldGiven;

            s.crateKinds = new int[crates.Count];
            s.cratePositions = new Vector3[crates.Count];
            for (int i = 0; i < crates.Count; i++)
            {
                s.crateKinds[i] = (int)crates[i].kind;
                s.cratePositions[i] = crates[i].position;
            }
            s.cratesOpened = cratesOpened;
            s.changedWeaponSaid = changedWeaponSaid;
            s.switchTaught = switchTaught;
            s.skillTaught = skillTaught;
            s.skillTeachPending = skillTeachPending;
            s.voidLineSaid = voidLineSaid;

            var map = WorldMap.Active;
            s.holeSeen = holeSeen;
            s.holeFailedToday = holeFailedToday;
            s.holeAwake = holeAwake;
            s.holeCheckDay = holeCheckDay;
            s.holeFed = holeFed;
            s.checkState = holeCheck.State;
            s.checkRequired = holeCheck.Required;
            s.checkSeconds = holeCheck.Seconds;
            s.checkDealt = holeCheck.Dealt;
            s.checkElapsed = holeCheck.Elapsed;
            s.hole = HoleSave.Of(map ? map.CampaignHole : null);
            s.hasTrailTarget = trailTarget.HasValue;
            s.trailTarget = trailTarget ?? Vector3.zero;

            s.killRegion = new int[regionKills.Count];
            s.killCount = new int[regionKills.Count];
            int region = 0;
            foreach (var pair in regionKills)
            {
                s.killRegion[region] = pair.Key;
                s.killCount[region] = pair.Value;
                region++;
            }
            var kindRegions = new List<int>();
            var kinds = new List<int>();
            foreach (var pair in regionKindKills)
                foreach (var kind in pair.Value)
                {
                    kindRegions.Add(pair.Key);
                    kinds.Add((int)kind);
                }
            s.kindRegion = kindRegions.ToArray();
            s.kindKill = kinds.ToArray();

            var summary = save.summary;
            summary.day = ArenaSaves.StoryDay(Day, phase);
            summary.phase = phase;
            summary.engineFixed = engineFixed;
            summary.armorFixed = armorFixed;
            summary.fuel = fuel;
            summary.fuelMax = FuelUnits;
            summary.mapFamily = ArenaSaves.MapFamily(map ? map.biome : null);
            summary.weapons = ArenaSaves.WeaponsOf(gm.runWeapons);
            return save;
        }

        /// <summary>What a won run's cleared slot shows of it (ArenaSaves.MarkCleared): its last day and the ship as it left.</summary>
        internal void FillFinishedSummary(SaveSummary summary)
        {
            summary.day = Mathf.Max(1, Day);
            summary.engineFixed = engineFixed;
            summary.armorFixed = armorFixed;
            summary.fuel = fuel;
            summary.fuelMax = FuelUnits;
        }

        /// <summary>A continued save, in the scene loaded for it: the run starts as a new one does on the same map (WorldMap laid the
        /// saved map, the terrain and the black hole follow the saved seed), takes the saved state, and enters the saved phase through its
        /// own entry. The title card, the starting pistol and Day 0 are left out, and the first black hole's cutscene does not play again
        /// once it had played. False when the run cannot start here.</summary>
        internal bool BeginFromSave(SaveData save)
        {
            var s = save?.story;
            if (s == null || !ui) return false;
            pinnedMapSeed = s.mapSeed;
            bool started = StartRun();
            pinnedMapSeed = 0;
            if (!started) return false;
            RestoreStory(s);
            gm.ApplyRun(save.run);
            RestoreVoidEnchants(save.run.hand, gm.runWeapons.Hand);
            RestoreVoidEnchants(save.run.auto, gm.runWeapons.Auto);
            SetTeamMk(s.teamMk); // every anomaly weapon held at the armament's Mk
            RestoreHoleQuest(s, save.run);
            // What Day 0 leaves behind for the days after it: the tutorial over, the crates' loop running.
            tutStep = tutLastDone = TutorialStep.Done;
            tutForced = false;
            tutNext = null;
            tutorialHint = "";
            PlayerController.InputGate = TutorialLets;
            if (!crateLoopRunning)
            {
                crateLoopRunning = true;
                StartCoroutine(CrateLoop());
            }
            gm.CampaignEnterPlay(); // the run is on (its analytics row reads the restored totals)
            EnterSavedPhase(s.phase);
            return true;
        }

        void RestoreStory(StorySave s)
        {
            Day = Mathf.Max(0, s.day);
            WaveOfDay = Mathf.Max(0, s.waveOfDay);
            Overtime = s.overtime;

            alloy = Mathf.Max(0, s.alloy);
            energy = Mathf.Max(0, s.energy);
            dark = Mathf.Max(0, s.dark);
            EarnedAlloy = s.earnedAlloy;
            EarnedEnergy = s.earnedEnergy;
            EarnedDark = s.earnedDark;
            lampStock = Mathf.Max(0, s.lampStock);
            nightAlloy = s.nightAlloy;
            nightDark = s.nightDark;
            voidEnergy = Mathf.Max(0, s.voidEnergy);
            voidAlloy = Mathf.Max(0, s.voidAlloy);
            voidWalletSeen = s.voidWalletSeen;
            prophetBeaten = s.prophetBeaten;
            voidArmor = s.voidArmor;
            voidOreCentre = s.hasVoidOreCentre ? s.voidOreCentre : null;

            for (int i = 0; i < rooms.Length; i++)
            {
                if (s.roomLevels != null && i < s.roomLevels.Length) rooms[i].level = Mathf.Max(0, s.roomLevels[i]);
                rooms[i].damaged = s.roomDamaged != null && i < s.roomDamaged.Length && s.roomDamaged[i];
            }
            Durability = Mathf.Max(1, s.durability);
            ShieldLevel = Mathf.Max(0, s.shieldLevel);
            BaseHp = Mathf.Clamp(s.baseHp, 1f, BaseMaxHp);
            BaseShield = Mathf.Clamp(s.baseShield, 0f, BaseMaxShield);
            engineFixed = s.engineFixed;
            armorFixed = s.armorFixed;
            fuel = Mathf.Clamp(s.fuel, 0, FuelUnits);
            RefreshShip();

            newWeaponsBought = Mathf.Max(0, s.newWeaponsBought);
            CopyCounts(s.upgradesBought, upgradesBought);
            CopyCounts(s.researchBought, researchBought);
            teamMk = Mathf.Max(1, s.teamMk);
            shipOreFromDay = s.shipOreFromDay;
            statsBought.Clear();
            for (int i = 0; s.statsBoughtStat != null && s.statsBoughtRarity != null && i < s.statsBoughtStat.Length && i < s.statsBoughtRarity.Length; i++)
                statsBought.Add(((StatId)s.statsBoughtStat[i], (CardRarity)s.statsBoughtRarity[i]));
            overloadShieldBuys = Mathf.Max(0, s.overloadShieldBuys);
            overloadDamageBuys = Mathf.Max(0, s.overloadDamageBuys);
            overloadShieldGiven = s.overloadShieldGiven;

            for (int i = 0; s.crateKinds != null && s.cratePositions != null && i < s.crateKinds.Length && i < s.cratePositions.Length; i++)
                if (Enum.IsDefined(typeof(CrateKind), s.crateKinds[i])) PlaceCrate(s.cratePositions[i], (CrateKind)s.crateKinds[i]);
            cratesOpened = Mathf.Max(0, s.cratesOpened);
            changedWeaponSaid = s.changedWeaponSaid;
            switchTaught = s.switchTaught;
            skillTaught = s.skillTaught;
            skillTeachPending = s.skillTeachPending;
            voidLineSaid = s.voidLineSaid;

            regionKills.Clear();
            regionKindKills.Clear();
            for (int i = 0; s.killRegion != null && s.killCount != null && i < s.killRegion.Length && i < s.killCount.Length; i++)
                regionKills[s.killRegion[i]] = s.killCount[i];
            for (int i = 0; s.kindRegion != null && s.kindKill != null && i < s.kindRegion.Length && i < s.kindKill.Length; i++)
            {
                if (!regionKindKills.TryGetValue(s.kindRegion[i], out var list)) regionKindKills[s.kindRegion[i]] = list = new List<EnemyKind>();
                list.Add((EnemyKind)s.kindKill[i]);
            }
        }

        static void CopyCounts(int[] from, int[] into)
        {
            for (int i = 0; i < into.Length; i++) into[i] = from != null && i < from.Length ? Mathf.Max(0, from[i]) : 0;
        }

        /// <summary>The void enchant back on the weapons that carried it, slot by slot.</summary>
        void RestoreVoidEnchants(WeaponSave[] saved, RunWeapon[] slots)
        {
            for (int i = 0; saved != null && i < saved.Length && i < slots.Length; i++)
                if (saved[i] != null && saved[i].voidEnchant && slots[i] != null) voidWeapons.Add(slots[i]);
        }

        /// <summary>The black hole and its side quest as they were: the hole's phase (a portal, or gone once the Nash Prophet fell), the
        /// trial's outcome, the first sight, the feeding and the day's timed check.</summary>
        void RestoreHoleQuest(StorySave s, RunSave run)
        {
            var map = WorldMap.Active;
            var hole = map ? map.CampaignHole : null;
            if (hole && s.hole != null) hole.Restore(hole.Centre, s.hole.feeds, s.hole.phase, s.hole.openedWave, s.hole.wavesLeft);
            if (map) map.RestoreRunState(run.trialWon, run.holeIntroDone || s.holeSeen);
            // The check first: making it new also clears the day's failure.
            RestoreHoleCheck(s);
            holeSeen = s.holeSeen;
            holeFailedToday = s.holeFailedToday;
            holeCheckDay = s.holeCheckDay;
            holeFed = Mathf.Max(0, s.holeFed);
            holeAwake = s.holeAwake;
            trailTarget = s.hasTrailTarget ? s.trailTarget : HoleCentre;
        }

        /// <summary>The day's timed check as it stood, played back through its own rules: a running check's hits and time, a passed one,
        /// or a failed one.</summary>
        void RestoreHoleCheck(StorySave s)
        {
            ResetHoleCheck();
            switch (s.checkState)
            {
                case HoleCheckState.Running when s.checkDealt > 0f:
                    holeCheck.Hit(s.checkDealt, s.checkRequired, s.checkSeconds);
                    if (s.checkElapsed > 0f) holeCheck.Tick(s.checkElapsed);
                    break;
                case HoleCheckState.Passed:
                    holeCheck.Hit(Mathf.Max(1f, s.checkRequired), s.checkRequired, s.checkSeconds);
                    break;
                case HoleCheckState.Failed:
                    holeCheck.Hit(1f, Mathf.Max(2f, s.checkRequired), s.checkSeconds);
                    holeCheck.Fail();
                    break;
            }
        }

        /// <summary>The saved phase begins through its own entry, as when the save was taken.</summary>
        void EnterSavedPhase(SavePhase phase)
        {
            switch (phase)
            {
                case SavePhase.Morning:
                    // The night ends as it did then: the dawn, then the day's morning (EndNight writes the save again on the way).
                    Stage = CampaignStage.Night;
                    EndNight(false);
                    break;
                case SavePhase.Afternoon:
                    ClearNight(false);
                    BeginDusk();
                    PlacePlayer(DoorPoint);
                    break;
                default:
                    gm.CampaignSetPhase(ArenaUiState.Night);
                    PlacePlayer(DoorPoint);
                    BeginNight();
                    break;
            }
        }
    }
}
