using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The Nash Prophet answering through the campaign's black hole (2026-11-10): while the player attacks the hole (V2Campaign.Hole.cs)
    /// the Prophet, still out of play in the void, casts two of its own moves onto the map. They keep its own rules and numbers
    /// (Enemy.Prophet.cs): its void pillars, where a standing pillar kills a player whose centre is inside it (TickVoidPillars), and its
    /// equilibrium ring, evenly spaced shots of NashDamage(nashRingDamage) at nashRingSpeed with the first aimed at the player, through
    /// the boss shot pool. Only where they land and come from is the hole's: the pillars round the player as the campaign places them,
    /// the ring out of the hole's rim (a shot inside the kill core would be swallowed at once). The Prophet's body stays inactive; it
    /// owns the pillars and the shots, so a death by them is its kill. The trial's fight clears them (BeginProphetFight).
    /// </summary>
    public partial class Enemy
    {
        /// <summary>Void pillars at these points, as its own cast lays them (PlaceVoidPillars), with the given radius, warning and
        /// life; the warning sound plays at soundAt.</summary>
        public void EchoPillars(IReadOnlyList<Vector3> centres, float pillarRadius, float warning, float life, Vector3 soundAt)
        {
            if (!IsProphet || centres == null || centres.Count == 0) return;
            foreach (var at in centres)
                voidPillars.Add(new VoidPillar
                    { centre = Query.Flat(at), radius = Mathf.Max(.1f, pillarRadius), warning = Mathf.Max(0f, warning), life = Mathf.Max(0f, life) });
            V5WeaponAudio.At(V5Sfx.NashPillarWarn, soundAt);
        }

        /// <summary>One equilibrium ring (its phase-one ring: nashRingShots evenly spaced, the first at the player) fired outward from
        /// centre, each shot starting muzzle cells out.</summary>
        public void EchoRing(Vector3 centre, float muzzle, PlayerController player)
        {
            if (!IsProphet || !GM || !GM.waves) return;
            var b = Boss;
            centre = Query.Flat(centre);
            int count = Mathf.Max(1, b.nashRingShots);
            float spacing = 360f / count;
            Vector3 aim = player ? Query.Flat(player.Position - centre) : Vector3.forward;
            float first = aim.sqrMagnitude > 1e-4f ? Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg : 0f;
            float damage = NashDamage(b.nashRingDamage);
            for (int i = 0; i < count; i++)
            {
                float angle = (first + spacing * i) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                GM.waves.LaunchTrackingBossBolt(this, centre + direction * Mathf.Max(0f, muzzle), direction, damage, b.nashRingSpeed, 0f, 0f);
            }
            V5WeaponAudio.At(V5Sfx.NashRing, centre);
        }

        /// <summary>Ages the answered pillars by dt; a standing one kills the player inside it (TickVoidPillars, its own pillar rule).</summary>
        public void TickEcho(float dt, PlayerController player)
        {
            if (IsProphet && dt > 0f) TickVoidPillars(dt, player);
        }

        /// <summary>The answered pillars go at once (the check is over).</summary>
        public void ClearEcho() => voidPillars.Clear();
    }
}
