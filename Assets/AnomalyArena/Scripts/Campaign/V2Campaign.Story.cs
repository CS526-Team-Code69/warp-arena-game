using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Story and the base visits. Day 0 (V2 doc section 2): a black "Day 0" card, then from the crash site west of the base (since
    /// 2026-11-06 as far from it as on the classic arena, not at the large map's edge) the walk to the base, taught step by step with the
    /// spotlight since 2026-11-10 (V2Campaign.Tutorial.cs), ends at the wreck and the base, whose system tells the main goal (Arrival). The
    /// memory plays as Day 0 passes into night; the tutorial night teaches the lamp and dark matter, and (having no clock since
    /// 2026-11-06) sends the player back to the base door to wait for dawn. The memory, the failed ignition before the Tyrant (the
    /// existing final boss) and the ending that sends the ship home are story cutscenes staged in whitebox (V2Cutscene), played by
    /// PlayCutscene.
    /// The lines are the drafts of the asset plan's appendix A (to be rewritten); lines without a recording show as subtitles
    /// only (D9: voice in English, subtitles per edition). T07 to T09 and D02 (2026-11-10) have none yet.
    /// </summary>
    public sealed partial class V2Campaign
    {
        // New lines go at the end: the voice table (CampaignArt.voices) names its takes by these ids.
        public enum Line { T01, T02, T03, T04, T05, T06, M01, S01, S02, S03, S04, S05, S06, S07, S08, S09, S10, F01, F02, F03, E00, E01, E02, E03, C01, C02, C03, C04, C05, D01, K01, N01, N02, N03, B01, T07, T08, T09, D02, T10, C06, C07, C08, C09, F04, F05 }

        // Each line is an ArenaText.Pair, read when it is said: the English edition keeps only the English side.
        static (bool system, string text) LineText(Line id) => id switch
        {
            Line.T01 => (false, ArenaText.Pair("这是哪……先四处看看。", "Where am I... Let me look around first.")),
            // T02 says to sprint, which Day 0 teaches only after the ore since 2026-11-10: T07 is said there instead. T02 keeps its
            // recording for when the lines are rewritten.
            Line.T02 => (false, ArenaText.Pair("角落里有东西在发光，冲过去看看。", "Something's glowing in that corner. Let me sprint over.")),
            Line.T07 => (false, ArenaText.Pair("角落里有东西在发光，过去看看。", "Something's glowing in that corner. Let me take a look.")),
            Line.T08 => (false, ArenaText.Pair("一个武器箱……居然没摔坏。", "A weapon crate... still in one piece.")),
            Line.T09 => (false, ArenaText.Pair("这东西不属于这里。", "This doesn't belong here.")),
            Line.D02 => (false, ArenaText.Pair("得去收集些矿物，顺便看看还有没有完好的武器箱。", "I need to gather some ore, and see if any weapon crates survived.")),
            // After the first weapon is taken from a crate (2026-11-10): only then does the game say the weapons here have changed.
            Line.T10 => (false, ArenaText.Pair("这个武器已经不一样了，应该是被异星能量改变了。我得注意这把武器的使用方式。", "This weapon isn't what it was. The alien energy must have changed it. I'd better watch how I use it.")),
            Line.T03 => (false, ArenaText.Pair("这块矿石……对修复飞船有大用。", "This ore... it'll go a long way toward fixing the ship.")),
            Line.T04 => (false, ArenaText.Pair("有东西过来了，先解决它们。", "Something's coming. Deal with them first.")),
            Line.T05 => (false, ArenaText.Pair("这东西对我之后的生存应该有用，带回基地研究一下。", "This should help me survive later. I'll take it back to base and study it.")),
            Line.T06 => (false, ArenaText.Pair("基地还在运转，先回去。", "The base is still running. Head back.")),
            Line.M01 => (false, ArenaText.Pair("飞船……烂成这样了。", "The ship... it's wrecked.")),
            Line.S01 => (true, ArenaText.Pair("基地系统已自动部署。飞船严重受损。修复引擎与装甲，并合成反物质燃料，即可返航。", "Base system deployed automatically. Ship heavily damaged. Repair the engine and armor and synthesize antimatter fuel to return.")),
            Line.S02 => (true, ArenaText.Pair("夜晚降临。无光区域无法视物，请携带光源行动。", "Night has fallen. Unlit areas are pitch dark. Carry a light source.")),
            Line.S03 => (true, ArenaText.Pair("警告：该区域影子怪物过多，光源被破坏。", "Warning: too many shadows in this area. A light source was destroyed.")),
            Line.S04 => (true, ArenaText.Pair("基地外壁受损，{0}停用，请在夜间修复。", "Hull damaged. {0} is offline. Repair it at night.")),
            Line.S05 => (true, ArenaText.Pair("功能区修复完成。", "Room repaired.")),
            Line.S06 => (true, ArenaText.Pair("基地护盾已恢复。", "Base shield restored.")),
            Line.S07 => (true, ArenaText.Pair("天亮了。敌袭来临，守住基地。", "Daybreak. Enemies incoming. Defend the base.")),
            Line.S08 => (true, ArenaText.Pair("飞船引擎修复完成。", "Ship engine repaired.")),
            Line.S09 => (true, ArenaText.Pair("飞船装甲修复完成。", "Ship armor repaired.")),
            Line.S10 => (true, ArenaText.Pair("反物质燃料已满。启动点火程序。", "Antimatter fuel full. Starting ignition sequence.")),
            Line.F01 => (true, ArenaText.Pair("点火失败。燃料无法激活。", "Ignition failed. Fuel cannot be activated.")),
            Line.F02 => (true, ArenaText.Pair("缺少暗物质核心。", "Dark matter core missing.")),
            Line.F03 => (false, ArenaText.Pair("那是……什么东西？", "What... is that?")),
            Line.E00 => (false, ArenaText.Pair("它身上掉出来的……这就是暗物质核心？", "What fell out of it... is that the dark matter core?")),
            Line.E01 => (true, ArenaText.Pair("暗物质核心已安装。点火程序启动。", "Dark matter core installed. Ignition sequence started.")),
            Line.E02 => (true, ArenaText.Pair("跃迁坐标锁定：地球。", "Jump coordinates locked: Earth.")),
            Line.E03 => (false, ArenaText.Pair("该回家了。", "Time to go home.")),
            Line.C01 => (false, ArenaText.Pair("我是一名星际战士，奉命前往银河系作战。", "I'm an interstellar soldier, sent to fight across the galaxy.")),
            Line.C02 => (false, ArenaText.Pair("那次虫洞跃迁，我们撞上了异星乱流。", "During the wormhole jump, we hit alien turbulence.")),
            Line.C03 => (false, ArenaText.Pair("醒来时，我已经落在这颗星球上，飞船烂了。", "When I woke, I'd crashed on this planet. The ship was wrecked.")),
            Line.C04 => (false, ArenaText.Pair("飞船自动部署了基地系统。它就是为这种意外准备的。", "The ship deployed its base system on its own. It was built for accidents like this.")),
            Line.C05 => (false, ArenaText.Pair("它会帮我离开这里。", "It will help me get out of here.")),
            // The story cutscenes' lines that have no recording: the memory's M1, M2, M3 and M6, and the failed ignition's L3 and L4.
            Line.C06 => (false, ArenaText.Pair("人类遭到了异星入侵。", "Humanity was invaded by aliens.")),
            Line.C07 => (false, ArenaText.Pair("为了抗战，人类开始了大征兵。", "To fight back, humanity called a great conscription.")),
            Line.C08 => (false, ArenaText.Pair("我叫 Felix，是外婆把我带大的。", "I'm Felix. My grandmother raised me.")),
            Line.C09 => (false, ArenaText.Pair("部队的其他人，不知所踪。", "The rest of my unit went missing.")),
            Line.F04 => (true, ArenaText.Pair("警告：暗物质能量波动正在扩散。", "Warning: dark matter energy surge spreading.")),
            Line.F05 => (false, ArenaText.Pair("点火程序完成不了……它需要一股极其庞大的能量来驱动。", "The ignition can't finish... it needs an enormous source of energy.")),
            // Since 2026-11-06 the screen calls this stage the afternoon; the English line stays as recorded.
            Line.D01 => (true, ArenaText.Pair("下午。怪物在夜里会被异星力量增强，趁天黑前补充物资。", "Dusk. Alien power strengthens the monsters at night. Gather what you can before dark.")),
            Line.K01 => (true, ArenaText.Pair("生命体征消失。已将你带回基地，今晚的收获全部遗失。", "Vital signs lost. You have been brought back to the base. Tonight's haul is lost.")),
            Line.N01 => (true, ArenaText.Pair($"按 {ArenaKeys.LampKey} 放下一盏矿灯。灯光里才能采集。", $"Press {ArenaKeys.LampKey} to set down a mining lamp. You can only mine in its light.")),
            Line.N02 => (true, ArenaText.Pair($"暗物质只在夜里出现。站在灯光里按住 {ArenaKeys.InteractKey} 采集。", $"Dark matter only shows at night. Hold {ArenaKeys.InteractKey} in the lamp light to mine it.")),
            Line.N03 => (true, ArenaText.Pair("影子怪物怕光。上午杀敌越多的地方，夜里影子越多。回基地门口按 " + ArenaKeys.InteractKey + "，选择等待天亮。", "Shadows fear the light. Where you killed the most by day, more shadows walk at night. Press " + ArenaKeys.InteractKey + " at the base door to wait for dawn.")),
            Line.B01 => (true, ArenaText.Pair("基地储备已开放。用合金和能量建造武装区与能量研究区。", "Base reserves released. Build the Armament Bay and the Energy Lab with alloy and energy.")),
            _ => (false, "")
        };

        public static string Speaker(bool system) => system ? ArenaText.Pair("基地系统", "BASE SYSTEM") : ArenaText.Pair("我", "ME");

        void Say(Line id, params object[] args) => SayWith(id, VoiceClip(id), args);

        /// <summary>S04 names the broken room: its voice is the take recorded for that room.</summary>
        void SayRoomOffline(CampaignRoom room) => SayWith(Line.S04, VoiceClip(Line.S04, room), new object[] { RoomName(room) });

        void SayWith(Line id, AudioClip voice, object[] args)
        {
            if (!ui) return;
            var line = LineText(id);
            if (string.IsNullOrEmpty(line.text)) return;
            string text = line.text;
            if (args != null && args.Length > 0) text = string.Format(text, args);
            PlayVoice(voice);
            // The speaking itself lasts as long as the recording (a line without one: a short while); the subtitle stays up longer to be read.
            speechUntil = Time.unscaledTime + (voice ? voice.length + .3f : Mathf.Min(3.5f, 1.2f + .06f * text.Length));
            ui.Subtitle(Speaker(line.system), text, line.system, Mathf.Max(2.2f + .085f * text.Length, voice ? voice.length + .4f : 0f));
        }

        /// <summary>Says a line once the subtitle on screen now has run out (at most maxWait seconds later), if still() holds then: a line
        /// that follows another said in the same moment (the afternoon's own after the base's) does not cut it short.</summary>
        void SayWhenQuiet(Line id, System.Func<bool> still, float maxWait = 10f) => StartCoroutine(SayingWhenQuiet(id, still, maxWait));

        IEnumerator SayingWhenQuiet(Line id, System.Func<bool> still, float maxWait)
        {
            quietLinesWaiting++;
            // One frame first: the line it follows may be said later in the same frame.
            yield return null;
            float giveUp = Time.realtimeSinceStartup + maxWait;
            while (ui && ui.SubtitleShowing && Time.realtimeSinceStartup < giveUp) yield return null;
            quietLinesWaiting = Mathf.Max(0, quietLinesWaiting - 1);
            if (Running && (still == null || still())) Say(id);
        }

        int quietLinesWaiting;
        float speechUntil;
        /// <summary>Someone is still talking: a line is being spoken, or one waits for the subtitle on screen to end. Day 0 holds its next
        /// step back meanwhile, so that what the hero says comes before what the player then does.</summary>
        bool SpeechBusy => quietLinesWaiting > 0 || Time.unscaledTime < speechUntil;

        // ───── Voice (D9: English in both editions; lines without a recording show the subtitle only) ─────

        AudioSource voiceSource, cgSoundSource;
        Dictionary<string, AudioClip> voiceClips;

        AudioClip VoiceClip(Line id, CampaignRoom? room = null)
        {
            if (!art) return null;
            if (id == Line.S04 && room.HasValue)
            {
                var takes = art.roomOfflineVoices;
                int i = (int)room.Value;
                return takes != null && i < takes.Length ? takes[i] : null;
            }
            if (voiceClips == null)
            {
                voiceClips = new Dictionary<string, AudioClip>();
                if (art.voices != null)
                    foreach (var v in art.voices)
                        if (v.clip && !string.IsNullOrEmpty(v.line)) voiceClips[v.line] = v.clip;
            }
            return voiceClips.TryGetValue(id.ToString(), out var clip) ? clip : null;
        }

        /// <summary>How long a scripted moment waits after a line: its recording's length when it has one, at least the planned time.</summary>
        float LineSeconds(Line id, float planned)
        {
            var clip = VoiceClip(id);
            return clip ? Mathf.Max(planned, clip.length + .25f) : planned;
        }

        static AudioSource Source2D(GameObject host)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        float AudioGain => V3WorldAudio.Instance ? V3WorldAudio.Instance.masterGain : 1f;

        void PlayVoice(AudioClip clip)
        {
            if (!clip) return;
            if (!voiceSource) voiceSource = Source2D(gameObject);
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.volume = AudioGain;
            voiceSource.Play();
        }

        // The memory's four sounds of the older set, in the order of art.memorySounds.
        static readonly CampaignSfx[] CgSounds = { CampaignSfx.MemoryCgTransition, CampaignSfx.WormholeTurbulence, CampaignSfx.ShipCrash, CampaignSfx.BaseDeploy };

        /// <summary>A cutscene's sound: CampaignAudio's take when it is installed. Otherwise the memory's four sounds fall back to their
        /// older takes in art.memorySounds, and the failed ignition to the weapon jam.</summary>
        void PlayCutsceneSound(CampaignSfx sound)
        {
            if (CampaignAudio.Play(sound)) return;
            if (sound == CampaignSfx.IgnitionFail)
            {
                V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponJam, ShipCentre);
                return;
            }

            int index = System.Array.IndexOf(CgSounds, sound);
            var sounds = art ? art.memorySounds : null;
            if (sounds == null || index < 0 || index >= sounds.Length || !sounds[index]) return;
            if (!cgSoundSource) cgSoundSource = Source2D(gameObject);
            cgSoundSource.PlayOneShot(sounds[index], AudioGain * .85f);
        }

        // ───── Day 0 (its walk and its night are taught in V2Campaign.Tutorial.cs) ─────

        void Objective(string text, Vector3? marker) { ObjectivePoint = marker; ui.SetObjective(text, marker); }
        /// <summary>Where the current objective points (null: nowhere in particular); the HUD marker and automated checks read it.</summary>
        public Vector3? ObjectivePoint { get; private set; }

        IEnumerator Arrival()
        {
            Stage = CampaignStage.Story;
            Objective("", null);
            EndDayZeroWalk();
            gm.CampaignSetPhase(ArenaUiState.Story, 1f);
            CameraRig rig = null;
            if (gm.cam && gm.cam.TryGetComponent(out rig)) rig.Focus(ShipCentre, .75f, 1.2f);
            Say(Line.M01);
            yield return new WaitForSecondsRealtime(LineSeconds(Line.M01, 2.6f));
            Say(Line.S01);
            yield return new WaitForSecondsRealtime(LineSeconds(Line.S01, 5.2f));
            if (rig) rig.ClearFocus(.8f);
            // Since 2026-11-10 the arrival tops Day 0 up to rules.dayZeroAlloy and dayZeroEnergy (V2Campaign.Days.cs): with both ores
            // mined and the first fight's energy picked up that is arrivalAlloy and 11 energy.
            Gain(DayZeroArrivalGift());
            Say(Line.B01);
            OpenBase();
        }

        // ───── Base visits ─────

        /// <summary>Always false since 2026-11-06: the dawn has no base panel any more (it starts the next day at once, EndNight). Kept
        /// for the base panel and the automated checks that read it.</summary>
        public bool BaseAtDawn => false;
        /// <summary>The visit on arrival at the end of Day 0 (leaving it plays the memory CG, then the tutorial night).</summary>
        public bool BaseAfterTutorial => Day == 0 && Stage == CampaignStage.Base && baseVisitFrom == CampaignStage.Story;
        /// <summary>The open base visit came in from the afternoon, so its ways out lead back into the afternoon, into the night or to the
        /// next day (false: from the night, or the arrival on Day 0).</summary>
        public bool BaseFromDusk => Stage == CampaignStage.Base && baseVisitFrom == CampaignStage.Dusk;
        // Where the open base visit came in from: Dusk or Night (leaving goes back out there; without open-ended evenings that clock goes
        // on), Story (the arrival at the end of Day 0), Off (no visit open).
        CampaignStage baseVisitFrom = CampaignStage.Off;
        bool OutdoorVisit => Stage == CampaignStage.Base && baseVisitFrom is (CampaignStage.Dusk or CampaignStage.Night);

        /// <summary>Opens the base panel: on arrival at the end of Day 0, or from the door in the afternoon or at night (without open-ended
        /// evenings that clock waits while the panel is open).</summary>
        void OpenBase()
        {
            baseVisitFrom = (Stage is CampaignStage.Dusk or CampaignStage.Night) ? Stage : CampaignStage.Story;
            // What the player carried in is the base's stock now: a later death that night no longer loses it (in the afternoon nothing is at risk).
            if (baseVisitFrom == CampaignStage.Night)
            {
                if (!TonightsHaul.IsFree) CampaignAudio.Play(CampaignSfx.ResourceDeposit);
                BankHaul();
            }
            Stage = CampaignStage.Base;
            mining = null; mineProgress = 0f; prompt = "";
            gm.CampaignSetPhase(ArenaUiState.Base);
            ui.ShowBase();
        }

        /// <summary>The base panel's way back out: after the arrival on Day 0 the memory CG and the tutorial night; otherwise out at the door
        /// into the afternoon or the night the player came in from.</summary>
        public void LeaveBase()
        {
            if (Stage != CampaignStage.Base) return;
            ui.HideBase();
            var from = baseVisitFrom;
            baseVisitFrom = CampaignStage.Off;
            if (Day == 0 && from == CampaignStage.Story) { StartCoroutine(MemoryCg()); return; }
            Stage = from == CampaignStage.Dusk ? CampaignStage.Dusk : CampaignStage.Night;
            gm.CampaignSetPhase(Stage == CampaignStage.Dusk ? DuskPhase : ArenaUiState.Night);
            PlacePlayer(DoorPoint);
            // The goal's marker points on from the door: the next ore in the afternoon, the nearest dark matter at night.
            AfternoonObjective();
            NightObjective();
        }

        /// <summary>The base panel's "into the night" during an afternoon visit (since 2026-11-06 the afternoon has no clock and only this
        /// ends it): the night begins at once and the player steps out at the door. Ore of the afternoon not mined stays into the night.</summary>
        public void EnterNight()
        {
            if (!BaseFromDusk) return;
            ui.HideBase();
            baseVisitFrom = CampaignStage.Off;
            gm.CampaignSetPhase(ArenaUiState.Night);
            PlacePlayer(DoorPoint);
            BeginNight();
        }

        /// <summary>The base panel's "wait for dawn", at night (and from the afternoon, skipping the night): the rest of the evening passes at
        /// once and the next day begins (EndNight). Since 2026-11-06 this is the only way on from the night besides falling in the dark.</summary>
        public void WaitForDawn()
        {
            if (!OutdoorVisit) return;
            ui.HideBase();
            baseVisitFrom = CampaignStage.Off;
            Stage = CampaignStage.Night; // EndNight takes it from the night; from an afternoon visit the night is skipped too
            EndNight(true);
        }

        /// <summary>True while a base visit from the afternoon or the night is open; seconds is what is left of that afternoon or night (its
        /// clock waits while the panel is open), or infinity with open-ended evenings. BaseFromDusk tells which of the two.</summary>
        public bool NightTimeLeft(out float seconds)
        {
            seconds = EveningsOpenEnded ? float.PositiveInfinity : Mathf.Max(0f, stageLength - stageClock);
            return OutdoorVisit;
        }

        // ───── Story cutscenes ─────

        V2Cutscene cutscene;
        // The memory's five illustrated pictures (CampaignArt.memoryShots) are the moments of the storyboard's M4, M5, M6, M8 and M9.
        static readonly string[] MemoryPictureShots = { "M4", "M5", "M6", "M8", "M9" };

        /// <summary>
        /// Plays a story cutscene on the CG screen shot by shot: each staged in whitebox (V2Cutscene), or, for a shot the art has a picture
        /// for while pictures are on (PictureFor), that picture with its slow push-in. Each shot says its lines and plays its sounds at
        /// their moments, and a cut clears the last shot's subtitle. A click or Esc skips everything left (V2CampaignUi.CgSkipped) and
        /// stops the line being spoken. The stage and its picture are let go at the end.
        /// </summary>
        IEnumerator PlayCutscene(CutsceneShot[] shots, bool memory)
        {
            if (!cutscene) cutscene = V2Cutscene.Create();
            ui.BeginCg(memory);
            for (int i = 0; i < shots.Length && !ui.CgSkipped; i++)
            {
                var shot = shots[i];
                var picture = PictureFor(shot.id, out var front);
                if (picture)
                {
                    cutscene.Hide();
                    ui.CgShot(picture, front, shot.seconds, i);
                }
                else
                {
                    cutscene.Show(shot);
                    ui.CgLive(cutscene.Picture, shot.seconds);
                }

                ui.ClearSubtitle();
                float clock = 0f;
                int next = 0;
                while (!ui.CgSkipped)
                {
                    while (next < shot.beats.Length && shot.beats[next].at <= clock) Beat(shot.beats[next++]);
                    if (clock >= shot.seconds) break;
                    yield return null;
                    clock += Time.unscaledDeltaTime;
                }
            }

            bool skipped = ui.CgSkipped;
            cutscene.End();
            ui.EndCg();
            ui.ClearSubtitle();
            if (!skipped) yield break;
            if (voiceSource) voiceSource.Stop();
            speechUntil = 0f;
        }

        void Beat(CutsceneBeat beat)
        {
            if (beat.line.HasValue) Say(beat.line.Value);
            if (beat.sound.HasValue) PlayCutsceneSound(beat.sound.Value);
            if (beat.whiteOut > 0f) ui.WhiteOut(beat.whiteOut);
        }

        /// <summary>The picture a shot shows in place of its whitebox staging while the illustrated art is on: the art's picture for the
        /// shot's id, else, for the memory, the illustrated memory picture of that moment with its foreground layer. None in whitebox, and
        /// none on the course's English builds (BuildEdition), whose cutscenes are always staged in whitebox.</summary>
        Texture PictureFor(string id, out Texture front)
        {
            front = null;
            if (!art || BuildEdition.IsCourseBuild || !Art.Enabled) return null;
            if (art.cutscenePictures != null)
                foreach (var entry in art.cutscenePictures)
                    if (entry.picture && entry.shot == id) return entry.picture;
            int i = System.Array.IndexOf(MemoryPictureShots, id);
            if (i < 0 || art.memoryShots == null || i >= art.memoryShots.Length || !art.memoryShots[i]) return null;
            if (art.memoryForegrounds != null && i < art.memoryForegrounds.Length) front = art.memoryForegrounds[i];
            return art.memoryShots[i];
        }

        IEnumerator MemoryCg()
        {
            Stage = CampaignStage.Story;
            gm.CampaignSetPhase(ArenaUiState.Story);
            // The base view pales into the memory, then its nine shots (V2Cutscene.Memory).
            ui.WhiteOut(1.2f);
            PlayCutsceneSound(CampaignSfx.MemoryCgTransition);
            float paleUntil = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < paleUntil) yield return null;
            yield return PlayCutscene(V2Cutscene.Memory, true);
            BeginTutorialNight();
        }

        /// <summary>The ignition, from the base panel or by itself at the dawn after the last planned day once the ship is ready: the full
        /// tank fails to light, the core is missing, and the Tyrant arrives (the existing wave-15 boss, same moves).</summary>
        public void Ignite()
        {
            if (!CanIgnite) return;
            ui.HideBase();
            baseVisitFrom = CampaignStage.Off;
            nightWhispers = false;
            StartCoroutine(IgnitionSequence());
        }

        /// <summary>The failed ignition (V2Cutscene.Ignition) while the run stands still with the player at the base door in daylight;
        /// after it the Tyrant fight starts there.</summary>
        IEnumerator IgnitionSequence()
        {
            Stage = CampaignStage.Story;
            ui.ClearFlash();
            Objective("", null);
            ClearNight(false);
            ApplyDaylight();
            PlacePlayer(DoorPoint);
            gm.CampaignSetPhase(ArenaUiState.Story);
            yield return PlayCutscene(V2Cutscene.Ignition, false);
            Stage = CampaignStage.FinalBoss;
            gm.CampaignFinalBoss();
        }

        /// <summary>The dark matter core was taken (TickCore): its pickup sound, then the ending.</summary>
        void StartEnding()
        {
            CampaignAudio.Play(CampaignSfx.CorePickup);
            Stage = CampaignStage.Ending;
            StartCoroutine(EndingSequence());
        }

        /// <summary>The ending (V2Cutscene.Ending) while the run stands still: the core rises, the ship lifts off and jumps home. Its last
        /// shot turns the screen white, and the run is won while it is white.</summary>
        IEnumerator EndingSequence()
        {
            gm.CampaignSetPhase(ArenaUiState.Story);
            yield return PlayCutscene(V2Cutscene.Ending, false);
            EndingDone = true;
            gm.CampaignWin();
        }
    }
}
