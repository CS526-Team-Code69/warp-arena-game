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
    /// The lines are full spoken sentences: the base system speaks precisely and without feeling, Felix like a person talking;
    /// lines without a recording show as subtitles only (D9: voice in English, subtitles per edition).
    /// </summary>
    public sealed partial class V2Campaign
    {
        // New lines go at the end: the voice table (CampaignArt.voices) names its takes by these ids.
        public enum Line { T01, T02, T03, T04, T05, T06, M01, S01, S02, S03, S04, S05, S06, S07, S08, S09, S10, F01, F02, F03, E00, E01, E02, E03, C01, C02, C03, C04, C05, D01, K01, N01, N02, N03, B01, T07, T08, T09, D02, T10, C06, C07, C08, C09, F04, F05,
            C10, C11, C12, C13, C14, C15, C16, C17, C18, C19, C20, C21, F06, F07, F08, F09, F10, F11, F12, E04, E05, E06, E07, E08 }

        // Each line is an ArenaText.Pair, read when it is said: the English edition keeps only the English side.
        static (bool system, string text) LineText(Line id) => id switch
        {
            Line.T01 => (false, ArenaText.Pair("这是哪儿……头还晕得厉害。先四处看看，弄清楚自己掉在了什么地方。", "Where am I... My head is still spinning. I'd better look around and work out where I've ended up.")),
            // T02 says to sprint, which Day 0 teaches only after the ore since 2026-11-10: T07 is said there instead. T02 keeps its
            // recording for when the lines are rewritten.
            Line.T02 => (false, ArenaText.Pair("角落里有东西在发光，冲过去看看。", "Something's glowing in that corner. Let me sprint over.")),
            Line.T07 => (false, ArenaText.Pair("那边的角落里好像有东西在发光，我过去看看是什么。", "Something in that corner over there is glowing. I'll go and see what it is.")),
            Line.T08 => (false, ArenaText.Pair("是个武器箱，居然没在坠毁时摔坏。打开看看，里面的东西也许还能用。", "It's a weapon crate, and it somehow made it through the crash. Let me open it; whatever's inside might still work.")),
            Line.T09 => (false, ArenaText.Pair("这块晶体采不下来……它和周围的东西完全不一样，好像根本不属于这颗星球。", "I can't mine this crystal... It's nothing like anything else around here, as if it doesn't belong on this planet at all.")),
            Line.D02 => (false, ArenaText.Pair("趁天还亮，我得多收集些矿物，顺便找找附近还有没有完好的武器箱。", "While it is still light, I should gather more ore and check whether any other weapon crates survived the crash.")),
            // After the first weapon is taken from a crate (2026-11-10): only then does the game say the weapons here have changed.
            Line.T10 => (false, ArenaText.Pair("这把武器已经不是原来的样子了，应该是被异星能量改变的。用的时候我得多加留意。", "This weapon isn't what it used to be. The alien energy must have changed it somehow, so I'd better be careful how I use it.")),
            Line.T03 => (false, ArenaText.Pair("这块矿石很特别，拿来修飞船应该能派上大用场。", "There's something unusual about this ore. It should go a long way toward repairing the ship.")),
            Line.T04 => (false, ArenaText.Pair("有东西朝我这边过来了，看样子来者不善，先把它们解决掉再说。", "Something's coming my way, and it doesn't look friendly. I'll deal with them first.")),
            Line.T05 => (false, ArenaText.Pair("这块碎片里好像蕴含着某种能量，说不定以后能帮我活下去。先带回基地，让系统研究一下。", "There's some kind of energy in this shard. It might help me stay alive later on, so I'll take it back to the base and have the system study it.")),
            Line.T06 => (false, ArenaText.Pair("基地好像还在运转。先回去看看，说不定那里有修飞船的办法。", "The base still seems to be running. I'll head back and see whether it can help me fix the ship.")),
            Line.M01 => (false, ArenaText.Pair("飞船摔成了这个样子……想靠它回家，得花不少功夫来修。", "Look at the state of the ship... If I'm going to get home in it, it's going to take a lot of work.")),
            Line.S01 => (true, ArenaText.Pair("基地系统已经部署完毕。飞船在坠毁中严重受损，要返回地球，需要先修复引擎和装甲，再合成足够的反物质燃料。", "The base system has finished deploying. The ship was badly damaged in the crash. To return to Earth, you must repair the engine and the armor, and then synthesize enough antimatter fuel.")),
            Line.S02 => (true, ArenaText.Pair("夜晚已经降临。只有灯光照到的地方才看得见，外出时请随身携带光源。", "Night has fallen. You can only see where there is light, so carry a light source with you whenever you go out.")),
            Line.S03 => (true, ArenaText.Pair("警告。这一带聚集的影子怪物太多，已经有一处光源被它们破坏了。", "Warning. Too many shadow monsters have gathered in this area, and they have destroyed one of your light sources.")),
            Line.S04 => (true, ArenaText.Pair("警告。基地受损，{0}已经停止运转，请在夜间回到基地修复。", "Warning. The base's outer wall is damaged, and {0} has gone offline. Repairs can be carried out at night.")),
            Line.S05 => (true, ArenaText.Pair("受损的功能区已经修复完毕，恢复正常运转。", "The damaged room has been repaired and is running normally again.")),
            Line.S06 => (true, ArenaText.Pair("基地护盾已经完全恢复，可以再次抵挡攻击。", "The base shield has been restored to full strength and can absorb attacks again.")),
            Line.S07 => (true, ArenaText.Pair("天已经亮了。检测到敌人正在逼近基地，请做好准备，守住基地。", "Day has broken. Enemies have been detected approaching the base. Prepare to defend it.")),
            Line.S08 => (true, ArenaText.Pair("飞船引擎已经修复完毕，距离返航又近了一步。", "Repair of the ship's engine is complete. The ship is one step closer to being able to return.")),
            Line.S09 => (true, ArenaText.Pair("飞船装甲已经修复完毕，距离返航又近了一步。", "Repair of the ship's armor is complete. The ship is one step closer to being able to return.")),
            // S10, F01 to F05, E01 to E03 and C01 to C09 are the story cutscenes' earlier lines: kept with their recordings, no longer said.
            Line.S10 => (true, ArenaText.Pair("反物质燃料已满。启动点火程序。", "Antimatter fuel full. Starting ignition sequence.")),
            Line.F01 => (true, ArenaText.Pair("点火失败。燃料无法激活。", "Ignition failed. Fuel cannot be activated.")),
            Line.F02 => (true, ArenaText.Pair("缺少暗物质核心。", "Dark matter core missing.")),
            Line.F03 => (false, ArenaText.Pair("那是……什么东西？", "What... is that?")),
            Line.E00 => (false, ArenaText.Pair("这是从它身上掉出来的……这就是系统说的暗物质核心吧？有了它，飞船应该就能点火了。", "This fell out of it... It must be the dark matter core the system talked about. With this, the ship should finally be able to ignite.")),
            Line.E01 => (true, ArenaText.Pair("暗物质核心已安装。点火程序启动。", "Dark matter core installed. Ignition sequence started.")),
            Line.E02 => (true, ArenaText.Pair("跃迁坐标锁定：地球。", "Jump coordinates locked: Earth.")),
            Line.E03 => (false, ArenaText.Pair("该回家了。", "Time to go home.")),
            Line.C01 => (false, ArenaText.Pair("我是一名星际战士，奉命前往银河系作战。", "I'm an interstellar soldier, sent to fight across the galaxy.")),
            Line.C02 => (false, ArenaText.Pair("那次虫洞跃迁，我们撞上了异星乱流。", "During the wormhole jump, we hit alien turbulence.")),
            Line.C03 => (false, ArenaText.Pair("醒来时，我已经落在这颗星球上，飞船烂了。", "When I woke, I'd crashed on this planet. The ship was wrecked.")),
            Line.C04 => (false, ArenaText.Pair("飞船自动部署了基地系统。它就是为这种意外准备的。", "The ship deployed its base system on its own. It was built for accidents like this.")),
            Line.C05 => (false, ArenaText.Pair("它会帮我离开这里。", "It will help me get out of here.")),
            Line.C06 => (false, ArenaText.Pair("人类遭到了异星入侵。", "Humanity was invaded by aliens.")),
            Line.C07 => (false, ArenaText.Pair("为了反击，人类开始了大规模征兵。", "To fight back, humanity called a great conscription.")),
            Line.C08 => (false, ArenaText.Pair("我叫 Felix，是外婆把我带大的。", "I'm Felix. My grandmother raised me.")),
            Line.C09 => (false, ArenaText.Pair("部队的其他人，不知所踪。", "The rest of my unit went missing.")),
            Line.F04 => (true, ArenaText.Pair("警告：暗物质能量波动正在扩散。", "Warning: dark matter energy surge spreading.")),
            Line.F05 => (false, ArenaText.Pair("点火程序完成不了……它需要一股极其庞大的能量来驱动。", "The ignition can't finish... it needs an enormous source of energy.")),
            // The story cutscenes (V2Cutscene). The memory: Felix gives the base system his survivor's record.
            Line.C10 => (true, ArenaText.Pair("幸存者记录已经开始录制。请先说出你的姓名和身份，再讲你是怎么来到这颗星球的。", "I have started recording your survivor's record. Please state your name and your role first, and then tell me how you came to be on this planet.")),
            Line.C11 => (false, ArenaText.Pair("我叫 Felix，是一名星际战士。我尽量把事情讲清楚，不过有些事，我自己到现在也没想明白。", "My name is Felix, and I'm an interstellar soldier. I'll tell it as clearly as I can, though there are parts I still haven't made sense of myself.")),
            Line.C12 => (false, ArenaText.Pair("这一切，要从异星人入侵的那天说起。在那之前，我从没想过自己有一天会离开地球。", "It all started on the day the aliens invaded. Before that, I never imagined I would one day leave Earth.")),
            Line.C13 => (false, ArenaText.Pair("为了反击，人类开始大规模征兵，只要拿得动枪的人都被征走了。我跟着成千上万的新兵上了舰队，被派往银河系的各个战场。", "To fight back, humanity called up anyone who could hold a rifle. I boarded the fleet with thousands of other new recruits, and we were sent to battlefields all across the galaxy.")),
            Line.C14 => (false, ArenaText.Pair("舰队在虫洞里跃迁到一半，突然撞上了一股异星乱流。整艘船都在剧烈摇晃，我只记得警报响个不停。", "Our fleet was halfway through a wormhole jump when we ran into alien turbulence. The whole ship was shaking violently, and all I remember is the alarms that would not stop.")),
            Line.C15 => (false, ArenaText.Pair("等我反应过来，飞船已经彻底失控，一头朝这颗星球栽了下去。", "By the time I understood what was happening, the ship was completely out of control and plunging toward this planet.")),
            Line.C16 => (false, ArenaText.Pair("醒来的时候，飞船已经摔得不成样子。我在残骸附近找了很久，部队里的其他人，一个也没有找到。", "When I came to, the ship was a wreck. I searched around it for a long time, but I couldn't find a single person from my unit.")),
            Line.C17 => (true, ArenaText.Pair("补充记录：坠毁之后，飞船为了保护自身，自动部署了基地系统，也就是我。", "Addendum to the record. After the crash, the ship deployed its base system automatically to protect itself. That base system is me.")),
            Line.C18 => (true, ArenaText.Pair("这艘飞船在设计时，就为这类事故准备了我。按照规程，我需要通知你的亲属。请告诉我，应该通知谁？", "This ship was designed with me aboard for accidents of exactly this kind. Procedure requires me to notify your next of kin. Please tell me whom I should notify.")),
            Line.C19 => (false, ArenaText.Pair("我外婆。我是她带大的，入伍那天，也是她站在家门口送我走的。", "My grandmother. She's the one who raised me, and on the day I shipped out, she stood at our front door to see me off.")),
            Line.C20 => (false, ArenaText.Pair("不，还是什么都别发给她。她年纪大了，受不了这种消息。等我回去，我要当面告诉她。", "No, don't send her anything. She's old, and news like that would be too much for her. When I get back, I want to tell her myself, face to face.")),
            Line.C21 => (true, ArenaText.Pair("明白，我不会发送任何通知。从现在起，我的首要任务是协助你离开这颗星球。", "Understood. I will not send any notification. From now on, my primary task is to help you leave this planet.")),
            // The failed ignition.
            Line.F06 => (false, ArenaText.Pair("反物质燃料终于加满了，这一天我等了很久。启动吧，带我们回家。", "The antimatter tank is finally full. I've been waiting a long time for this. Start it up and take us home.")),
            Line.F07 => (true, ArenaText.Pair("收到。燃料储量已满，现在开始执行点火程序。", "Acknowledged. The fuel tank is full. Beginning the ignition sequence now.")),
            Line.F08 => (false, ArenaText.Pair("燃料一点问题都没有，卡住的是点火程序。它停在最后一步，好像还缺一股极其庞大的能量来推动。", "There's nothing wrong with the fuel. It's the ignition sequence that's stuck. It stops at the last step, as if it still needs an enormous surge of energy to push it through.")),
            Line.F09 => (true, ArenaText.Pair("警告。点火失败时释放出了一股暗物质能量波，正在向四周扩散，可能会引来附近的异星生物。", "Warning. The failed ignition released a wave of dark matter energy. It is spreading outward and may attract alien life nearby.")),
            Line.F10 => (true, ArenaText.Pair("确认：有一个大型目标被这股能量吸引，正在朝基地移动。", "Confirmed. A large creature has been drawn by the energy and is moving toward the base.")),
            Line.F11 => (true, ArenaText.Pair("扫描完成。目标体内有一颗暗物质核心，点火程序缺少的能量，全都可以从那颗核心里取得。", "Scan complete. The creature carries a dark matter core. All the energy the ignition sequence is missing can be taken from that core.")),
            Line.F12 => (false, ArenaText.Pair("那就没什么好犹豫的了。我去把那颗核心拿回来。", "Then there's nothing left to decide. I'll go and take that core.")),
            // The ending.
            Line.E04 => (false, ArenaText.Pair("核心已经装上了。再试一次吧，这回应该能行。", "The core is in. Try it again; this time it should work.")),
            Line.E05 => (true, ArenaText.Pair("暗物质核心已经就位，能量读数正常。现在重新执行点火程序。", "The dark matter core is in place, and the energy readings are normal. Running the ignition sequence again now.")),
            Line.E06 => (false, ArenaText.Pair("部队里的其他人，我到最后也没能找到。等回到地球，我一定会让人回来接着找他们。", "I never did find the rest of my unit. Once I'm back on Earth, I'll make sure someone comes back here to keep looking for them.")),
            Line.E07 => (true, ArenaText.Pair("跃迁坐标已锁定地球。幸存者记录仍在录制，请确认是否现在关闭。", "The jump coordinates are locked on Earth. The survivor's record is still open. Please confirm whether I should close it now.")),
            Line.E08 => (false, ArenaText.Pair("关掉吧，剩下的不用再记了。我说过，我要亲自回去告诉她。", "Close it. The rest doesn't need to be recorded. I told you I'd go back and tell her myself.")),
            // The screen calls this stage the afternoon, and so does the line.
            Line.D01 => (true, ArenaText.Pair("现在是下午。入夜以后，异星力量会让怪物变得更强，建议趁天黑之前尽量补充物资。", "It is now afternoon. After nightfall, alien power makes the monsters stronger. I recommend gathering as many supplies as you can before dark.")),
            Line.K01 => (true, ArenaText.Pair("检测到你的生命体征消失，我已将你带回基地。你今晚收集的物资已经全部遗失。", "Your vital signs were lost, so I have brought you back to the base. Everything you collected tonight has been lost.")),
            Line.N01 => (true, ArenaText.Pair($"按 {ArenaKeys.LampKey} 放下一盏矿灯。只有在灯光照到的范围内才能采集，所以请先把灯放在矿点旁边。", $"Press {ArenaKeys.LampKey} to set down a mining lamp. You can only mine inside its light, so place it next to the deposit first.")),
            Line.N02 => (true, ArenaText.Pair($"暗物质只会在夜里出现。站在灯光照亮的范围内，按住 {ArenaKeys.InteractKey} 就能采集。", $"Dark matter only appears at night. Stand inside the lamp light and hold {ArenaKeys.InteractKey} to mine it.")),
            Line.N03 => (true, ArenaText.Pair($"影子怪物害怕光亮。上午杀敌越多的地方，夜里聚集的影子也会越多。采集完毕后，请回到基地门口按 {ArenaKeys.InteractKey}，选择等待天亮。", $"Shadow monsters are afraid of light. The more enemies you kill in a place during the morning, the more shadows gather there at night. When you have finished mining, return to the base door and press {ArenaKeys.InteractKey} to wait for dawn.")),
            Line.B01 => (true, ArenaText.Pair("基地的储备物资已经开放。你可以用合金和异星能量建造武装区与能量研究区，扩充基地的功能。", "The base reserves are now open to you. Use alloy and energy to build the Armament Bay and the Energy Lab and extend what the base can do.")),
            _ => (false, "")
        };

        public static string Speaker(bool system) => system ? ArenaText.Pair("基地系统", "BASE SYSTEM") : ArenaText.Pair("我", "ME");

        /// <summary>The words said as the night falls (S02) in the language shown: the story layer's night banner carries them.</summary>
        public static string NightFallWords => LineText(Line.S02).text;

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
            speechUntil = Time.unscaledTime + SpeakingSeconds(voice, text) + (voice ? .3f : 0f);
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

        /// <summary>How long saying a line takes: its recording, or for a line shown as a subtitle only a short while by its length.</summary>
        static float SpeakingSeconds(AudioClip voice, string text) => voice ? voice.length : Mathf.Min(3.5f, 1.2f + .06f * text.Length);

        /// <summary>How long saying a line takes in the language shown (SpeakingSeconds), with its recording when it has one.</summary>
        float SpeakingSeconds(Line id) => SpeakingSeconds(VoiceClip(id), LineText(id).text);

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
            if (!art || !RecordedKeysHeld(id)) return null;
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

        /// <summary>N01 to N03 were recorded naming the default keys, F for the lamp and E to use. Once the player has moved that key in the
        /// settings the line shows as a subtitle only, which names the key in use, so the voice never names a key that does nothing.</summary>
        static bool RecordedKeysHeld(Line id) => id switch
        {
            Line.N01 => AtDefaultKey(GameAction.Lamp),
            Line.N02 or Line.N03 => AtDefaultKey(GameAction.Interact),
            _ => true
        };

        static bool AtDefaultKey(GameAction action) => ArenaKeys.KeyNameOf(action) == ArenaKeys.DefaultKeyNameOf(action);

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
            SaveStoryPhase(SavePhase.Night); // the night's save, before its shadows and dark matter (V2Campaign.Save.cs)
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
        readonly List<(float at, CutsceneBeat beat)> cutsceneBeats = new();
        /// <summary>In a cutscene a line starts at least this long after the one before it has finished, and a shot lasts at least this
        /// long after its last line has finished.</summary>
        public const float CutsceneLineGap = .3f, CutsceneLineTail = .4f;

        /// <summary>
        /// Plays a story cutscene on the CG screen shot by shot: each staged in whitebox (V2Cutscene), or, for a shot the art has a picture
        /// for while pictures are on (PictureFor), that picture, framed as the shot asks. Each shot begins with its own cut, is graded as
        /// the present or a memory (V2CampaignUi.CgShot), says its lines and plays its sounds at the moments Schedule gives them, and lasts
        /// until its last line has finished; a cut clears the last shot's subtitle. A shot fades out to black when the next begins from
        /// black, and so does the last, unless a white-out covers its end. A click or Esc skips everything left (V2CampaignUi.CgSkipped)
        /// and stops the line being spoken and the cutscene's sounds. The stage and its picture are let go at the end.
        /// </summary>
        IEnumerator PlayCutscene(CutsceneShot[] shots)
        {
            // A page without cutscenes (BuildEdition.PlaysCutscenes) goes on at once, as if the player had skipped.
            if (!BuildEdition.PlaysCutscenes) yield break;
            if (!cutscene) cutscene = V2Cutscene.Create();
            ui.BeginCg();
            for (int i = 0; i < shots.Length && !ui.CgSkipped; i++)
            {
                var shot = shots[i];
                float length = Schedule(shot, SpeakingSeconds, cutsceneBeats);
                bool fadeOut = i + 1 < shots.Length ? shots[i + 1].cut == CutsceneCut.Black : !shot.EndsWhite;
                if (shot.cut == CutsceneCut.Dissolve) ui.CgHoldPicture();
                var picture = PictureFor(shot.id);
                if (picture)
                {
                    cutscene.Hide();
                    ui.CgShot(picture, false, shot, length, fadeOut);
                }
                else
                {
                    cutscene.Show(shot);
                    ui.CgShot(cutscene.Picture, true, shot, length, fadeOut);
                }

                ui.ClearSubtitle();
                float clock = 0f;
                int next = 0;
                while (!ui.CgSkipped)
                {
                    while (next < cutsceneBeats.Count && cutsceneBeats[next].at <= clock) Beat(cutsceneBeats[next++].beat);
                    if (clock >= length) break;
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
            // The cutscene's sounds stop with its picture.
            foreach (var shot in shots)
                foreach (var beat in shot.beats)
                    if (beat.sound.HasValue) CampaignAudio.Stop(beat.sound.Value);
            if (cgSoundSource) cgSoundSource.Stop();
        }

        /// <summary>
        /// Puts a shot's beats at the moments they happen, in order of time, into beats, and returns how long the shot lasts. Sounds,
        /// white-outs and cuts to black keep the moments the table gives them. A line waits until the line before it has finished
        /// (speakingSeconds: its recording, or the time a line without one counts as spoken) and CutsceneLineGap more; the shot lasts
        /// until its last line has finished and CutsceneLineTail more, when that is longer than its seconds. A shot that grows keeps its
        /// closing (a white-out full as it ends, or a cut to black) at its end: the beats from where the closing begins move on by as much
        /// as the shot grew.
        /// </summary>
        public static float Schedule(CutsceneShot shot, System.Func<Line, float> speakingSeconds, List<(float at, CutsceneBeat beat)> beats)
        {
            beats.Clear();
            float spokenUntil = float.NegativeInfinity, length = shot.seconds;
            foreach (var beat in shot.beats)
            {
                if (!beat.line.HasValue) continue;
                float at = Mathf.Max(beat.at, spokenUntil + CutsceneLineGap);
                spokenUntil = at + Mathf.Max(0f, speakingSeconds(beat.line.Value));
                length = Mathf.Max(length, spokenUntil + CutsceneLineTail);
                beats.Add((at, beat));
            }

            float grown = length - shot.seconds, closing = shot.ClosingAt;
            foreach (var beat in shot.beats)
                if (!beat.line.HasValue) beats.Add((beat.at >= closing ? beat.at + grown : beat.at, beat));
            beats.Sort((a, b) => a.at.CompareTo(b.at));
            return length;
        }

        void Beat(CutsceneBeat beat)
        {
            if (beat.line.HasValue) Say(beat.line.Value);
            if (beat.sound.HasValue) PlayCutsceneSound(beat.sound.Value);
            if (beat.whiteOut > 0f) ui.WhiteOut(beat.whiteOut);
            if (beat.black) ui.CgBlack();
        }

        /// <summary>The picture a shot shows in place of its whitebox staging while the illustrated art is on: the art's picture for the
        /// shot's id (CampaignArt.cutscenePictures; the file of M11 is cutscene-m11.png). A shot without one is staged in whitebox. None in
        /// whitebox, and none on the course's Alpha and Beta (BuildEdition): the Beta plays no cutscenes and the Alpha has no story. The
        /// English Gold shows them.</summary>
        Texture PictureFor(string id)
        {
            if (!art || BuildEdition.IsCourseBuild || !Art.Enabled || art.cutscenePictures == null) return null;
            foreach (var entry in art.cutscenePictures)
                if (entry.picture && entry.shot == id) return entry.picture;
            return null;
        }

        IEnumerator MemoryCg()
        {
            Stage = CampaignStage.Story;
            gm.CampaignSetPhase(ArenaUiState.Story);
            // The memory (V2Cutscene.Memory) opens in the present, in the base's control room, and pales into the past on its own
            // white-out (M11); a page without cutscenes goes straight on.
            yield return PlayCutscene(V2Cutscene.Memory);
            BeginTutorialNight();
        }

        /// <summary>The ignition, from the base panel or by itself at the dawn after the last planned day once the ship is ready: the full
        /// tank fails to light for want of an enormous energy, and the Tyrant arrives carrying a dark matter core (the existing wave-15
        /// boss, same moves).</summary>
        public void Ignite()
        {
            if (!CanIgnite) return;
            ui.HideBase();
            baseVisitFrom = CampaignStage.Off;
            StartCoroutine(IgnitionSequence());
        }

        /// <summary>The failed ignition (V2Cutscene.Ignition) while the run stands still with the player at the base door in daylight;
        /// after it the final battle's save is written and the Tyrant fight starts there.</summary>
        IEnumerator IgnitionSequence()
        {
            Stage = CampaignStage.Story;
            ui.ClearFlash();
            Objective("", null);
            ClearNight(false);
            ApplyDaylight();
            PlacePlayer(DoorPoint);
            gm.CampaignSetPhase(ArenaUiState.Story);
            yield return PlayCutscene(V2Cutscene.Ignition);
            SaveStoryPhase(SavePhase.Tyrant); // the final battle's save, before the fight begins (V2Campaign.Save.cs)
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

        /// <summary>The ending (V2Cutscene.Ending) while the run stands still: the core goes into the engine, the ship lifts off, the
        /// record is closed in orbit and the ship jumps home. Its last shot turns the screen white, and the run is won while it is white.</summary>
        IEnumerator EndingSequence()
        {
            gm.CampaignSetPhase(ArenaUiState.Story);
            yield return PlayCutscene(V2Cutscene.Ending);
            EndingDone = true;
            gm.CampaignWin();
        }
    }
}
