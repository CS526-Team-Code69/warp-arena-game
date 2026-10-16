using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    [DefaultExecutionOrder(1300)]
    public sealed class V3FullAudioLifecycle : MonoBehaviour
    {
        public GameManager game;
        public V3WorldAudio audioBank;
        [Tooltip("Read cycle distance from the saved Run clip and reference speed; positive values override it.")] public float stepDistanceOverride;
        readonly Dictionary<Enemy,int> bossPhases=new();
        HolePhase[] holePhases;
        ArenaUiState previousPhase;
        GameState previousState;
        Vector3 lastPlayerPoint;
        float stepDistance,travelled;
        bool wasSprinting,subscribed;
        void Start()
        {
            if(!game)game=GetComponent<GameManager>();if(!audioBank)audioBank=GetComponent<V3WorldAudio>();
            if(!game||!audioBank)return;
            game.waves.EnemySpawned+=OnEnemySpawned;game.waves.EnemyEliminated+=OnEnemyDied;game.waves.WaveCompleted+=OnWaveEnded;
            game.progression.LevelGained+=OnLevelGained;game.player.Eliminated+=OnPlayerEliminated;
            if(game.skills){game.skills.CastSucceeded+=OnSkillCast;game.skills.VisualRequested+=OnSkillImpact;}
            previousPhase=game.RunPhase;previousState=game.State;lastPlayerPoint=game.player.Position;
            var model=game.player.GetComponent<V2ModelPresentation>();
            stepDistance=stepDistanceOverride>0?stepDistanceOverride:model&&model.runClip?model.runReferenceSpeed*model.runClip.length*.5f:1.8f;
            var world=WorldMap.Active;
            if(world){holePhases=new HolePhase[world.holes.Length];for(int i=0;i<holePhases.Length;i++)holePhases[i]=world.holes[i].Phase;}
            subscribed=true;
        }
        void OnDestroy()
        {
            if(!subscribed||!game)return;
            if(game.waves){game.waves.EnemySpawned-=OnEnemySpawned;game.waves.EnemyEliminated-=OnEnemyDied;game.waves.WaveCompleted-=OnWaveEnded;}
            if(game.progression)game.progression.LevelGained-=OnLevelGained;
            if(game.player)game.player.Eliminated-=OnPlayerEliminated;
            if(game.skills){game.skills.CastSucceeded-=OnSkillCast;game.skills.VisualRequested-=OnSkillImpact;}
        }
        void OnEnemySpawned(Enemy enemy)
        {
            if(enemy.IsBoss){bossPhases[enemy]=enemy.BossPhase;audioBank.Play(ArenaWorldCue.BossEnter,enemy.Position);}
            else if(enemy.definition&&enemy.definition.kind==EnemyKind.Shade)audioBank.Play(ArenaWorldCue.NpcShadeSpawn,enemy.Position);
        }
        void OnEnemyDied(Enemy enemy)
        {
            if(enemy.IsBoss){bossPhases.Remove(enemy);audioBank.Play(ArenaWorldCue.BossDeath,enemy.Position);}
            else audioBank.Play(ArenaWorldCue.NpcDeath,enemy.Position);
        }
        void OnWaveEnded(int wave)=>audioBank.Play(ArenaWorldCue.WaveEnd,game.player.Position);
        void OnLevelGained(int level)=>audioBank.Play(ArenaWorldCue.LevelUp,game.player.Position);
        void OnPlayerEliminated(Combatant player)=>audioBank.OnPlayerDeath(player);
        void OnSkillCast(SkillId id,Vector3 point,float radius,float duration)
        {

            var cue=id switch
            {
                SkillId.FlameWake=>ArenaSoundCue.Spray,SkillId.Contagion=>ArenaSoundCue.Poison,
                SkillId.FrostNova=>ArenaSoundCue.Ice,SkillId.StormCall=>ArenaSoundCue.Arc,
                SkillId.Bulwark=>ArenaSoundCue.Gravity,_=>ArenaSoundCue.Pulse
            };
            V2Audio.Instance?.PlayWorld(cue,point);
        }
        void OnSkillImpact(SkillId id,Vector3 point,float radius,float duration)
        {
            if(id==SkillId.StormCall)V2Audio.Instance?.PlayWorld(ArenaSoundCue.Arc,point);
        }
        void LateUpdate()
        {
            if(!subscribed||!game||!game.player||!audioBank)return;
            var point=game.player.Position;float distance=Query.Flat(point-lastPlayerPoint).magnitude;lastPlayerPoint=point;
            bool moving=game.CanPlayerMove&&game.player.IsAlive&&game.player.State==CharacterState.Normal&&!game.player.MovementLocked&&distance>.0001f;
            bool sprinting=moving&&game.player.stamina.Sprinting;
            if(sprinting&&!wasSprinting)audioBank.Play(ArenaWorldCue.PlayerSprint,point);wasSprinting=sprinting;
            if(moving&&distance<3f)
            {
                travelled+=distance;
                if(travelled>=Mathf.Max(.1f,stepDistance))
                {
                    travelled%=Mathf.Max(.1f,stepDistance);
                    string biome=WorldMap.Active&&WorldMap.Active.biome?WorldMap.Active.biome.name:"Wreck";
                    audioBank.Play(biome.StartsWith("Wreck")?ArenaWorldCue.PlayerStepMetal:ArenaWorldCue.PlayerStepStone,point);
                }
            }
            else travelled=0f;
            foreach(var enemy in game.waves.Alive)
            {
                if(!enemy||!enemy.IsBoss||!enemy.IsAlive)continue;
                if(bossPhases.TryGetValue(enemy,out int phase)&&phase!=enemy.BossPhase)audioBank.Play(ArenaWorldCue.BossPhase,enemy.Position);
                bossPhases[enemy]=enemy.BossPhase;
            }
            var world=WorldMap.Active;
            if(world&&holePhases!=null)
                for(int i=0;i<holePhases.Length&&i<world.holes.Length;i++)
                {
                    var hole=world.holes[i];var phase=hole.Phase;
                    if(holePhases[i]!=phase)
                    {
                        if(phase==HolePhase.Portal)audioBank.Play(ArenaWorldCue.PortalOpen,hole.Centre);
                        if(phase==HolePhase.Gone&&holePhases[i]!=HolePhase.Gone)audioBank.Play(ArenaWorldCue.PortalClose,hole.Centre);
                        holePhases[i]=phase;
                    }
                }
            if(previousPhase!=game.RunPhase)
            {
                if(game.RunPhase==ArenaUiState.VoidTrial)audioBank.Play(ArenaWorldCue.PortalEnter,point);
                if(previousPhase!=ArenaUiState.Paused&&game.RunPhase is ArenaUiState.Draft or ArenaUiState.Armory or ArenaUiState.Loadout)audioBank.Play(ArenaWorldCue.CardReveal,point);
                previousPhase=game.RunPhase;
            }
            if(previousState!=game.State)
            {
                if(game.State==GameState.Won)audioBank.Play(ArenaWorldCue.Victory,point);
                if(game.State==GameState.Lost)audioBank.Play(ArenaWorldCue.Defeat,point);
                previousState=game.State;
            }
        }
    }
}
