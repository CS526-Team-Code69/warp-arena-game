using UnityEngine;

namespace AnomalyArena
{
    public enum AttackMode { Bolt, Knife, Blast, Field, Pull, Sticky, Bouncy, Swarm, Hook, Swap, Recall, Lob, Shard }
    /// <summary>Actors are pre-placed in ArenaV2. A full pool refuses an attack instead of allocating during combat.</summary>
    public sealed class AttackPool : MonoBehaviour
    {
        public AttackActor[] actors;
        public static AttackPool Instance { get; private set; }
        void Awake()=>Instance=this;
        public AttackActor Acquire()
        {
            foreach(var actor in actors)
            {
                if(!actor || actor.gameObject.activeSelf) continue;
                if(WorldMap.Active && WorldMap.Active.IsBattlefieldFrozen(actor.gameObject)) continue;
                actor.gameObject.SetActive(true);
                return actor;
            }
            return null;
        }
        public void Clear(){foreach(var a in actors)if(a)a.Release();}
    }
}
