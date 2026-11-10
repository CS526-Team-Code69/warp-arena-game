using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The player's own inputs that PlayerController reads (E and F are the campaign's own and not among them).</summary>
    [Flags]
    public enum PlayerAct { None = 0, Move = 1, Sprint = 2, Fire = 4, Switch = 8, All = Move | Sprint | Fire | Switch }

    /// <summary>Day 0's taught steps in order (implementation spec 2026-11-10, section A), then the two of the tutorial night. Switch
    /// (the right button, taught the second time a crate is opened) was added after them and stands last so the others keep their numbers.</summary>
    public enum TutorialStep { None, Move, Fire, Mine, Crate, Sprint, Fight, Collect, SecondCrate, Lamp, DarkMatter, Done, Switch }

    /// <summary>
    /// The rules of the forced spotlight tutorial, apart from the scene so they can be tested: which inputs a step lets through, what has
    /// been taught once a step is done, and the step conditions that are plain arithmetic. While a step is taught only its own input works
    /// (spec A); a step at a thing the player must reach (the ore, the crate, the dropped energy)
    /// also lets the player walk, or it could never be done. Between steps everything taught so far works.
    /// </summary>
    public static class SpotlightTutorial
    {
        /// <summary>The inputs that work while this step's spotlight is up.</summary>
        public static PlayerAct Allowed(TutorialStep step) => step switch
        {
            TutorialStep.Move => PlayerAct.Move,
            TutorialStep.Fire => PlayerAct.Fire,
            TutorialStep.Mine or TutorialStep.Crate or TutorialStep.Collect or TutorialStep.DarkMatter => PlayerAct.Move,
            TutorialStep.Sprint => PlayerAct.Move | PlayerAct.Sprint,
            TutorialStep.Fight => PlayerAct.Move | PlayerAct.Sprint | PlayerAct.Fire,
            TutorialStep.Lamp => PlayerAct.None,
            TutorialStep.Switch => PlayerAct.Switch,
            _ => PlayerAct.All
        };

        /// <summary>The inputs that work between steps, once lastDone is the latest step passed: walking after step 1, firing after
        /// step 2, sprinting after step 5, and everything (switching hands too) once the first fight is over.</summary>
        public static PlayerAct Taught(TutorialStep lastDone) => lastDone switch
        {
            TutorialStep.None => PlayerAct.None,
            TutorialStep.Move => PlayerAct.Move,
            TutorialStep.Fire or TutorialStep.Mine or TutorialStep.Crate => PlayerAct.Move | PlayerAct.Fire,
            TutorialStep.Sprint => PlayerAct.Move | PlayerAct.Fire | PlayerAct.Sprint,
            _ => PlayerAct.All
        };

        /// <summary>Whether act works now: forced is the step whose spotlight is up (None between steps).</summary>
        public static bool Lets(PlayerAct act, TutorialStep forced, TutorialStep lastDone) =>
            ((forced != TutorialStep.None ? Allowed(forced) : Taught(lastDone)) & act) != 0;

        /// <summary>How far the player has come from start toward target, on the ground (cells along the line; walking away counts
        /// negative, walking sideways nothing).</summary>
        public static float MovedToward(Vector3 start, Vector3 now, Vector3 target)
        {
            Vector3 way = target - start, moved = now - start;
            way.y = 0f; moved.y = 0f;
            return way.sqrMagnitude < 1e-6f ? moved.magnitude : Vector3.Dot(moved, way.normalized);
        }

        /// <summary>A weapon fired between two looks at its cooldown: firing sets the cooldown to the full interval, which only ever counts
        /// down otherwise.</summary>
        public static bool Fired(float cooldownBefore, float cooldownNow) => cooldownNow > cooldownBefore + 1e-4f;

        /// <summary>The farthest point of a trail along dir from origin (0 for no points): how far the day's ore trail reaches.</summary>
        public static float Reach(IEnumerable<Vector3> trail, Vector3 origin, Vector3 dir)
        {
            float reach = 0f;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return 0f;
            dir.Normalize();
            foreach (var point in trail)
            {
                Vector3 d = point - origin; d.y = 0f;
                reach = Mathf.Max(reach, Vector3.Dot(d, dir));
            }
            return reach;
        }
    }

    /// <summary>Where a weapon taken from a weapon crate goes (the base's research rules, V2Campaign.Base.cs): a hand weapon into a free
    /// hand, else in place of the starting pistol, else in place of the hand not in use; an auto weapon only into a free auto slot.</summary>
    public static class CrateSlots
    {
        /// <summary>The hand a new hand weapon goes into.</summary>
        public static int HandFor(RunWeapon[] hand, int activeIndex)
        {
            if (hand == null || hand.Length == 0) return -1;
            int free = Array.FindIndex(hand, w => w == null);
            if (free >= 0) return free;
            int pistol = Array.FindIndex(hand, w => w != null && w.form == WeaponForm.Pistol);
            if (pistol >= 0) return pistol;
            return hand.Length > 1 ? 1 - Mathf.Clamp(activeIndex, 0, 1) : 0;
        }

        /// <summary>The auto slot a new auto weapon goes into, or -1 when every one is taken.</summary>
        public static int AutoFor(RunWeapon[] auto) => auto == null ? -1 : Array.FindIndex(auto, w => w == null);

        /// <summary>The slot a crate weapon goes into, or -1 when it has nowhere to go.</summary>
        public static int SlotFor(RunWeapon weapon, RunWeapon[] hand, RunWeapon[] auto, int activeIndex) =>
            weapon == null ? -1 : weapon.IsAuto ? AutoFor(auto) : HandFor(hand, activeIndex);
    }
}
