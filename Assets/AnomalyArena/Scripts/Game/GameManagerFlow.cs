using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace AnomalyArena
{
    /// <summary>
    /// Run flow beside GameManagerV2.cs. The keyboard answers only to the keys the game shows: Space, Enter and keypad
    /// Enter only close the weapon card, which names them (document 2.2, 10-27), and the UI has no submit action, so every other step is a
    /// click or a key printed on screen. A Void Trial won mid-wave hands the wave back after a short freeze in which the player absorbs
    /// the void's power (document 12.5).
    /// </summary>
    public partial class GameManager
    {
        // ───── Keys: only what the game shows ─────

        /// <summary>The menu keys V2Update reads and the KeyCode PressKey knows each one by. E (standing on an open portal) is read by
        /// WorldMap; WASD, Shift and the mouse by PlayerController.</summary>
        static readonly (Key key, KeyCode code)[] ShownKeys =
        {
            (Key.Escape, KeyCode.Escape), (Key.T, KeyCode.T), (Key.Tab, KeyCode.Tab), (Key.R, KeyCode.R),
            (Key.Digit1, KeyCode.Alpha1), (Key.Digit2, KeyCode.Alpha2), (Key.Digit3, KeyCode.Alpha3),
            // Document 2.2 (10-27): the weapon card also closes with Space or Enter, and its hint names them; nothing else reads them.
            (Key.Space, KeyCode.Space), (Key.Enter, KeyCode.Return), (Key.NumpadEnter, KeyCode.KeypadEnter),
        };

        /// <summary>The keys the menus answer to (tests check that Space and Enter act on the weapon card only).</summary>
        public static IEnumerable<KeyCode> ShownKeyCodes
        {
            get { foreach (var (_, code) in ShownKeys) yield return code; }
        }

        void ReadShownKeys(Keyboard keyboard)
        {
            if (V2Tutorial.HoldsInput || ArenaSettingsPanel.HoldsInput || hud && hud.v2Hud && hud.v2Hud.GetComponent<ArenaCodex>() is { IsOpen: true }) return;
            bool digitRead = false;
            foreach (var (key, code) in ShownKeys)
            {
                // A key the player moved in the settings is read where they put it (ArenaKeys: the skill keys and T); the fixed
                // menu keys are read as they are.
                if (!keyboard[ArenaKeys.Physical(key)].wasPressedThisFrame) continue;
                // One digit a frame: one card or one skill per press, as before.
                bool digit = code is >= KeyCode.Alpha1 and <= KeyCode.Alpha3;
                if (digit && digitRead) continue;
                digitRead |= digit;
                PressKey(code);
            }
        }

        /// <summary>What a key does in the current phase, the path the keyboard takes (open to tests). A key the game does not show
        /// does nothing in any phase, the black-hole cutscene included; Space, Enter and keypad Enter act on the weapon card only.</summary>
        public void PressKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Space:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    // Document 2.2 (10-27): click anywhere, Space or Enter closes the weapon card (after the same input lock as a click).
                    if (RunPhase == ArenaUiState.WeaponIntro) ExecuteUiCommand(ArenaUiCommand.DismissWeaponIntro);
                    break;
                case KeyCode.Escape:
                    if (RunPhase == ArenaUiState.Paused) ExecuteUiCommand(ArenaUiCommand.Resume);
                    else if (CanPause) ExecuteUiCommand(ArenaUiCommand.Pause);
                    else if (RunPhase == ArenaUiState.LevelUp) ExecuteUiCommand(ArenaUiCommand.Cancel); // pick later
                    // The open mode's map pick on the title: Esc goes back to the title's entries, as its back button does.
                    else if (RunPhase == ArenaUiState.Title && hud && hud.v2Hud) hud.v2Hud.CloseMapSelect();
                    break;
                case KeyCode.T:
                    // A banked pick mid-wave, which the HUD shows. The title's art style has no key: no screen shows one.
                    if (RunPhase == ArenaUiState.WaveActive && MenuInputReady && progression.BankedDrafts > 0) OpenLevelUp();
                    break;
                case KeyCode.Tab:
                    // Puts the build panel away (walk the empty arena, step onto a portal) and brings it back.
                    if (CanToggleBuildWalk) ExecuteUiCommand(ArenaUiCommand.ToggleBuildWalk);
                    break;
                case KeyCode.R:
                    if (RunPhase == ArenaUiState.Result) ExecuteUiCommand(ArenaUiCommand.Restart);
                    else if (RunPhase is (ArenaUiState.Draft or ArenaUiState.LevelUp) && !BuildWalking) ExecuteUiCommand(ArenaUiCommand.Reroll);
                    break;
                case KeyCode.Alpha1:
                case KeyCode.Alpha2:
                case KeyCode.Alpha3:
                    ChooseByNumber(key - KeyCode.Alpha1);
                    break;
            }
        }

        // ───── What the bottom row flashes for ─────

        /// <summary>A weapon has just attacked: its slot's count moves at once, so the slot flashes with the shot and not at the next 10 Hz refresh.</summary>
        public void NoteWeaponAttack(RunWeapon weapon)
        {
            if (!runWeapons || weapon == null) return;
            for (int i = 0; i < runWeapons.Hand.Length && i < snapshot.handSlots.Length; i++)
                if (runWeapons.Hand[i] == weapon) snapshot.handSlots[i].pulses++;
            for (int i = 0; i < runWeapons.Auto.Length && i < snapshot.autoSlots.Length; i++)
                if (runWeapons.Auto[i] == weapon) snapshot.autoSlots[i].pulses++;
        }

        void NoteSkillCast(SkillId id)
        {
            for (int i = 0; i < skills.Slots.Length && i < snapshot.skillSlots.Length; i++)
                if (!skills.Slots[i].Empty && skills.Slots[i].Id == id)
                {
                    snapshot.skillSlots[i].pulses++;
                    // The clock starts from the cooldown just set, not from what the last 10 Hz refresh left in the slot.
                    snapshot.skillSlots[i].cooldown01 = skills.Slots[i].CooldownLeft / Mathf.Max(.01f, skills.Slots[i].Cooldown);
                }
        }

        /// <summary>Tab walks the arena only in a real build phase. The greater void reward is not one: after a trial won mid-wave the live
        /// wave waits under it, and walking there would run its clock and spawns before the frozen return (document 3.2: the reward freezes
        /// everything). Its screen does not show Tab either.</summary>
        bool CanToggleBuildWalk => RunPhase is (ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready) && !greaterDraft;

        /// <summary>Esc pauses wherever the run's clock runs: in combat, walking the build phase and, since the build phase's time flows,
        /// with one of its panels open or while its supply crate comes down. Screens that already
        /// freeze the run (the crate before the first weapon, a weapon card, a mid-wave pick, the greater void reward) do not pause.</summary>
        bool CanPause => CanPlayerMove || BuildTimeFlows;

        /// <summary>The build phase was being walked (its panel put away) when the game paused, so Resume walks on.</summary>
        bool resumeWalking;

        /// <summary>1 / 2 / 3: a skill in combat, otherwise that choice on the open panel.</summary>
        void ChooseByNumber(int choice)
        {
            if (!MenuInputReady || BuildWalking) return;
            if (CanUseCombatInput) skills.TryCast(choice);
            else if (RunPhase == ArenaUiState.Loadout) ExecuteUiCommand(ArenaUiCommand.ChooseLoadout, choice);
            else if (RunPhase == ArenaUiState.Armory && choice < runWeapons.Offers.Length) ExecuteUiCommand(ArenaUiCommand.TakeArmory, choice);
            else if (RunPhase is ArenaUiState.Draft or ArenaUiState.LevelUp) ExecuteUiCommand(ArenaUiCommand.ChooseCard, choice);
            else if (RunPhase == ArenaUiState.ReplaceWeapon) ExecuteUiCommand(ArenaUiCommand.ReplaceWeapon, choice);
            else if (RunPhase == ArenaUiState.ReplaceSkill) ExecuteUiCommand(ArenaUiCommand.ReplaceSkill, choice);
        }

        /// <summary>Enter must not press a selected button either: the UI module's submit action presses whichever button is selected (Enter
        /// on a keyboard, the south button on a gamepad), so the run drops it. Buttons still answer clicks; the weapon card reads Space and
        /// Enter itself (PressKey).</summary>
        static void DisableUiSubmit()
        {
            foreach (var module in FindObjectsByType<InputSystemUIInputModule>(FindObjectsInactive.Include)) module.submit = null;
        }

        // ───── Back from a Void Trial won mid-wave ─────

        [Min(0f), Tooltip("Seconds the battlefield and the player stay frozen after a Void Trial won mid-wave, while the player absorbs the void's power and can see where the enemies are (document 12.5). 0 resumes the wave at once.")]
        public float trialReturnFreeze = 2.5f;
        float trialReturnLeft, absorbClock, nextMoteAt, nextRingAt;
        bool absorbBurst;

        // Shape of the absorb animation, drawn with the saved FxPool on unscaled time (the game clock is stopped meanwhile).
        const float AbsorbRadius = 4.5f; // cells from the player where the void motes appear
        const float AbsorbMoteLife = .55f; // seconds a mote takes to reach the player
        const float AbsorbMoteInterval = .04f; // seconds between two pairs of motes
        const float AbsorbRingInterval = .3f; // seconds between the rings closing in
        const float AbsorbBurstLead = .45f; // the gathered power flashes out this long before the wave resumes
        const float FxPoolDrag = 3f; // FxPool slows a moving quad by e^(-3t)

        /// <summary>Freezes the battlefield and the player (the clock stops and no phase input reaches them), plays the absorb animation,
        /// then resumes the wave; the trial's extra level pops its pick up as soon as it does.</summary>
        void BeginTrialReturn()
        {
            trialReturnLeft = Mathf.Max(0f, trialReturnFreeze);
            if (trialReturnLeft <= 0f) { WorldMap.Active?.RemoveHolesAfterTrial(); SetPhase(ArenaUiState.WaveActive); return; }
            SetPhase(ArenaUiState.TrialReturn);
            absorbClock = nextMoteAt = nextRingAt = 0f;
            absorbBurst = false;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.TimeHeld, player.Position);
            // V2 day campaign (since 2026-11-10): the win pays the void currencies, so the banner names them instead of the classic
            // run's absorbed power (no enemy is on the map to look for either).
            if (CampaignRunning && campaign.rules)
                hud?.Banner(ArenaText.Pair("虚空的馈赠", "THE GIFT OF THE VOID"),
                    ArenaText.Pair($"虚空能量 +{campaign.rules.voidEnergyReward} · 虚空合金 +{campaign.rules.voidAlloyReward}　回基地的能量研究区研习",
                        $"Void energy +{campaign.rules.voidEnergyReward} · Void alloy +{campaign.rules.voidAlloyReward}   Study them at the base's Energy Lab"));
            else
                hud?.Banner(ArenaText.Pair("吸收虚空之力", "ABSORBING THE VOID"),
                    ArenaText.Pair($"战场冻结 {trialReturnFreeze:0.#} 秒，趁现在看清敌人的位置。", $"The battlefield is frozen for {trialReturnFreeze:0.#} s. Use it to see where the enemies are."));
        }

        void TickTrialReturn()
        {
            float dt = Time.unscaledDeltaTime;
            absorbClock += dt;
            trialReturnLeft -= dt;
            DrawAbsorb();
            // The holes stay in view through the freeze and turn into ordinary ground as the wave resumes.
            if (trialReturnLeft <= 0f) { WorldMap.Active?.RemoveHolesAfterTrial(); SetPhase(ArenaUiState.WaveActive); }
        }

        /// <summary>Void motes stream in from a ring around the player while smaller and smaller rings close on them; at the end the
        /// gathered power flashes out once. Saved FxPool quads only, no new art.</summary>
        void DrawAbsorb()
        {
            var pool = FxPool.Active;
            if (!pool || !player) return;
            Color tint = ElementCombat.ColorFor(ElementKind.Void);
            Vector3 centre = Query.AtCastHeight(player.Position);
            float inflow = Mathf.Max(0f, trialReturnFreeze - AbsorbBurstLead);
            if (absorbClock < inflow)
            {
                // Launched so that FxPool's drag stops each mote on the player just as it fades out.
                float speed = FxPoolDrag * AbsorbRadius / (1f - Mathf.Exp(-FxPoolDrag * AbsorbMoteLife));
                for (int pairs = 0; absorbClock >= nextMoteAt && pairs < 3; pairs++)
                {
                    nextMoteAt += AbsorbMoteInterval;
                    for (int i = 0; i < 2; i++)
                    {
                        float angle = Random.Range(0f, Mathf.PI * 2f);
                        var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                        if (V5Vfx.On) V5Vfx.I.AbsorbMote(centre + dir * AbsorbRadius + Vector3.up * Random.Range(-.3f, .5f), -dir * AbsorbRadius / AbsorbMoteLife, AbsorbMoteLife);
                        else pool.Emit(centre + dir * AbsorbRadius + Vector3.up * Random.Range(-.3f, .5f), new Color(tint.r, tint.g, tint.b, .9f),
                            Random.Range(.3f, .5f), AbsorbMoteLife, -dir * speed);
                    }
                }
                if (absorbClock >= nextMoteAt) nextMoteAt = absorbClock + AbsorbMoteInterval; // after a hitch, skip instead of flooding the pool
                if (absorbClock >= nextRingAt)
                {
                    nextRingAt += AbsorbRingInterval;
                    float closing = Mathf.Clamp01(absorbClock / Mathf.Max(.01f, inflow));
                    Fx.Ring(player.Position, Mathf.Lerp(AbsorbRadius, 1f, closing), tint, .35f);
                    Fx.Pop(centre, tint, Mathf.Lerp(1f, 2.4f, closing));
                }
                return;
            }
            if (absorbBurst) return;
            absorbBurst = true;
            Fx.Pop(centre, tint, 3.2f);
            Fx.Ring(player.Position, AbsorbRadius * .8f, tint, .5f);
            Fx.Burst(centre, Vector3.zero, tint, 8, 9f, 360f, .25f);
            Shake(.25f);
            // A real win is one extra level: the level-up sound the XP path plays (the enemy lab's practice trial grants none).
            if (WorldMap.Active && WorldMap.Active.TrialWon) V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, player.Position);
        }
    }
}
