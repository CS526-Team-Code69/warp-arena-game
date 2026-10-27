using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace AnomalyArena
{

    public partial class GameManager
    {

        static readonly (Key key, KeyCode code)[] ShownKeys =
        {
            (Key.Escape, KeyCode.Escape), (Key.T, KeyCode.T), (Key.Tab, KeyCode.Tab), (Key.R, KeyCode.R),
            (Key.Digit1, KeyCode.Alpha1), (Key.Digit2, KeyCode.Alpha2), (Key.Digit3, KeyCode.Alpha3),

            (Key.Space, KeyCode.Space), (Key.Enter, KeyCode.Return), (Key.NumpadEnter, KeyCode.KeypadEnter),
        };

        public static IEnumerable<KeyCode> ShownKeyCodes
        {
            get { foreach (var (_, code) in ShownKeys) yield return code; }
        }

        void ReadShownKeys(Keyboard keyboard)
        {
            bool digitRead = false;
            foreach (var (key, code) in ShownKeys)
            {
                if (!keyboard[key].wasPressedThisFrame) continue;

                bool digit = code is >= KeyCode.Alpha1 and <= KeyCode.Alpha3;
                if (digit && digitRead) continue;
                digitRead |= digit;
                PressKey(code);
            }
        }

        public void PressKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Space:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:

                    if (RunPhase == ArenaUiState.WeaponIntro) ExecuteUiCommand(ArenaUiCommand.DismissWeaponIntro);
                    break;
                case KeyCode.Escape:
                    if (RunPhase == ArenaUiState.Paused) ExecuteUiCommand(ArenaUiCommand.Resume);
                    else if (CanPause) ExecuteUiCommand(ArenaUiCommand.Pause);
                    else if (RunPhase == ArenaUiState.LevelUp) ExecuteUiCommand(ArenaUiCommand.Cancel);              
                    break;
                case KeyCode.T:

                    if (RunPhase == ArenaUiState.WaveActive && MenuInputReady && progression.BankedDrafts > 0) OpenLevelUp();
                    break;
                case KeyCode.Tab:

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

        bool CanToggleBuildWalk => RunPhase is (ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready) && !greaterDraft;

        bool CanPause => CanPlayerMove || BuildTimeFlows;

        bool resumeWalking;

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

        static void DisableUiSubmit()
        {
            foreach (var module in FindObjectsByType<InputSystemUIInputModule>(FindObjectsInactive.Include)) module.submit = null;
        }

        [Min(0f), Tooltip("Seconds the battlefield and the player stay frozen after a Void Trial won mid-wave, while the player absorbs the void's power and can see where the enemies are (document 12.5). 0 resumes the wave at once.")]
        public float trialReturnFreeze = 2.5f;
        float trialReturnLeft, absorbClock, nextMoteAt, nextRingAt;
        bool absorbBurst;

        const float AbsorbRadius = 4.5f;                                                     
        const float AbsorbMoteLife = .55f;                                            
        const float AbsorbMoteInterval = .04f;                                      
        const float AbsorbRingInterval = .3f;                                        
        const float AbsorbBurstLead = .45f;                                                                    
        const float FxPoolDrag = 3f;                                         

        void BeginTrialReturn()
        {
            trialReturnLeft = Mathf.Max(0f, trialReturnFreeze);
            if (trialReturnLeft <= 0f) { WorldMap.Active?.RemoveHolesAfterTrial(); SetPhase(ArenaUiState.WaveActive); return; }
            SetPhase(ArenaUiState.TrialReturn);
            absorbClock = nextMoteAt = nextRingAt = 0f;
            absorbBurst = false;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.TimeHeld, player.Position);
            hud?.Banner(ArenaText.Pair("ABSORBING THE VOID","ABSORBING THE VOID"),
                ArenaText.Pair($"The battlefield is frozen for {trialReturnFreeze:0.#} s. Use it to see where the enemies are.",$"The battlefield is frozen for {trialReturnFreeze:0.#} s. Use it to see where the enemies are."));
        }

        void TickTrialReturn()
        {
            float dt = Time.unscaledDeltaTime;
            absorbClock += dt;
            trialReturnLeft -= dt;
            DrawAbsorb();

            if (trialReturnLeft <= 0f) { WorldMap.Active?.RemoveHolesAfterTrial(); SetPhase(ArenaUiState.WaveActive); }
        }

        void DrawAbsorb()
        {
            var pool = FxPool.Active;
            if (!pool || !player) return;
            Color tint = ElementCombat.ColorFor(ElementKind.Void);
            Vector3 centre = Query.AtCastHeight(player.Position);
            float inflow = Mathf.Max(0f, trialReturnFreeze - AbsorbBurstLead);
            if (absorbClock < inflow)
            {

                float speed = FxPoolDrag * AbsorbRadius / (1f - Mathf.Exp(-FxPoolDrag * AbsorbMoteLife));
                for (int pairs = 0; absorbClock >= nextMoteAt && pairs < 3; pairs++)
                {
                    nextMoteAt += AbsorbMoteInterval;
                    for (int i = 0; i < 2; i++)
                    {
                        float angle = Random.Range(0f, Mathf.PI * 2f);
                        var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                        pool.Emit(centre + dir * AbsorbRadius + Vector3.up * Random.Range(-.3f, .5f), new Color(tint.r, tint.g, tint.b, .9f),
                            Random.Range(.3f, .5f), AbsorbMoteLife, -dir * speed);
                    }
                }
                if (absorbClock >= nextMoteAt) nextMoteAt = absorbClock + AbsorbMoteInterval;                                                    
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

            if (WorldMap.Active && WorldMap.Active.TrialWon) V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, player.Position);
        }
    }
}
