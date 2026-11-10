using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The kinds of monster one campaign day may bring.</summary>
    [Serializable]
    public struct DayKinds
    {
        public EnemyKind[] kinds;
        public DayKinds(params EnemyKind[] kinds) { this.kinds = kinds; }
    }

    /// <summary>How the body research's items of one stat add up.</summary>
    public enum StatLineMode
    {
        /// <summary>One line per stat, bought white, green, blue, purple, orange in turn; a higher step replaces the value of the step
        /// below it instead of adding to it.</summary>
        Replace,
        /// <summary>Every stat at every rarity bought once, in any order, each adding to the others.</summary>
        Stack
    }

    // The campaign's numbers by day (2026-11-10), from the number ledger work/v2-doc-1110/numbers/params.json (its single source; the
    // ledger's notes are in 数值说明-1110.md, sections 4 to 9 and 13): which monsters each day brings (the implementation spec's section F
    // table), their HP from the campaign's own reference DPS by day (the algorithm of EncounterBalance.MaxHp, whose asset and Tyrant.asset
    // the open mode keeps reading), the two bosses, the skill and status scale and the infusion floor by day, the payoff of the effects
    // paid as a share of damage, Tame and Twist, the body research's stat lines, the player's energy, the alien energy each day pays and
    // Day 0's gifts. None of these is in V2CampaignRules.asset, so the asset loads them at these defaults.
    // Not settled yet, each a field here to change: stat lines replace rather than stack (statLineMode); the infusion
    // floor follows a table by day (infusionFloorByDay; empty falls back to infusionFloorPerDay x day); every kill restores one point of
    // energy (energyPerKill); the last two days and the Tyrant are sized for a player who spends everything (dayReferenceDps, tyrantHp).
    public sealed partial class CampaignRules
    {
        [Header("Each day's monsters (since 2026-11-10)")]
        [Tooltip("The kinds day 1, 2, 3 ... may bring; a day past the table takes its last entry. Day d still fights the wave table's wave (WaveNumber): its numbers and its pace stay, only its kinds are these. Day 1 the plain melee monster; day 2 adds the Spitter and the Warden; day 3 the Spire and the Chanter; day 4 the Bloater and the Rammer; day 5 the Stomper; day 6 the Shade instead of the Stomper; day 7 every kind (the Skitter, the Splitter and the Brute only then).")]
        public DayKinds[] dayKinds =
        {
            new(EnemyKind.Grunt),
            new(EnemyKind.Grunt, EnemyKind.Spitter, EnemyKind.Warden),
            new(EnemyKind.Grunt, EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Spire, EnemyKind.Chanter),
            new(EnemyKind.Grunt, EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Spire, EnemyKind.Chanter, EnemyKind.Bloater, EnemyKind.Rammer),
            new(EnemyKind.Grunt, EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Spire, EnemyKind.Chanter, EnemyKind.Bloater, EnemyKind.Rammer, EnemyKind.Stomper),
            new(EnemyKind.Grunt, EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Spire, EnemyKind.Chanter, EnemyKind.Bloater, EnemyKind.Rammer, EnemyKind.Shade),
            new(EnemyKind.Grunt, EnemyKind.Brute, EnemyKind.Skitter, EnemyKind.Spitter, EnemyKind.Rammer, EnemyKind.Bloater, EnemyKind.Warden, EnemyKind.Splitter,
                EnemyKind.Spire, EnemyKind.Stomper, EnemyKind.Shade, EnemyKind.Chanter)
        };
        [Min(1), Tooltip("Spawn weight of a kind the day allows but its wave's table does not list (the Bloater on day 4, the Spire and the Chanter on days 4 and 5, the Shade on day 6): the table's weight of an ordinary special.")]
        public int unlistedKindWeight = 6;

        [Header("Each day's monster HP (since 2026-11-10; the open mode keeps EncounterBalance)")]
        [Tooltip("The campaign's reference DPS on days 1, 2, 3 ...; a day past the table takes its last entry. A monster's HP = this x its seconds to kill / (1 + its opening shield share / the shield's direct damage factor), as EncounterBalance.MaxHp does with the classic reference DPS.")]
        public float[] dayReferenceDps = { 10f, 16f, 35f, 70f, 130f, 240f, 580f };
        [Min(1f), Tooltip("The reference DPS in the Tyrant's fight, for the monsters it calls (the Tyrant's own HP is tyrantHp).")]
        public float tyrantReferenceDps = 930f;
        [Min(.05f), Tooltip("Seconds to kill a melee monster (the plain one, the Skitter, the Splitter, the Brute), at the day's reference DPS.")]
        public float fodderSeconds = .375f;
        [Tooltip("EnemyKind order (14 kinds, Chanter last). Seconds to kill an ordinary monster that is not melee, at the day's reference DPS (the game's own ratios between kinds, averaging 1.8 s). The melee and boss entries are not read.")]
        public float[] functionSeconds = { .375f, .375f, .375f, 1.47f, 1.96f, 1.31f, 1.88f, .375f, 2.45f, 2.29f, 1.15f, 0f, 0f, 1.88f };
        [Tooltip("EnemyKind order (14 kinds, Chanter last). Seconds to kill an elite, at the day's reference DPS (the game's own ratios, averaging 6 s). Only the specials become elites; the other entries are not read.")]
        public float[] eliteSeconds = { 6f, 6f, 6f, 6f, 6.21f, 5.38f, 6f, 6f, 6.62f, 6.62f, 4.97f, 0f, 0f, 6.21f };

        [Header("The two bosses in the campaign (since 2026-11-10)")]
        [Min(1f), Tooltip("The Nash Prophet's HP in the void trial, whatever the day; it has no shield. The black hole's check asks for holeCheckProphetShare of it.")]
        public float prophetHp = 8200f;
        [Min(1f), Tooltip("The Tyrant's HP, its shields not counted.")]
        public float tyrantHp = 43000f;
        [Range(0f, 1f), Tooltip("The Tyrant's opening shield as a share of its max HP.")]
        public float tyrantStartShieldShare = .116f;
        [Range(0f, 1f), Tooltip("The shield the Tyrant raises once on entering its second phase, as a share of its max HP.")]
        public float tyrantPhaseShieldShare = .175f;

        [Header("Skills, statuses and payoffs by day (since 2026-11-10)")]
        [Tooltip("Days 1, 2, 3 ...: the scale of skills and statuses that grow with the fight (strike, gravity well, poison, chain lightning, fire trail ...), in place of the classic run's wave scale R(w); a day past the table takes its last entry.")]
        public float[] dayCombatScale = { 1.1f, 1.6f, 2.1f, 2.7f, 4.4f, 7.3f, 16f };
        [Min(1f), Tooltip("The same scale in the Tyrant's fight.")]
        public float tyrantCombatScale = 24f;
        [Tooltip("Days 1, 2, 3 ...: the infusion floor per infusion research tier (a burn or poison of the player's deals at least this x the tier a second); a day past the table takes its last entry. Empty: infusionFloorPerDay x day x tier.")]
        public float[] infusionFloorByDay = { 2f, 3f, 7f, 10f, 17f, 27f, 61f };
        [Min(0f), Tooltip("The infusion floor per tier in the Tyrant's fight.")]
        public float tyrantInfusionFloor = 90f;
        [Tooltip("Days 1, 2, 3 ...: the effects paid as a share of the damage dealt (Light heals, Earth shields, Void and the void enchant restore energy, Radiance heals) are multiplied by this, so they pay what they did in the classic run; a day past the table takes its last entry.")]
        public float[] payoffScaleByDay = { .85f, .75f, .457f, .286f, .2f, .129f, .064f };
        [Min(1f), Tooltip("Tame: the tamed weapon attacks this many times as fast, on top of what taming did before.")]
        public float tameAttackSpeed = 1.25f;
        [Min(1f), Tooltip("Twist: the twisted weapon deals this many times the damage, on top of the twist itself.")]
        public float twistDamage = 1.5f;
        [Tooltip("Replace: each stat is one line, bought white, green, blue, purple, orange in turn, and a higher step replaces the value of the one below. Stack: every stat at every rarity once, in any order, each adding to the others.")]
        public StatLineMode statLineMode = StatLineMode.Replace;

        [Header("The player's energy (the skill bar) in the campaign (since 2026-11-10)")]
        [Tooltip("The energy bar fills up as each day's morning begins.")]
        public bool dawnEnergyRefill = true;
        [Min(0f), Tooltip("Energy each monster's death restores (bosses and the monsters a boss calls excepted).")]
        public float energyPerKill = 1f;
        [Min(0f), Tooltip("Energy a second the void armor enchant restores (the classic run's void energy regeneration stays for the open mode).")]
        public float voidArmorEnergyRegen = 1f;

        [Header("Alien energy by day (since 2026-11-10)")]
        [Tooltip("Days 1, 2, 3 ...: the alien energy the morning's kills drop in all; a day past the table takes its last entry. Each kill drops its share of what is left by its seconds to kill against the bodies still to come, and the last kill drops the rest, so killing every monster of the day pays exactly this.")]
        public int[] dayKillEnergy = { 20, 32, 52, 80, 120, 176, 240 };
        [Tooltip("Nights 1, 2, 3 ...: the alien energy the night's dark matter nodes carry in all, shared evenly by them; a night past the table takes its last entry.")]
        public int[] nightEnergy = { 5, 8, 13, 20, 30, 44, 60 };

        [Header("Day 0's gifts (since 2026-11-10)")]
        [Min(0), Tooltip("Alloy Day 0 gives in all: its two ores on the way share dayZeroAlloy - arrivalAlloy, and the arrival tops what was mined up to this.")]
        public int dayZeroAlloy = 10;
        [Min(0), Tooltip("Alien energy Day 0 gives in all: the first fight's monsters drop one each, and the arrival tops what was picked up (or still lies on the ground) up to this.")]
        public int dayZeroEnergy = 15;
        [Tooltip("The tutorial night's own ore (tutorialNightOre nodes of tutorialNightOreAlloy). Off, so Day 0 gives dayZeroAlloy in all.")]
        public bool dayZeroNightOre = false;

        static readonly CardRarity[] StatLineSteps = { CardRarity.Common, CardRarity.Rare, CardRarity.Exceptional, CardRarity.Epic, CardRarity.Legendary };

        // ───── Pure rules (tested without a scene) ─────

        /// <summary>The table's entry for day (from 1); a day past the table takes its last entry, day 0 or an empty table the fallback.</summary>
        public static float ByDay(float[] table, int day, float fallback)
        {
            if (table == null || table.Length == 0 || day <= 0) return fallback;
            return table[Mathf.Min(day, table.Length) - 1];
        }

        /// <summary>Whether day (from 1; Day 0 counts as day 1) may bring this kind. An empty table allows every kind.</summary>
        public bool KindOnDay(int day, EnemyKind kind)
        {
            if (dayKinds == null || dayKinds.Length == 0) return true;
            var kinds = dayKinds[Mathf.Clamp(day, 1, dayKinds.Length) - 1].kinds;
            return kinds != null && Array.IndexOf(kinds, kind) >= 0;
        }

        /// <summary>A kind's spawn weight on day: 0 when the day does not allow it; else the wave table's saved weight, or
        /// unlistedKindWeight when the table does not list it.</summary>
        public int DayKindWeight(int day, EnemyKind kind, int savedWeight) =>
            !KindOnDay(day, kind) ? 0 : savedWeight > 0 ? savedWeight : Mathf.Max(1, unlistedKindWeight);

        /// <summary>The reference DPS of day (Day 0 counts as day 1), or of the Tyrant's fight.</summary>
        public float DayReferenceDps(int day, bool tyrantFight = false) =>
            Mathf.Max(1f, tyrantFight ? tyrantReferenceDps : ByDay(dayReferenceDps, Mathf.Max(1, day), 10f));

        /// <summary>Seconds of the reference DPS a plain or elite body of this kind takes to kill: melee bodies that are not elite take
        /// fodderSeconds, the rest their kind's entry (1.8 or 6 when the table has none).</summary>
        public float TargetSeconds(EnemyKind kind, bool elite)
        {
            if (!elite && EnemyRoles.IsMelee(kind)) return Mathf.Max(.05f, fodderSeconds);
            var table = elite ? eliteSeconds : functionSeconds;
            float seconds = table != null && (int)kind < table.Length ? table[(int)kind] : 0f;
            return seconds > 0f ? seconds : elite ? 6f : 1.8f;
        }

        /// <summary>A body's max HP in the campaign: the Nash Prophet prophetHp, the Tyrant tyrantHp; any other the day's reference DPS x its
        /// seconds x miniShare / (1 + its opening shield share / shieldDirectFactor), rounded as EncounterBalance.MaxHp rounds, at least 1.</summary>
        public float BodyHp(EnemyKind kind, int day, bool elite, float shieldShare, float shieldDirectFactor, float miniShare = 1f, bool tyrantFight = false)
        {
            if (kind == EnemyKind.Hollow) return Mathf.Max(1f, prophetHp);
            if (kind == EnemyKind.Tyrant) return Mathf.Max(1f, tyrantHp);
            float budget = DayReferenceDps(day, tyrantFight) * TargetSeconds(kind, elite) * Mathf.Clamp01(miniShare);
            return Mathf.Max(1f, Mathf.Round(budget / (1f + Mathf.Max(0f, shieldShare) / Mathf.Max(.01f, shieldDirectFactor))));
        }

        /// <summary>The scale of skills and statuses on day, or in the Tyrant's fight; 0 on Day 0, where the classic rule (1) stays.</summary>
        public float CombatScaleOn(int day, bool tyrantFight) => tyrantFight ? Mathf.Max(1f, tyrantCombatScale) : day <= 0 ? 0f : ByDay(dayCombatScale, day, 0f);

        /// <summary>The factor on the effects paid as a share of damage on day; 1 on Day 0 or with an empty table.</summary>
        public float PayoffScaleOn(int day) => day <= 0 ? 1f : Mathf.Max(0f, ByDay(payoffScaleByDay, day, 1f));

        /// <summary>The alien energy the morning's kills of day drop in all.</summary>
        public int DayKillEnergy(int day) => ByDay(dayKillEnergy, day);
        /// <summary>The alien energy the night's dark matter of night carries in all.</summary>
        public int NightEnergy(int day) => ByDay(nightEnergy, day);

        /// <summary>What one kill drops of the day's kill energy still left: its share by weight (its seconds to kill) against the weight of
        /// the bodies still to come, the fraction rounded up with its own chance (roll, 0 to 1) so the average stays the share; all that is
        /// left when nothing is still to come. Never more than left, so the day pays exactly its table entry once its last body falls.</summary>
        public static int KillEnergyShare(int left, float weight, float stillToCome, float roll)
        {
            if (left <= 0) return 0;
            if (stillToCome <= 0f) return left;
            if (weight <= 0f) return 0;
            float expected = left * weight / (weight + stillToCome);
            int whole = Mathf.FloorToInt(expected);
            return Mathf.Clamp(whole + (roll < expected - whole ? 1 : 0), 0, left);
        }

        /// <summary>How much a stat's value must be added (RunStats.Add) to go from one step of its line to the next: Damage chains by
        /// multiplying, so it takes (1 + to) / (1 + from) - 1; every other stat the difference.</summary>
        public static float StatLineStep(StatId stat, float from, float to) =>
            stat == StatId.Damage ? (1f + to) / Mathf.Max(.0001f, 1f + from) - 1f : to - from;

        /// <summary>The rarity of a stat line's step after `stepsBought` steps (0: white), or null once the line is at orange.</summary>
        public static CardRarity? StatLineRarity(int stepsBought) =>
            stepsBought >= 0 && stepsBought < StatLineSteps.Length ? StatLineSteps[stepsBought] : null;

        /// <summary>The alloy of Day 0's ores on the way: dayZeroAlloy less arrivalAlloy, shared by them.</summary>
        public int[] DayZeroOreAlloy(int ores = 2) => Split(Mathf.Max(0, dayZeroAlloy - arrivalAlloy), ores);

        /// <summary>What the arrival at the base gives: Day 0's alloy and energy less what the day already brought.</summary>
        public CampaignCost DayZeroArrival(int alloyBrought, int energyBrought) =>
            new(Mathf.Max(0, dayZeroAlloy - Mathf.Max(0, alloyBrought)), Mathf.Max(0, dayZeroEnergy - Mathf.Max(0, energyBrought)));
    }
}
