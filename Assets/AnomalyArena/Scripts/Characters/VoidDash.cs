using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The void dash, the gift of a won void trial (GameManager.VoidDashUnlocked): Space moves the player at once a fixed distance toward
    /// the cursor's point on the ground, however near or far the cursor is. A wall, a pillar, the map's edge or a hole stops it in front;
    /// monsters do not, it passes through them. It is paid from the sprint's stamina under the sprint's own rule (Stamina): while any
    /// stamina is left a dash may start, and once the bar has run empty, by sprinting or by dashing, none starts until it is full again
    /// (Stamina.Exhausted). A dash spends staminaShare of the bar, or what is left when less is left, which empties it. After a dash the
    /// next one waits the cooldown. A dash that cannot move at all (a wall right in front) spends nothing and starts no cooldown.
    /// The numbers are tuned here, on the player (PlayerController.voidDash); PlayerController.TryVoidDash moves the body.
    /// </summary>
    [Serializable]
    public class VoidDash
    {
        [Range(0f, 1f), Tooltip("Share of the stamina bar one dash spends: 0.5 lets a full bar dash twice")] public float staminaShare = .5f;
        [Min(0f), Tooltip("Cells one dash covers when nothing stands in the way")] public float distance = 5f;
        [Min(0f), Tooltip("Seconds after a dash before the next one can start")] public float cooldown = .5f;

        /// <summary>A dash shorter than this many cells is no dash: nothing is spent.</summary>
        public const float LeastMove = .1f;
        // A sphere cast ignores a collider it starts inside, so a body touching a wall casts from a little behind itself.
        const float CastBack = .1f;
        // The map's own rules along the way (its edge, the layout's walls, the holes, the trial's ground) are read every this many cells.
        const float Slice = .1f;

        float readyAt = float.NegativeInfinity;

        /// <summary>The cooldown after the last dash still runs. It runs on game time, so a pause holds it.</summary>
        public bool CoolingDown => Time.time < readyAt;

        /// <summary>A dash was made: the cooldown starts.</summary>
        public void Started() => readyAt = Time.time + Mathf.Max(0f, cooldown);

        /// <summary>The stamina left after a dash made with value left: the share less, never below empty.</summary>
        public float StaminaAfter(float value) => Mathf.Max(0f, value - Mathf.Max(0f, staminaShare));

        /// <summary>What a dash costs, in the words of the notice that gives it: half the bar at the usual share, else the share in percent.</summary>
        public string CostWords => Mathf.Abs(staminaShare - .5f) < .001f
            ? ArenaText.Pair("半管体力", "half your stamina")
            : ArenaText.Pair($"{staminaShare * 100f:0}% 体力", $"{staminaShare * 100f:0}% of your stamina");

        /// <summary>Where a body of this radius ends a dash from from along direction (flat and of length 1) over at most length cells:
        /// touching the first wall in the way (anything on the wall layer, and the base's footprint that stops bodies only), and short of
        /// the first point off the map, inside a wall of the layout, in a hole, or off the trial's ground. Bodies are not looked at: the
        /// dash passes through them.</summary>
        public static Vector3 Reach(WorldMap world, Vector3 from, Vector3 direction, float length, float radius)
        {
            int walls = GameManager.WallMask | 1 << Combatant.BodyOnlyWallLayer;
            float reach = Mathf.Max(0f, length);
            if (Physics.SphereCast(Query.AtCastHeight(from) - direction * CastBack, radius, direction, out var hit, reach + CastBack, walls,
                    QueryTriggerInteraction.Ignore))
                reach = Mathf.Clamp(hit.distance - CastBack, 0f, reach);
            if (!world) return from + direction * reach;
            float clear = 0f;
            for (int i = 1, slices = Mathf.CeilToInt(reach / Slice); i <= slices; i++)
            {
                float along = reach * i / slices;
                if (!Open(world, from + direction * along)) break;
                clear = along;
            }
            return from + direction * clear;
        }

        static bool Open(WorldMap world, Vector3 at) =>
            (world.TrialActive ? world.OnTrialGround(at) : world.Contains(at)) && !world.IsWall(at) && !world.IsHole(at);
    }
}
