using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnomalyArena
{
    /// <summary>
    /// The run's side of the saves (ArenaSaves). A run takes the slot the title bound for it as it starts (ClaimSaveSlot: the open mode
    /// once its first weapon is taken, the story with its title card); a run with no slot writes nothing. The open mode saves just
    /// before every wave, the first and the Tyrant's included (SaveOpenRun); the story saves at the start of its phases
    /// (V2Campaign.Save.cs). Both keep the player alike (CaptureRun, ApplyRun). A continue loads the scene again with the saved map
    /// pinned (ArenaSaves.Continue, ReloadForSave); the scene that loads takes it on its title (StartPendingContinue) and starts the run
    /// there, without the title, the supply crate, the starting weapon or the first-time pages. When a run ends its slot keeps its last
    /// save; a won run's is marked cleared (RunEndedForSaves).
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The slot this run writes its saves into, -1 when it writes none; and the mode it was bound for.</summary>
        internal int RunSaveSlot { get; private set; } = -1;
        internal SaveMode RunSaveMode { get; private set; }

        // The GameManager that asked for a continue: its scene runs on for a moment before the reload, and must not take the continue.
        static GameManager continueRequester;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetContinueRequester() => continueRequester = null;

        /// <summary>A continue may start now: on the title, with no map pick reloading the scene.</summary>
        internal bool CanContinueSave => RunPhase == ArenaUiState.Title && !openModeStartPending;

        /// <summary>ArenaSaves.Continue: the scene loads again a moment later (the click's sound plays meanwhile), and the scene that loads
        /// takes the continue.</summary>
        internal void ReloadForSave()
        {
            continueRequester = this;
            _ = ReloadAfterUiFeedback();
        }

        /// <summary>A run of this mode starts from the title: it writes into the slot the title bound for it, if any.</summary>
        internal void ClaimSaveSlot(SaveMode mode)
        {
            RunSaveMode = mode;
            RunSaveSlot = ArenaSaves.ClaimBinding(mode);
        }

        /// <summary>The open mode's first weapon is taken and its first wave is about to begin: the run takes its slot and saves.</summary>
        void OpenRunBegins()
        {
            ClaimSaveSlot(SaveMode.Open);
            SaveOpenRun();
        }

        /// <summary>The open mode's save just before a wave or the Tyrant begins: after the heal and the stamina's refill, with no card
        /// pick banked and no armory offer waiting.</summary>
        void SaveOpenRun()
        {
            if (RunSaveSlot < 0 || RunSaveMode != SaveMode.Open || CampaignRunning) return;
            try
            {
                var save = new SaveData { run = CaptureRun() };
                var world = WorldMap.Active;
                if (world && world.holes != null)
                {
                    save.open.holes = new HoleSave[world.holes.Length];
                    for (int i = 0; i < world.holes.Length; i++) save.open.holes[i] = HoleSave.Of(world.holes[i]);
                }
                var summary = save.summary;
                summary.wave = ArenaSaves.OpenWave(waves.WaveIndex);
                summary.waveCount = waves.WaveCount + 1;
                summary.mapFamily = ArenaSaves.MapFamily(world ? world.biome : null);
                summary.weapons = ArenaSaves.WeaponsOf(runWeapons);
                if (ArenaSaves.Store(SaveMode.Open, RunSaveSlot, save)) Debug.Log($"[Saves] open mode, slot {RunSaveSlot + 1}: wave {summary.wave} of {summary.waveCount}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Saves] the open mode's save failed; the run goes on without it. {e}");
            }
        }

        /// <summary>The run ended: its slot keeps its last save. A won run's save is marked cleared, its summary showing the run as it
        /// finished (the story's day and ship, the open mode's last wave, the weapons held). The run writes no more. A failure here is
        /// logged and never stops the result screen that follows.</summary>
        void RunEndedForSaves()
        {
            if (RunSaveSlot < 0) return;
            var mode = RunSaveMode;
            int slot = RunSaveSlot;
            RunSaveSlot = -1;
            if (State != GameState.Won) return;
            try
            {
                ArenaSaves.MarkCleared(mode, slot, summary =>
                {
                    var world = WorldMap.Active;
                    summary.mapFamily = ArenaSaves.MapFamily(world ? world.biome : null);
                    summary.weapons = ArenaSaves.WeaponsOf(runWeapons);
                    if (mode == SaveMode.Story && campaign) campaign.FillFinishedSummary(summary);
                    else if (mode == SaveMode.Open)
                    {
                        summary.wave = ArenaSaves.OpenWave(waves.WaveIndex);
                        summary.waveCount = waves.WaveCount + 1;
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Saves] the won run's slot could not be marked cleared. {e}");
            }
        }

        // ───── What both modes keep ─────

        /// <summary>The player's progression, skills, weapons, HP and stamina, the waves fought and the map, as they are now; voidEnchanted
        /// (the story's) tells which weapons carry the void enchant.</summary>
        internal RunSave CaptureRun(Func<RunWeapon, bool> voidEnchanted = null)
        {
            var run = new RunSave();
            var p = progression;
            run.level = p.Level;
            run.xp = p.Xp;
            run.energy = p.Energy;
            run.energyUnlocked = p.EnergyUnlocked;
            run.voidEnergy = p.VoidEnergy;
            run.voidBadge = p.VoidBadge;
            run.stats = p.Stats.Copy();
            run.statCardPity = p.StatCardPity;
            run.draftWave = p.DraftWave;
            run.bankedPicks = p.BankedPickKinds;
            run.skills = new int[skills.Slots.Length];
            run.skillLevels = new int[skills.Slots.Length];
            for (int i = 0; i < skills.Slots.Length; i++)
            {
                var slot = skills.Slots[i];
                run.skills[i] = slot.Empty ? -1 : (int)slot.Id;
                run.skillLevels[i] = slot.Empty ? 1 : slot.Level;
            }
            run.skillCardPity = skills.SkillCardPity;
            run.hand = WeaponSaves(runWeapons.Hand, voidEnchanted);
            run.auto = WeaponSaves(runWeapons.Auto, voidEnchanted);
            run.activeHand = runWeapons.ActiveIndex;
            run.hp = player.Hp;
            run.stamina = player.stamina.Value;
            run.waveIndex = waves.WaveIndex;
            run.kills = waves.Kills;
            run.elapsed = Elapsed;
            var world = WorldMap.Active;
            run.mapIndex = world ? world.BiomeIndex : -1;
            run.trialWon = world && world.TrialWon;
            run.holeIntroDone = world && world.HoleIntroDone;
            return run;
        }

        static WeaponSave[] WeaponSaves(RunWeapon[] slots, Func<RunWeapon, bool> voidEnchanted)
        {
            var saved = new WeaponSave[slots.Length];
            for (int i = 0; i < slots.Length; i++) saved[i] = WeaponSave.Of(slots[i], slots[i] != null && voidEnchanted != null && voidEnchanted(slots[i]));
            return saved;
        }

        static RunWeapon[] Weapons(WeaponSave[] saved)
        {
            if (saved == null) return Array.Empty<RunWeapon>();
            var weapons = new RunWeapon[saved.Length];
            for (int i = 0; i < saved.Length; i++) weapons[i] = saved[i]?.ToWeapon();
            return weapons;
        }

        /// <summary>A continued save's player and run: the skills learned again (before the progression, so the energy ends as saved and
        /// no skill counts it twice), the progression and its stats, the weapons with full clips, the stats pushed to the player, then the
        /// HP and the stamina; the waves fought and the run's time.</summary>
        internal void ApplyRun(RunSave run)
        {
            skills.ResetRun();
            for (int i = 0; run.skills != null && i < run.skills.Length && i < skills.Slots.Length; i++)
                if (run.skills[i] >= 0 && Enum.IsDefined(typeof(SkillId), run.skills[i]))
                    skills.Learn((SkillId)run.skills[i], i, run.skillLevels != null && i < run.skillLevels.Length ? run.skillLevels[i] : 1);
            skills.RestoreCardPity(run.skillCardPity);
            progression.Restore(run.level, run.xp, run.stats, run.energy, run.energyUnlocked, run.voidEnergy, run.voidBadge, run.statCardPity,
                run.draftWave, run.bankedPicks);
            runWeapons.RestoreSlots(Weapons(run.hand), Weapons(run.auto), run.activeHand);
            ApplyStats();
            player.RestoreHp(run.hp > 0f ? run.hp : player.maxHp);
            player.stamina.Restore(run.stamina);
            waves.RestoreProgress(run.waveIndex, run.kills);
            Elapsed = Mathf.Max(0f, run.elapsed);
            UpdatePenetrationEligibility();
        }

        // ───── A continue ─────

        /// <summary>After a continue's reload: the run starts once the title's text is ready (and, for the story, once the campaign has its
        /// screen), so the title never shows. A continue that cannot start leaves the title as it is; one that fails half way loads the
        /// scene again, back on a clean title.</summary>
        void StartPendingContinue()
        {
            if (RunPhase != ArenaUiState.Title || !ArenaText.Ready || ReferenceEquals(continueRequester, this)) return;
            if (!ArenaSaves.ContinuePending(out var waiting)) return;
            if (waiting == SaveMode.Story && campaign && !campaign.ReadyToContinue) return;
            if (!ArenaSaves.TakeContinue(out var mode, out int slot, out var save)) return;
            continueRequester = null;
            RunSaveMode = mode;
            RunSaveSlot = slot;
            bool started;
            try
            {
                started = mode == SaveMode.Story ? campaign && campaign.BeginFromSave(save) : BeginOpenFromSave(save);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Saves] continuing {mode} slot {slot + 1} failed; back to the title. {e}");
                RunSaveSlot = -1;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }
            if (started) return;
            RunSaveSlot = -1;
            Debug.LogWarning($"[Saves] {mode} slot {slot + 1} could not be continued in this scene");
        }

        /// <summary>A continued open-mode save, on the map it was made on: the black holes as they were, the player's run, then the saved
        /// wave (or the Tyrant) begins as BeginNextV2Wave begins it, its save written again there.</summary>
        bool BeginOpenFromSave(SaveData save)
        {
            var run = save.run;
            var world = WorldMap.Active;
            if (world)
            {
                if (run.mapIndex >= 0 && world.BiomeIndex != run.mapIndex) Debug.LogWarning($"[Saves] the save was made on map {run.mapIndex}; map {world.BiomeIndex} is laid");
                var holes = save.open.holes;
                for (int i = 0; holes != null && world.holes != null && i < holes.Length && i < world.holes.Length; i++)
                    if (world.holes[i] && holes[i] != null) world.holes[i].Restore(holes[i].position, holes[i].feeds, holes[i].phase, holes[i].openedWave, holes[i].wavesLeft);
                world.RestoreRunState(run.trialWon, run.holeIntroDone);
            }
            State = GameState.Playing;
            ApplyRun(run);
            RunAnalytics.RunStarted(this);
            BeginNextV2Wave(false);
            return true;
        }
    }
}
