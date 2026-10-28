using System;
using UnityEngine;

namespace AnomalyArena
{

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

        public void Play(ArenaWorldCue cue,Vector3 at)
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
            if(free<0)return;
            var voice=voices[free];voice.Stop();voice.transform.position=at;
            voice.pitch=1f;voice.volume=sound.gain*(life?lifecycleGain:worldGain)*masterGain;
            voice.clip=sound.clip;voice.Play();left[free]=sound.clip.length;assignments[free]=cue;
            nextCue[(int)cue]=Time.unscaledTime+sound.minimumInterval;
        }
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

            bool fadeOut=gm&&gm.State==GameState.Playing&&(gm.RunPhase is ArenaUiState.LevelUp or ArenaUiState.Cutscene or ArenaUiState.TrialReturn
                ||gm.RunPhase==ArenaUiState.ReplaceSkill&&gm.waves&&gm.waves.Phase==WavePhase.WaveActive);
            bool running=gm&&gm.State==GameState.Playing&&!fadeOut;
            if(!paused)backgroundBlend=Mathf.MoveTowards(backgroundBlend,running?1f:0f,Time.unscaledDeltaTime/Mathf.Max(.01f,backgroundFadeSeconds));
            var world=WorldMap.Active;
            if(world&&(selectedBiome!=world.biome || selectedTrial!=world.TrialActive))
            {
                selectedBiome=world.biome; selectedTrial=world.TrialActive;
                string name=selectedBiome?selectedBiome.name:"Wreck";
                ArenaWorldCue cue=selectedTrial?ArenaWorldCue.AmbientRift:name.StartsWith("Dust",StringComparison.OrdinalIgnoreCase)?ArenaWorldCue.AmbientDust:name.StartsWith("Rift",StringComparison.OrdinalIgnoreCase)?ArenaWorldCue.AmbientRift:ArenaWorldCue.AmbientWreck;
                if(ambienceSource){ambienceSource.Stop();ambienceSource.clip=byCue[(int)cue]?.clip;}
            }
            if(running&&!paused)
            {
                if(musicSource&&!musicSource.clip)musicSource.clip=byCue[(int)ArenaWorldCue.CombatMusic]?.clip;
                if(musicSource&&musicSource.clip&&!musicSource.isPlaying)musicSource.Play();
                if(ambienceSource&&ambienceSource.clip&&!ambienceSource.isPlaying)ambienceSource.Play();
            }
            if(musicSource)musicSource.volume=musicGain*backgroundBlend*masterGain;
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
