using UnityEngine;

namespace AnomalyArena
{
    public sealed partial class RunWeapons
    {
                                                                                                                                          
                                                                                                                                             
                                                           
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
