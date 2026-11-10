using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Saved full-game bank. All event sources and looping channels are authored in the Editor.</summary>
    public sealed class V3WorldAudio : MonoBehaviour
    {
        [Serializable] public sealed class Sound
        {
            public ArenaWorldCue cue;
            public AudioClip clip;
            [Range(0f,1f)] public float gain=.55f;
            [Min(0f)] public float minimumInterval=.1f;
            [Range(1,8)] public int maxConcurrent=2;
        }
        public static V3WorldAudio Instance {get;private set;}
        public Sound[] bank;
        public AudioSource[] worldVoices, lifecycleVoices;
        public AudioSource ambienceSource, musicSource;
        [Range(0f,1f)] public float worldGain=.65f, lifecycleGain=.65f, ambienceGain=.14f, musicGain=.2f;
        [Range(0f,1f)] public float masterGain=1f;
        [Min(1f)] public float audibleRange=26f;
        [Min(.01f)] public float backgroundFadeSeconds=.3f;
        Sound[] byCue;
        float[] nextCue, worldLeft, lifeLeft;
        ArenaWorldCue[] worldAssignments, lifeAssignments;
        int deadPlayerVersion=-1;
        bool paused;
        float backgroundBlend;
        Biome selectedBiome;
        bool selectedTrial;

        void Awake()
        {
            Instance=this;
            int count=Enum.GetValues(typeof(ArenaWorldCue)).Length;
            byCue=new Sound[count];nextCue=new float[count];
            foreach(var sound in bank??Array.Empty<Sound>())if(sound!=null)byCue[(int)sound.cue]=sound;
            worldLeft=new float[worldVoices?.Length??0];lifeLeft=new float[lifecycleVoices?.Length??0];
            worldAssignments=new ArenaWorldCue[worldLeft.Length];lifeAssignments=new ArenaWorldCue[lifeLeft.Length];
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
        void OnDisable()=>StopAll();
        public void OnConfirmedHit(Combatant victim,HitKind kind)
        {
            if(!victim)return;
            Play(victim.Team==Team.Player?ArenaWorldCue.PlayerHit:ArenaWorldCue.NpcHit,victim.Position);
        }
        public void OnPlayerDeath(Combatant player)
        {
            if(!player||deadPlayerVersion==player.SpawnVersion)return;
            deadPlayerVersion=player.SpawnVersion;Play(ArenaWorldCue.PlayerDeath,player.Position);
        }
        public void OnAcceptedReroll()=>Play(ArenaWorldCue.CardReroll,Vector3.zero);

        /// <param name="pitch">Lower for a heavier body, higher for a lighter one, so a saved clip can fit more than one enemy (the bank itself stays as saved).</param>
        /// <param name="gainScale">Times the bank's gain.</param>
        public void Play(ArenaWorldCue cue,Vector3 at,float pitch=1f,float gainScale=1f)
        {
            if(!isActiveAndEnabled||byCue==null)return;
            var sound=byCue[(int)cue];bool life=Lifecycle(cue);
            if(sound==null||!sound.clip||Time.unscaledTime<nextCue[(int)cue]||paused&&!life)return;
            var gm=GameManager.Instance;
            if(!life&&gm&&gm.player&&Query.Flat(gm.player.Position-at).sqrMagnitude>audibleRange*audibleRange)return;
            var voices=life?lifecycleVoices:worldVoices;var left=life?lifeLeft:worldLeft;var assignments=life?lifeAssignments:worldAssignments;
            if(voices==null)return;
            int free=-1,concurrent=0;
            for(int i=0;i<voices.Length;i++)
            {
                if(!voices[i])continue;
                if(left[i]<=0f){if(free<0)free=i;}
                else if(assignments[i]==cue)concurrent++;
            }
            if(concurrent>=Mathf.Max(1,sound.maxConcurrent))return;
            if(free<0&&life)
            {
                float shortest=float.PositiveInfinity;
                for(int i=0;i<voices.Length;i++)if(voices[i]&&left[i]<shortest){shortest=left[i];free=i;}
            }
            // An attack's warning must not go missing in a crowd. With every voice busy it takes the voice of a hit, a death or a footstep nearest its end.
            if(free<0&&!life&&Warns(cue))
            {
                float shortest=float.PositiveInfinity;
                for(int i=0;i<voices.Length;i++)if(voices[i]&&assignments[i] is ArenaWorldCue.NpcHit or ArenaWorldCue.NpcDeath or ArenaWorldCue.PlayerStepMetal or ArenaWorldCue.PlayerStepStone&&left[i]<shortest){shortest=left[i];free=i;}
            }
            if(free<0)return;
            var voice=voices[free];voice.Stop();voice.transform.position=at;
            pitch=Mathf.Clamp(pitch,.25f,3f);
            voice.pitch=pitch;voice.volume=sound.gain*gainScale*(life?lifecycleGain:worldGain)*masterGain;
            voice.clip=sound.clip;voice.Play();left[free]=sound.clip.length/pitch;assignments[free]=cue;
            nextCue[(int)cue]=Time.unscaledTime+sound.minimumInterval;
        }
        static bool Warns(ArenaWorldCue cue)=>cue is ArenaWorldCue.NpcWindup or ArenaWorldCue.NpcChargeWarning or ArenaWorldCue.NpcChargeRush or ArenaWorldCue.NpcPulse or ArenaWorldCue.NpcLaserLock or ArenaWorldCue.NpcLaserFire;
        static bool Lifecycle(ArenaWorldCue cue)=>cue is ArenaWorldCue.BossEnter or ArenaWorldCue.BossPhase or ArenaWorldCue.BossDeath
            or ArenaWorldCue.PlayerHit or ArenaWorldCue.PlayerDeath or ArenaWorldCue.PortalOpen or ArenaWorldCue.PortalEnter or ArenaWorldCue.PortalClose
            or ArenaWorldCue.LevelUp or ArenaWorldCue.CardReveal or ArenaWorldCue.CardReroll or ArenaWorldCue.WaveEnd or ArenaWorldCue.Victory or ArenaWorldCue.Defeat
            or ArenaWorldCue.PlayerStunned or ArenaWorldCue.PickupEnergy or ArenaWorldCue.TimeHeld or ArenaWorldCue.SkillVoidWalk;
        void Update()
        {
            var gm=GameManager.Instance;
            bool shouldPause=gm&&gm.RunPhase==ArenaUiState.Paused;
            if(shouldPause!=paused)
            {
                paused=shouldPause;
                foreach(var voice in worldVoices??Array.Empty<AudioSource>())if(voice){if(paused)voice.Pause();else voice.UnPause();}
                if(ambienceSource){if(paused)ambienceSource.Pause();else ambienceSource.UnPause();}
                if(musicSource){if(paused)musicSource.Pause();else musicSource.UnPause();}
            }
            TickVoices(lifecycleVoices,lifeLeft);
            if(!paused)TickVoices(worldVoices,worldLeft);
            // The combat music and the ambience play only while the player can move, as before 10-26:
            // the wave and its intro, the void trial, the final boss, and the build phase walked with its panel put away or while its
            // supply crate comes down. Every screen that holds the player (the wave's end, an opened crate and the build panels, a weapon
            // card, a card pick, the black-hole cutscene, the trial's return) fades them out and stops them; once the player can move
            // again they start from the top. Only the pause menu holds them where they are, and they fade out once the run is over.
            bool running=gm&&gm.CanPlayerMove;
            if(!paused)backgroundBlend=Mathf.MoveTowards(backgroundBlend,running?1f:0f,Time.unscaledDeltaTime/Mathf.Max(.01f,backgroundFadeSeconds));
            var world=WorldMap.Active;
            if(world&&(selectedBiome!=world.biome || selectedTrial!=world.TrialActive))
            {
                selectedBiome=world.biome; selectedTrial=world.TrialActive;
                string name=selectedBiome?selectedBiome.name:"Wreck";
                ArenaWorldCue cue=selectedTrial?ArenaWorldCue.AmbientRift:name.StartsWith("Dust",StringComparison.OrdinalIgnoreCase)?ArenaWorldCue.AmbientDust:name.StartsWith("Rift",StringComparison.OrdinalIgnoreCase)?ArenaWorldCue.AmbientRift:ArenaWorldCue.AmbientWreck;
                if(ambienceSource){ambienceSource.Stop();ambienceSource.clip=byCue[(int)cue]?.clip;}
            }
            // The title screen and a V2 campaign run have their own tracks (CampaignMusic, 2026-11-06): while one plays, this music is
            // silent and stopped; the ambience goes on as before. Without those tracks (a classic run, or a file not there) nothing changes.
            bool yieldMusic=CampaignMusic.Overrides;
            if(running&&!paused)
            {
                if(musicSource&&!musicSource.clip)musicSource.clip=byCue[(int)ArenaWorldCue.CombatMusic]?.clip;
                if(musicSource&&musicSource.clip&&!musicSource.isPlaying&&!yieldMusic)musicSource.Play();
                if(ambienceSource&&ambienceSource.clip&&!ambienceSource.isPlaying)ambienceSource.Play();
            }
            if(yieldMusic&&!paused&&musicSource&&musicSource.isPlaying)musicSource.Stop();
            if(musicSource)musicSource.volume=yieldMusic?0f:musicGain*backgroundBlend*masterGain;
            if(ambienceSource)ambienceSource.volume=ambienceGain*backgroundBlend*masterGain;
            if(!running&&!paused&&backgroundBlend<=0f){if(musicSource)musicSource.Stop();if(ambienceSource)ambienceSource.Stop();}
        }
        static void TickVoices(AudioSource[] voices,float[] left)
        {
            if(voices==null||left==null)return;
            for(int i=0;i<left.Length;i++)if(left[i]>0f){left[i]=Mathf.Max(0,left[i]-Time.unscaledDeltaTime);if(left[i]<=0&&voices[i])voices[i].Stop();}
        }
        public void StopAll()
        {
            foreach(var voice in worldVoices??Array.Empty<AudioSource>())if(voice)voice.Stop();
            foreach(var voice in lifecycleVoices??Array.Empty<AudioSource>())if(voice)voice.Stop();
            if(musicSource)musicSource.Stop();if(ambienceSource)ambienceSource.Stop();
            if(worldLeft!=null)Array.Clear(worldLeft,0,worldLeft.Length);if(lifeLeft!=null)Array.Clear(lifeLeft,0,lifeLeft.Length);
            backgroundBlend=0f;paused=false;
        }
    }
}
