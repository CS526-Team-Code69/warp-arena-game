using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
                 
                                                                                                                                             
                                                                                                                                        
                                                                                                                                              
                                                                                                                                           
                                                                                                                          
                                                                                                                                          
                                                                           
                  
    public sealed partial class V2Campaign
    {
        public enum Line { T01, T02, T03, T04, T05, T06, M01, S01, S02, S03, S04, S05, S06, S07, S08, S09, S10, F01, F02, F03, E00, E01, E02, E03, C01, C02, C03, C04, C05, D01, K01, N01, N02, N03, B01 }

                                                                                                                 
        static (bool system, string text) LineText(Line id) => id switch
        {
            Line.T01 => (false, ArenaText.Pair("Where am I... Let me look around first.","Where am I... Let me look around first.")),
            Line.T02 => (false, ArenaText.Pair("Something's glowing in that corner. Let me sprint over.","Something's glowing in that corner. Let me sprint over.")),
            Line.T03 => (false, ArenaText.Pair("This ore... it'll go a long way toward fixing the ship.","This ore... it'll go a long way toward fixing the ship.")),
            Line.T04 => (false, ArenaText.Pair("Something's coming. Deal with them first.","Something's coming. Deal with them first.")),
            Line.T05 => (false, ArenaText.Pair("This should help me survive later. I'll take it back to base and study it.","This should help me survive later. I'll take it back to base and study it.")),
            Line.T06 => (false, ArenaText.Pair("The base is still running. Head back.","The base is still running. Head back.")),
            Line.M01 => (false, ArenaText.Pair("The ship... it's wrecked.","The ship... it's wrecked.")),
            Line.S01 => (true, ArenaText.Pair("Base system deployed automatically. Ship heavily damaged. Repair the engine and armor and synthesize antimatter fuel to return.","Base system deployed automatically. Ship heavily damaged. Repair the engine and armor and synthesize antimatter fuel to return.")),
            Line.S02 => (true, ArenaText.Pair("Night has fallen. Unlit areas are pitch dark. Carry a light source.","Night has fallen. Unlit areas are pitch dark. Carry a light source.")),
            Line.S03 => (true, ArenaText.Pair("Warning: too many shadows in this area. A light source was destroyed.","Warning: too many shadows in this area. A light source was destroyed.")),
            Line.S04 => (true, ArenaText.Pair("Hull damaged. {0} is offline. Repair it at night.","Hull damaged. {0} is offline. Repair it at night.")),
            Line.S05 => (true, ArenaText.Pair("Room repaired.","Room repaired.")),
            Line.S06 => (true, ArenaText.Pair("Base shield restored.","Base shield restored.")),
            Line.S07 => (true, ArenaText.Pair("Daybreak. Enemies incoming. Defend the base.","Daybreak. Enemies incoming. Defend the base.")),
            Line.S08 => (true, ArenaText.Pair("Ship engine repaired.","Ship engine repaired.")),
            Line.S09 => (true, ArenaText.Pair("Ship armor repaired.","Ship armor repaired.")),
            Line.S10 => (true, ArenaText.Pair("Antimatter fuel full. Starting ignition sequence.","Antimatter fuel full. Starting ignition sequence.")),
            Line.F01 => (true, ArenaText.Pair("Ignition failed. Fuel cannot be activated.","Ignition failed. Fuel cannot be activated.")),
            Line.F02 => (true, ArenaText.Pair("Dark matter core missing.","Dark matter core missing.")),
            Line.F03 => (false, ArenaText.Pair("What... is that?","What... is that?")),
            Line.E00 => (false, ArenaText.Pair("What fell out of it... is that the dark matter core?","What fell out of it... is that the dark matter core?")),
            Line.E01 => (true, ArenaText.Pair("Dark matter core installed. Ignition sequence started.","Dark matter core installed. Ignition sequence started.")),
            Line.E02 => (true, ArenaText.Pair("Jump coordinates locked: Earth.","Jump coordinates locked: Earth.")),
            Line.E03 => (false, ArenaText.Pair("Time to go home.","Time to go home.")),
            Line.C01 => (false, ArenaText.Pair("I'm an interstellar soldier, sent to fight across the galaxy.","I'm an interstellar soldier, sent to fight across the galaxy.")),
            Line.C02 => (false, ArenaText.Pair("During the wormhole jump, we hit alien turbulence.","During the wormhole jump, we hit alien turbulence.")),
            Line.C03 => (false, ArenaText.Pair("When I woke, I'd crashed on this planet. The ship was wrecked.","When I woke, I'd crashed on this planet. The ship was wrecked.")),
            Line.C04 => (false, ArenaText.Pair("The ship deployed its base system on its own. It was built for accidents like this.","The ship deployed its base system on its own. It was built for accidents like this.")),
            Line.C05 => (false, ArenaText.Pair("It will help me get out of here.","It will help me get out of here.")),
            Line.D01 => (true, ArenaText.Pair("Dusk. Alien power strengthens the monsters at night. Gather what you can before dark.","Dusk. Alien power strengthens the monsters at night. Gather what you can before dark.")),
            Line.K01 => (true, ArenaText.Pair("Vital signs lost. You have been brought back to the base. Tonight's haul is lost.","Vital signs lost. You have been brought back to the base. Tonight's haul is lost.")),
            Line.N01 => (true, ArenaText.Pair("Press F to set down a mining lamp. You can only mine in its light.","Press F to set down a mining lamp. You can only mine in its light.")),
            Line.N02 => (true, ArenaText.Pair("Dark matter only shows at night. Hold E in the lamp light to mine it.","Dark matter only shows at night. Hold E in the lamp light to mine it.")),
            Line.N03 => (true, ArenaText.Pair("Shadows fear the light. Where you killed the most by day, more shadows walk at night. Press E at the base door to wait for dawn.","Shadows fear the light. Where you killed the most by day, more shadows walk at night. Press E at the base door to wait for dawn.")),
            Line.B01 => (true, ArenaText.Pair("Base reserves released. Build the Armament Bay and the Energy Lab with alloy and energy.","Base reserves released. Build the Armament Bay and the Energy Lab with alloy and energy.")),
            _ => (false, "")
        };

        public static string Speaker(bool system) => system ? ArenaText.Pair("BASE SYSTEM","BASE SYSTEM") : ArenaText.Pair("ME","ME");

        void Say(Line id, params object[] args) => SayWith(id, VoiceClip(id), args);

                                                                                                       
        void SayRoomOffline(CampaignRoom room) => SayWith(Line.S04, VoiceClip(Line.S04, room), new object[] { RoomName(room) });

        void SayWith(Line id, AudioClip voice, object[] args)
        {
            if (!ui) return;
            var line = LineText(id);
            if (string.IsNullOrEmpty(line.text)) return;
            string text = line.text;
            if (args != null && args.Length > 0) text = string.Format(text, args);
            PlayVoice(voice);
            ui.Subtitle(Speaker(line.system), text, line.system, Mathf.Max(2.2f + .085f * text.Length, voice ? voice.length + .4f : 0f));
        }

                                                                                                             

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

                                                                                                                                        
        void PlayCgSound(int index)
        {
            var sounds = art ? art.memorySounds : null;
            if (sounds == null || index < 0 || index >= sounds.Length || !sounds[index]) return;
            if (!cgSoundSource) cgSoundSource = Source2D(gameObject);
            cgSoundSource.PlayOneShot(sounds[index], AudioGain * .85f);
        }

                            

        enum Tut { Wake, Corner, Mine, Field, Fight, Collect, Home, Done }
        Tut tut;
        Vector3 tutMarker;
        Node tutOre;
        readonly List<Enemy> tutGrunts = new();
        string tutorialHint = "";
        bool tutPickupSaid, tutHomeSaid;
        float tutClock;

        void BeginTutorial()
        {
            Stage = CampaignStage.Tutorial;
            tut = Tut.Wake; tutClock = 0f; tutPickupSaid = tutHomeSaid = false;
            tutGrunts.Clear();
            tutMarker = FindOpen(CrashSite + new Vector3(14f, 0f, 0f), 1f);
            Objective(ArenaText.Pair("Walk right (WASD)","Walk right (WASD)"), tutMarker);
            tutorialHint = ArenaText.Pair("WASD MOVE · MOUSE AIM · LEFT CLICK ATTACK · SHIFT SPRINT","WASD MOVE · MOUSE AIM · LEFT CLICK ATTACK · SHIFT SPRINT");
            Say(Line.T01);
                                                                                                                 
            for (int i = 0; i < 5; i++)
                Box(worldRoot, "CrashDebris", CrashSite + new Vector3(Random.Range(-4f, 3f), .2f, Random.Range(-5f, 5f)), new Vector3(Random.Range(.8f, 2f), .4f, Random.Range(.6f, 1.6f)), new Color(.18f, .16f, .15f))
                    .transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
        }

        void Objective(string text, Vector3? marker) { ObjectivePoint = marker; ui.SetObjective(text, marker); }
                                                                                                                                             
        public Vector3? ObjectivePoint { get; private set; }

        void TickTutorial(float dt)
        {
            prompt = "";
            tutClock += dt;
            TickPickups(dt);
            var p = gm.player;
            if (!p || !p.IsAlive) return;
            Vector3 pp = p.Position;
                                                                                                 
            if (tut is Tut.Corner or Tut.Mine && tutOre != null && !tutOre.mined && Random.value < dt * 8f)
                Fx.Pop(tutOre.position + new Vector3(Random.Range(-.6f, .6f), Random.Range(.6f, 1.8f), Random.Range(-.6f, .6f)), new Color(.6f, .9f, 1f), Random.Range(.5f, .9f));
            switch (tut)
            {
                case Tut.Wake:
                    if (Query.Flat(pp - tutMarker).magnitude < 3f || pp.x > tutMarker.x)
                    {
                        tut = Tut.Corner;
                        Vector3 corner = FindOpen(new Vector3(CrashSite.x + 20f, 0f, -WorldMap.Active.Bounds.extents.z * .55f), 1.4f);
                        tutOre = AddNode(NodeKind.Ore, corner, rules.tutorialOreAlloy);
                        if (tutOre.view) tutOre.view.localScale *= 1.4f;
                        Objective(ArenaText.Pair("Hold Shift and sprint to the glowing corner","Hold Shift and sprint to the glowing corner"), corner);
                        Say(Line.T02);
                    }
                    break;
                case Tut.Corner:
                    if (tutOre != null && Query.Flat(pp - tutOre.position).magnitude < 3f)
                    {
                        tut = Tut.Mine;
                        Objective(ArenaText.Pair("Hold E to mine the ore","Hold E to mine the ore"), tutOre.position);
                    }
                    break;
                case Tut.Mine:
                    TickMining(dt, true);
                    break;
                case Tut.Field:
                    if (pp.x > tutMarker.x - 6f)
                    {
                        tut = Tut.Fight;
                        SpawnTutorialGrunts(pp + new Vector3(16f, 0f, 0f));
                        Objective(ArenaText.Pair("Defeat the monsters","Defeat the monsters"), null);
                        Say(Line.T04);
                    }
                    break;
                case Tut.Fight:
                    tutGrunts.RemoveAll(e => !e || !e.IsAlive);
                    if (tutGrunts.Count == 0)
                    {
                        tut = Tut.Collect; tutClock = 0f;
                        Objective(ArenaText.Pair("Walk over the dropped star matter","Walk over the dropped star matter"), null);
                    }
                    break;
                case Tut.Collect:
                    if (pickups.Count == 0 || tutClock > 12f)
                    {
                        tut = Tut.Home;
                        Objective(ArenaText.Pair("Head right to the base","Head right to the base"), DoorPoint);
                    }
                    break;
                case Tut.Home:
                    if (!tutHomeSaid && Query.Flat(pp - BaseCentre).magnitude < 32f) { tutHomeSaid = true; Say(Line.T06); }
                    if (Query.Flat(pp - DoorPoint).magnitude < 3.2f) { tut = Tut.Done; StartCoroutine(Arrival()); }
                    break;
            }
        }

        void SpawnTutorialGrunts(Vector3 centre)
        {
            for (int i = 0; i < rules.tutorialGrunts; i++)
            {
                Vector3 at = FindOpen(centre + Quaternion.Euler(0f, 360f * i / rules.tutorialGrunts, 0f) * Vector3.forward * 3.5f, .6f);
                var enemy = gm.waves.SpawnCampaign(EnemyKind.Grunt, at);
                if (enemy) tutGrunts.Add(enemy);
            }
        }

        void OnTutorialMined(NodeKind kind)
        {
            if (Stage == CampaignStage.Tutorial && tut == Tut.Mine && kind == NodeKind.Ore)
            {
                Say(Line.T03);
                tut = Tut.Field;
                tutMarker = FindOpen(new Vector3(CrashSite.x + 40f, 0f, 0f), 1f);
                Objective(ArenaText.Pair("Keep going right","Keep going right"), tutMarker);
            }
            if (Day == 0 && Stage == CampaignStage.Night && kind == NodeKind.Dark && tutNightStep == 1)
            {
                tutDarkMined++;
                if (tutDarkMined >= rules.nodesPerCluster) { tutNightStep = 2; Say(Line.N03); Objective(ArenaText.Pair("Return to the base and press E at the door to wait for dawn","Return to the base and press E at the door to wait for dawn"), DoorPoint); }
            }
        }

        void OnTutorialPickup(bool energyShard)
        {
            if (Stage == CampaignStage.Tutorial && energyShard && !tutPickupSaid) { tutPickupSaid = true; Say(Line.T05); }
        }

        IEnumerator Arrival()
        {
            Stage = CampaignStage.Story;
            Objective("", null);
            tutorialHint = "";
            gm.CampaignSetPhase(ArenaUiState.Story, 1f);
            CameraRig rig = null;
            if (gm.cam && gm.cam.TryGetComponent(out rig)) rig.Focus(ShipCentre, .75f, 1.2f);
            Say(Line.M01);
            yield return new WaitForSecondsRealtime(LineSeconds(Line.M01, 2.6f));
            Say(Line.S01);
            yield return new WaitForSecondsRealtime(LineSeconds(Line.S01, 5.2f));
            if (rig) rig.ClearFocus(.8f);
            Gain(new CampaignCost(rules.arrivalAlloy, rules.arrivalEnergy));
            Say(Line.B01);
            OpenBase(false);
        }

                                     

        int tutNightStep, tutDarkMined;

        void BeginTutorialNight()
        {
            Stage = CampaignStage.Night;
            CampaignEvents.RaisePhase("night", Day);
            gm.CampaignSetPhase(ArenaUiState.Night);
            PlacePlayer(DoorPoint);
            lampStock += rules.freeLampsPerNight;
            nightAlloy = nightDark = 0; knockedOut = false;
            stageClock = 0f; stageLength = rules.NightLength(0);
            LerpLighting(1f);
            darknessLevel = rules.nightDarkness;
            darkness.SetActive(true);
            if (baseLight) baseLight.intensity = 2.2f;
            Vector3 cluster = FindOpen(BaseCentre + new Vector3(18f, 0f, -9f), 1.2f);
            AddCluster(cluster);
                                                                                                                                   
            for (int i = 0; i < rules.tutorialNightOre; i++)
                AddNode(NodeKind.Ore, FindOpen(cluster + new Vector3(2.6f + 1.4f * i, 0f, 1.8f - 3.6f * i), 1f, 2.5f), rules.tutorialNightOreAlloy);
            SpawnShadows();
            tutNightStep = 0; tutDarkMined = 0;
            Objective(ArenaText.Pair("Go to the purple dark matter and press F to set down a lamp","Go to the purple dark matter and press F to set down a lamp"), cluster);
            Say(Line.S02);
            nightWhispers = true;
            StartCoroutine(TutorialNightLine());
        }

        IEnumerator TutorialNightLine()
        {
            yield return new WaitForSecondsRealtime(4.5f);
            if (Stage == CampaignStage.Night && Day == 0) Say(Line.N01);
        }

        void TickTutorialNight(float dt)
        {
            if (tutNightStep == 0 && lamps.Count > 0)
            {
                tutNightStep = 1;
                Say(Line.N02);
                Objective(ArenaText.Pair("Stand in the light and hold E to mine the dark matter","Stand in the light and hold E to mine the dark matter"), null);
            }
        }

                                  

        public bool BaseAtDawn { get; private set; }
        public bool BaseAfterTutorial => Day == 0 && Stage == CampaignStage.Base && !BaseAtDawn && !nightBaseVisit;
        bool nightBaseVisit;

                                                                                                                                          
        void OpenBase(bool dawn)
        {
            nightBaseVisit = !dawn && Stage == CampaignStage.Night;
            BaseAtDawn = dawn;
            if (nightBaseVisit) BankHaul();                                                                                                   
            Stage = CampaignStage.Base;
            mining = null; mineProgress = 0f; prompt = "";
            gm.CampaignSetPhase(ArenaUiState.Base);
            ui.ShowBase();
        }

                                                                                                                                        
                                       
        public void LeaveBase()
        {
            ui.HideBase();
            if (nightBaseVisit)
            {
                nightBaseVisit = false;
                Stage = CampaignStage.Night;
                gm.CampaignSetPhase(ArenaUiState.Night);
                PlacePlayer(DoorPoint);
                return;
            }
            if (!BaseAtDawn && Day == 0) { StartCoroutine(MemoryCg()); return; }
            if (Day >= rules.days && !Overtime && !CanIgnite)
            {
                                                                                                                
                ui.Flash(ArenaText.Pair("The tank is not full: the ship cannot leave. Hold out another day.","The tank is not full: the ship cannot leave. Hold out another day."), 3.5f);
            }
            OpenDawnBuild();
        }

                                                                         
        public void WaitForDawn()
        {
            ui.HideBase();
            nightBaseVisit = false;
            Stage = CampaignStage.Night;
            EndNight(true);
        }

        public bool NightTimeLeft(out float seconds)
        {
            seconds = Mathf.Max(0f, stageLength - stageClock);
            return nightBaseVisit;
        }

                          

        IEnumerator MemoryCg()
        {
            Stage = CampaignStage.Story;
            gm.CampaignSetPhase(ArenaUiState.Story);
                                                                                                                                       
                                                                                                                                       
                                       
            bool pictures = art && Art.Enabled;
            var shots = pictures ? art.memoryShots : null;
            var fronts = pictures ? art.memoryForegrounds : null;
            var plan = new (Line line, float seconds)[] { (Line.C01, 5f), (Line.C02, 6f), (Line.C03, 6f), (Line.C04, 7f), (Line.C05, 4f) };
            ui.WhiteOut(1.2f);
            PlayCgSound(0);
            float paleUntil = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < paleUntil) yield return null;
            ui.BeginCg();
            for (int shot = 0; shot < plan.Length; shot++)
            {
                if (ui.CgSkipped) break;
                var (line, seconds) = plan[shot];
                Texture picture = shots != null && shot < shots.Length ? shots[shot] : null;
                Texture front = fronts != null && shot < fronts.Length ? fronts[shot] : null;
                                                                                                                        
                if (!picture && pictures && shot == 0 && gm.hud && gm.hud.v2Hud && gm.hud.v2Hud.art) picture = gm.hud.v2Hud.art.themeBackground;
                ui.CgShot(picture, front, seconds, shot);
                if (shot is >= 1 and <= 3) PlayCgSound(shot);                                                 
                Say(line);
                if (line == Line.C02) gm.Shake(.4f);
                float until = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < until && !ui.CgSkipped) yield return null;
            }
            ui.EndCg();
            ui.ClearSubtitle();
            BeginTutorialNight();
        }

                                                                                                                                       
                                                       
        public void Ignite()
        {
            if (!CanIgnite) return;
            ui.HideBase();
            StartCoroutine(IgnitionSequence());
        }

        IEnumerator IgnitionSequence()
        {
            Stage = CampaignStage.Story;
            ui.ClearFlash();
            Objective("", null);
            ClearNight(false);
            ApplyDaylight();
            PlacePlayer(DoorPoint);
            gm.CampaignSetPhase(ArenaUiState.Story, 1f);
            CameraRig rig = null;
            if (gm.cam && gm.cam.TryGetComponent(out rig)) rig.Focus(ShipCentre, .7f, 1.4f);
            Say(Line.S10);
            yield return new WaitForSecondsRealtime(LineSeconds(Line.S10, 2.8f));
            Vector3 nozzle = ShipCentre + new Vector3(-7f, 1.4f, 0f);
            for (float t = 0f; t < 3.2f; t += Time.unscaledDeltaTime)
            {
                if (Random.value < .5f) Fx.Pop(nozzle + Random.insideUnitSphere * .6f, Color.Lerp(new Color(.4f, .8f, 1f), new Color(1f, .6f, .2f), Random.value), Random.Range(.8f, 1.8f));
                gm.Shake(.08f + t * .05f);
                yield return null;
            }
            for (int i = 0; i < 4; i++) { Fx.Burst(nozzle, Vector3.left, new Color(1f, .5f, .2f), 12, 8f, 120f, .3f); gm.Shake(.3f); yield return new WaitForSecondsRealtime(.35f); }
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponJam, ShipCentre);
            Say(Line.F01);
            yield return new WaitForSecondsRealtime(LineSeconds(Line.F01, 2.6f));
            Say(Line.F02);
            ui.Flash(ArenaText.Pair("DARK MATTER CORE MISSING","DARK MATTER CORE MISSING"), 2.5f);
            yield return new WaitForSecondsRealtime(LineSeconds(Line.F02, 2.6f));
            for (float t = 0f; t < 1.8f; t += Time.unscaledDeltaTime) { gm.Shake(.25f); yield return null; }
            if (rig) rig.ClearFocus(.6f);
            Say(Line.F03);
            yield return new WaitForSecondsRealtime(LineSeconds(Line.F03, 1.2f));
            Stage = CampaignStage.FinalBoss;
            gm.CampaignFinalBoss();
        }

        void StartEnding()
        {
            Stage = CampaignStage.Ending;
            StartCoroutine(EndingSequence());
        }

        IEnumerator EndingSequence()
        {
            gm.CampaignSetPhase(ArenaUiState.Story, 1f);
            CameraRig rig = null;
            if (gm.cam && gm.cam.TryGetComponent(out rig)) rig.Focus(ShipCentre, .7f, 1.4f);
            yield return new WaitForSecondsRealtime(1.2f);
            Say(Line.E01);
            fuelTank.sharedMaterial = gm.Mat(new Color(.85f, .4f, 1f));
            yield return new WaitForSecondsRealtime(LineSeconds(Line.E01, 2.4f));
            Vector3 start = shipRoot.position;
            Vector3 nozzle = new(-7f, 1.4f, 0f);
            for (float t = 0f; t < 4.5f; t += Time.unscaledDeltaTime)
            {
                float lift = t * t * .9f;
                shipRoot.position = start + Vector3.up * lift;
                if (Random.value < .7f) Fx.Pop(shipRoot.position + nozzle + Random.insideUnitSphere * .5f, new Color(.6f, .85f, 1f), Random.Range(1f, 2f));
                gm.Shake(.12f);
                if (t > 2f && t - Time.unscaledDeltaTime <= 2f) Say(Line.E02);
                yield return null;
            }
            Say(Line.E03);
            ui.WhiteOut(1.6f);
            yield return new WaitForSecondsRealtime(2f);
            if (rig) rig.ClearFocus(.1f);
            EndingDone = true;
            gm.CampaignWin();
        }
    }
}
