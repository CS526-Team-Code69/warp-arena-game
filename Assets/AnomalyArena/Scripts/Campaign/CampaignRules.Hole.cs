using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Numbers the campaign gained on 2026-11-10 (the implementation spec of the new V2 doc, sections B, C and D): the black hole's timed
    /// damage check and the Nash Prophet's answer through it, the void currencies and what they buy, the afternoon's alloy by day and the
    /// night's dark matter by night. The new asset fields keep these code defaults until the asset is saved again. Tooltips name the
    /// numbers the spec left open; they are first guesses to tune after play.
    /// </summary>
    public sealed partial class CampaignRules
    {
        [Header("Black hole: timed damage check (since 2026-11-10)")]
        [Min(1f), Tooltip("Seconds from the first hit on the black hole in which its damage must reach the requirement. Not reached: the side quest fails for the day and the hole comes back the next morning, its damage from zero.")]
        public float holeCheckSeconds = 45f;
        [Min(0f), Tooltip("Damage the black hole must take within holeCheckSeconds to collapse into the hidden gate. 0 = holeCheckProphetShare of the Nash Prophet's HP as the campaign would give it on that day.")]
        public float holeCheckDamage = 0f;
        [Range(0f, 2f), Tooltip("With holeCheckDamage 0: the requirement as a share of the Nash Prophet's HP (half: a build that deals it in time, dodging, beats the Prophet in about 90 s).")]
        public float holeCheckProphetShare = .5f;
        [Min(0f), Tooltip("In the afternoon, cells from the black hole within which the afternoon turns into a fight (weapons fire), so the hole can be attacked. A first guess.")]
        public float holeFightRadius = 26f;
        [Min(0f), Tooltip("Seconds after the check starts before the Nash Prophet first answers through the hole.")]
        public float holeCounterDelay = 3f;
        [Min(.5f), Tooltip("Seconds between its answers at the check's start (no damage dealt yet).")]
        public float holeCounterIntervalStart = 6f;
        [Min(.5f), Tooltip("Seconds between its answers as the damage reaches the requirement; in between it follows the damage share.")]
        public float holeCounterIntervalEnd = 4f;
        [Min(1), Tooltip("Void pillars of one answer at the check's start: the first on the player, the others round them.")]
        public int holePillarsMin = 3;
        [Min(1), Tooltip("Void pillars of one answer as the damage reaches the requirement.")]
        public int holePillarsMax = 5;
        [Min(.1f), Tooltip("Radius of one void pillar's warning circle, in cells.")]
        public float holePillarRadius = 2.5f;
        [Min(0f), Tooltip("Seconds a pillar's circle warns before the pillar comes down.")]
        public float holePillarWarning = 1.2f;
        [Min(0f), Tooltip("Seconds a pillar stands once down; a player whose centre is inside it dies, the Nash Prophet's own pillar rule. A first guess (the Prophet's own pillars stand longer).")]
        public float holePillarLife = 1.5f;
        [Min(0f), Tooltip("How far from the player the other pillars of an answer land, in pillar radii; above 2 leaves gaps between them to step through.")]
        public float holePillarSpread = 2.2f;

        [Header("Black hole: fed first (since 2026-11-10)")]
        [Min(0), Tooltip("Monsters the black hole must swallow, lured into its pull by the player, before it can be attacked. 0 skips the feeding.")]
        public int holeFeedGoal = 10;
        [Min(.1f), Tooltip("The campaign hole's pull as a share of the player's walking speed: a little above 1, so a walk loses ground slowly and a sprint gets out, and the player can stand in the pull to lure monsters in.")]
        public float holePullFactor = 1.1f;
        [Min(0), Tooltip("In the afternoon no monsters are out, so the hole calls packs of this many while it is being fed and the player is near it.")]
        public int holeLurePack = 4;
        [Min(.5f), Tooltip("Seconds between two such packs.")]
        public float holeLureInterval = 5f;
        [Min(0), Tooltip("Most monsters of those packs alive at once.")]
        public int holeLureAlive = 6;

        [Header("Void currencies (since 2026-11-10)")]
        [Min(0), Tooltip("Void energy the won void trial gives in the campaign (the gift of the void).")]
        public int voidEnergyReward = 100;
        [Min(0), Tooltip("Void alloy the won void trial gives in the campaign.")]
        public int voidAlloyReward = 30;
        [Min(0), Tooltip("Void alloy nodes that grow each afternoon round where the black hole stood, once the Nash Prophet is beaten.")]
        public int voidOreNodes = 4;
        [Min(1), Tooltip("Void alloy from one of those nodes.")]
        public int voidOreYield = 5;
        [Min(2f), Tooltip("Cells from where the black hole stood within which its void alloy grows.")]
        public float voidOreReach = 8f;
        [Min(0), Tooltip("Void energy to learn one void skill (Pocket Void, Void Walk or Singularity; they have no levels).")]
        public int voidSkillCost = 100;
        [Min(0), Tooltip("Void alloy to void-enchant one weapon: its hits restore energy, as the Void element does.")]
        public int voidWeaponEnchantCost = 15;
        [Min(0), Tooltip("Void alloy to raise the armament's Mk from voidMkFrom to the next, the void Mk (black mark).")]
        public int voidMkCost = 25;
        [Min(1), Tooltip("The Mk the armament must have reached before the void Mk can be bought.")]
        public int voidMkFrom = 10;
        [Min(0), Tooltip("Void alloy to void-enchant the armor: energy restores over time.")]
        public int voidArmorCost = 20;

        [Header("Mined each day (since 2026-11-10)")]
        [Tooltip("Alloy in each afternoon's ore, days 1, 2, 3 ...; later days use the last entry. Shared evenly by the day's ore nodes (the remainder goes to the nodes nearest the black hole) and multiplied by the ship's ore bonus.")]
        public int[] afternoonAlloy = { 20, 30, 45, 70, 105, 160, 240 };
        [Tooltip("Days 1, 2, 3 ...: the mining lamps each afternoon's extra alloy pays for (lampCost each), added to afternoonAlloy after the ship's ore bonus; later days use the last entry. From day 1 on lamps are crafted (dailyFreeLamps), so this rises with the nights.")]
        public int[] dayLampsToCraft = { 3, 3, 4, 4, 5, 5, 6 };
        [Tooltip("Dark matter in each night's nodes, nights 1, 2, 3 ...; later nights use the last entry. Shared evenly by the night's dark matter nodes (Day 0's tutorial night places its own).")]
        public int[] nightDark = { 10, 12, 15, 18, 28, 36, 45 };

        /// <summary>The table's entry for day (from 1); a day past the table takes its last entry, day 0 or a missing table 0.</summary>
        public static int ByDay(int[] table, int day)
        {
            if (table == null || table.Length == 0 || day <= 0) return 0;
            return Mathf.Max(0, table[Mathf.Min(day, table.Length) - 1]);
        }

        /// <summary>The day's afternoon alloy: the table's entry times the ore bonus, rounded half up to whole alloy. 1 + 0.3 is
        /// 1.29999995 as a float, so 45 x 1.3 and 105 x 1.3 come out a hair under the half; the small margin makes them 59 and 137, as
        /// the V2 doc's ledger has them, where plain rounding gave 58 and 136.</summary>
        /// <summary>The alloy a day's afternoon adds for its lamps: dayLampsToCraft lamps at lampCost each (none before day 1).</summary>
        public int LampAlloyOn(int day) => day < 1 ? 0 : ByDay(dayLampsToCraft, day) * Mathf.Max(0, lampCost);

        public static int AfternoonAlloyTotal(int[] table, int day, float scale) =>
            Mathf.Max(0, Mathf.FloorToInt(ByDay(table, day) * Mathf.Max(0f, scale) + .5f + 1e-3f));

        /// <summary>total shared out over count parts as evenly as whole numbers allow: the first (total mod count) parts get one more.
        /// The parts always add up to total; none for count 0.</summary>
        public static int[] Split(int total, int count)
        {
            if (count <= 0) return new int[0];
            total = Mathf.Max(0, total);
            var parts = new int[count];
            int each = total / count, rest = total % count;
            for (int i = 0; i < count; i++) parts[i] = each + (i < rest ? 1 : 0);
            return parts;
        }

        /// <summary>The campaign hole's pull against a player who walks at this speed.</summary>
        public static float HolePull(float factor, float walkSpeed) => Mathf.Max(.1f, factor) * Mathf.Max(0f, walkSpeed);

        /// <summary>How many monsters the next pack brings: a full pack, or what the most alive at once still leaves room for.</summary>
        public static int HoleLureCount(int pack, int mostAlive, int alive) => Mathf.Clamp(Mathf.Min(pack, mostAlive - alive), 0, Mathf.Max(0, pack));

        /// <summary>Damage the black hole's check asks for: fixedDamage when set, else share of the Nash Prophet's HP; at least 1.</summary>
        public static float HoleCheckRequirement(float fixedDamage, float share, float prophetHp) =>
            fixedDamage > 0f ? fixedDamage : Mathf.Max(1f, Mathf.Round(Mathf.Max(0f, share) * Mathf.Max(0f, prophetHp)));

        /// <summary>Pillars of one answer at this damage share (0 to 1): from fewest to most, rounded.</summary>
        public static int HolePillarCount(int fewest, int most, float progress)
        {
            fewest = Mathf.Max(1, fewest);
            most = Mathf.Max(fewest, most);
            return Mathf.Clamp(fewest + Mathf.RoundToInt((most - fewest) * Mathf.Clamp01(progress)), fewest, most);
        }

        /// <summary>Where an answer's pillars land: the first on the player, the others evenly round them at spread, the ring turned by
        /// turnDegrees, so a pillar is always under the player and the gaps between the others are the way out.</summary>
        public static List<Vector3> HolePillarSpots(Vector3 player, int count, float spread, float turnDegrees)
        {
            var spots = new List<Vector3>();
            if (count <= 0) return spots;
            Vector3 centre = Query.Flat(player);
            spots.Add(centre);
            int around = count - 1;
            for (int i = 0; i < around; i++)
            {
                float a = (turnDegrees + 360f * i / around) * Mathf.Deg2Rad;
                spots.Add(centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Mathf.Max(0f, spread));
            }
            return spots;
        }
    }
}
