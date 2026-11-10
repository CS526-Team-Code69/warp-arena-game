using UnityEngine;

namespace AnomalyArena
{
    public sealed partial class RunWeapons
    {
        /// <summary>Build phase (GameManager.BuildTimeFlows): time flows but no weapon is used, so only the timers Tick
        /// runs down move on: cooldowns, reloads, heat, a lost knife's way back and the Meltdown haste. Nothing fires, charges or moves, and
        /// the auto weapons hold where they are.</summary>
        public void TickTimers(float dt)
        {
            float chill=ElementStatus.For(player)?.PlayerChillMultiplier??1f;
            foreach(var w in Hand)
            {
                if(w==null)continue;
                w.cooldown=Mathf.Max(0,w.cooldown-dt*chill*WeaponCooldownRate);w.reload=Mathf.Max(0,w.reload-dt*chill);if(w.cooldown<=0)w.holeReturnSpan=0;
                w.heldTime=0;w.heat=Mathf.Max(0,w.heat-dt);
            }
            float rate=Mathf.Max(.01f,WeaponCooldownRate);
            foreach(var w in Auto)if(w!=null)w.cooldown=Mathf.Max(0,w.cooldown-dt*rate);
            TickMeltdownHaste(dt);TickVoidEnchant(dt);
        }
    }
}
