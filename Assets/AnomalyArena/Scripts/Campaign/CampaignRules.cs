using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>One value per enemy tier: melee swarm and mini Splitters, ordinary specials, elites (affixed specials).</summary>
    [Serializable]
    public struct TierValues
    {
        public float fodder, special, elite;
        public TierValues(float fodder, float special, float elite) { this.fodder = fodder; this.special = special; this.elite = elite; }
        public float For(CampaignDropTier tier) => tier switch { CampaignDropTier.Elite => elite, CampaignDropTier.Special => special, _ => fodder };
    }

    public enum CampaignDropTier { Fodder, Special, Elite }

    /// <summary>A price in the three V2 currencies.</summary>
    [Serializable]
    public struct CampaignCost
    {
        [Min(0)] public int alloy, energy, dark;
        public CampaignCost(int alloy, int energy = 0, int dark = 0) { this.alloy = alloy; this.energy = energy; this.dark = dark; }
        public bool IsFree => alloy <= 0 && energy <= 0 && dark <= 0;
    }

    /// <summary>
    /// Numbers of the V2 day campaign: waves become days with a day and a night, a base with HP, a shield and four rooms, three currencies,
    /// shadow monsters at night. The defaults equal work/v2-campaign-1106/economy/ledger.py, which checks that a weak, a typical and a strong
    /// player (kills measured by the XP pacing driver on 2026-10-27) all finish every room, the ship and the fuel by the dawn after the last
    /// night. Change a price or a drop here and rerun the ledger with the same value.
    /// </summary>
    [CreateAssetMenu(menuName = "Anomaly Arena/Campaign/Rules")]
    public sealed partial class CampaignRules : ScriptableObject
    {
        [Header("Days")]
        [Min(1), Tooltip("Days before the ignition is forced: 7 since 2026-11-06, one wave a day. Day d fights wave d x waveStride of the table, so day 7 is wave 14 and the 15th (the Tyrant) comes after the failed ignition.")]
        public int days = 7;
        [Min(1), Tooltip("Waves of the wave table fought each day, back to back with a short lull between them (1 since 2026-11-06).")]
        public int wavesPerDay = 1;
        [Min(1), Tooltip("Waves of the table one day steps over: 2 since 2026-11-06, so the 7 days fight waves 2, 4 ... 14 and the difficulty meets the Tyrant.")]
        public int waveStride = 2;
        [Min(0f), Tooltip("Seconds between the waves of one day.")] public float lullSeconds = 4f;
        [Min(0f), Tooltip("The afternoon (dusk) after the day's last wave: rest and mining time, the day's ore shows, no shadows yet. The light fades over this many seconds; without openEndedEvenings the afternoon also ends then and the night begins.")]
        public float duskSeconds = 15f;
        [Min(5f), Tooltip("Seconds of night on day 1; the night is longer by nightSecondsPerDay each day. Only without openEndedEvenings: with it the night lasts until the player waits for dawn at the base.")] public float nightSeconds = 35f;
        [Min(0f)] public float nightSecondsPerDay = 1f;
        [Min(5f), Tooltip("The Day 0 tutorial night. With openEndedEvenings it has no end of its own: after this many seconds its goal points back to the base door, where waiting for dawn starts day 1.")] public float tutorialNightSeconds = 55f;
        [Min(0), Tooltip("Free air drops (armory rounds) per dawn: 0 since 2026-11-06. Every upgrade is bought in its room as the V2 doc places it and takes effect at once.")]
        public int armoryRoundsPerDawn = 0;

        [Header("Kill drops: alien energy (star matter); since 2026-11-06 no alloy")]
        [Tooltip("Chance a kill drops alien energy, per enemy tier (0 = never). Since 2026-11-06 kills drop nothing else: alloy and dark matter are only mined.")] public TierValues energyChance = new(.05f, .30f, 1f);
        [Tooltip("Alien energy in one drop, per enemy tier; grows by energyGrowthPerDay a day.")] public TierValues energyAmount = new(1f, 2f, 2f);
        [Min(0f)] public float energyGrowthPerDay = .06f;
        [Tooltip("Chance a kill drops an alloy shard, per enemy tier. 0 since 2026-11-06 (big map): all alloy is mined.")] public TierValues alloyChance = new(0f, 0f, 0f);
        [Tooltip("Alloy in one shard, per enemy tier; grows by alloyGrowthPerDay a day. Not read since 2026-11-06: kills drop no alloy.")] public TierValues alloyAmount = new(1f, 1f, 2f);
        [Min(0f)] public float alloyGrowthPerDay = .10f;
        [Min(.5f), Tooltip("Cells within which a dropped shard flies to the player.")] public float pickupMagnet = 3.5f;

        [Header("Mining (afternoon and night)")]
        [Min(0), Tooltip("Alloy ore nodes that surface in the afternoon of day 1; one more each later day. Since 2026-11-06 (big map, no clock) every node can be mined, so the nodes are the day's alloy ceiling.")] public int oreNodes = 4;
        [Min(1), Tooltip("Alloy from one ore node: oreYield on days 1-2, + oreYieldStep every oreYieldStepDays days.")] public int oreYield = 3;
        [Min(0)] public int oreYieldStep = 1;
        [Min(1)] public int oreYieldStepDays = 2;
        [Min(0), Tooltip("Dark matter clusters that surface each night, at most one per region. Since 2026-11-06 (big map, no clock) every cluster can be mined, so they are the night's dark matter ceiling.")] public int darkClusters = 2;
        [Min(1), Tooltip("Dark matter nodes in one cluster (4 since 2026-11-06, up from 3); the Day 0 night's cluster has as many.")] public int nodesPerCluster = 4;
        [Min(1), Tooltip("Dark matter from one node.")] public int darkPerNode = 1;
        [Min(0f), Tooltip("Seconds of holding E to mine a node.")] public float mineSeconds = 1.1f;
        [Min(.5f), Tooltip("Cells from a node within which E mines it.")] public float mineRange = 1.9f;
        [Min(0f), Tooltip("No node surfaces closer than this to the base centre.")] public float nodeBaseClearance = 16f;

        [Header("Night: light and darkness")]
        [Range(0f, 1f), Tooltip("Darkness outside every light at night (1 = black).")] public float nightDarkness = .9f;
        [Range(0f, 1f), Tooltip("Extra darkness in a region full of shadows; danger shows as darkness and sound, no marks on the ground.")]
        public float crowdedDarkness = .08f;
        [Range(0f, 1f), Tooltip("Darkness at the end of dusk.")] public float duskDarkness = .55f;
        [Range(0f, 1f), Tooltip("With open-ended evenings, how far the afternoon's light turns toward dusk's end (0 = full daylight, 1 = duskDarkness and the evening light): the afternoon is a rest and mining time in daylight, and the dark comes when the player goes into the night.")]
        public float afternoonLight = .25f;
        [Min(1f), Tooltip("Radius of a placed mining lamp, in cells.")] public float lampRadius = 6.5f;
        [Min(1f), Tooltip("Radius of the base's light at night, in cells: its floodlights light this circle and no shadow comes into it.")] public float baseLightRadius = 15f;
        [Min(0f), Tooltip("The player's own faint glow: shows the feet, does not scare shadows, does not allow mining.")] public float playerGlowRadius = 2.4f;
        [Min(0), Tooltip("Mining lamps the base hands out each night.")] public int freeLampsPerNight = 3;
        [Min(1f), Tooltip("How long a lamp lasts once a crowd of shadows has gone into its light: this many seconds against two of them, less against more.")] public float lampHp = 4f;

        [Header("Night: shadow monsters")]
        [Tooltip("Columns and rows of regions the map is cut into for the kills-to-shadows rule.")] public Vector2Int regions = new(6, 4);
        [Min(1f), Tooltip("Day kills in a region per shadow those kills raise there at night; each is the dark double of a monster killed there.")] public float killsPerShadow = 4f;
        [Min(1), Tooltip("Shadows that make a crowd: that many at one lamp go into its light together and put it out, and a region holding that many sounds and looks its darkest.")] public int overrunShadows = 5;
        [Min(1), Tooltip("Most shadows the kills raise in one region.")] public int maxShadowsPerRegion = 8;
        [Min(1), Tooltip("Most shadows on the whole map.")] public int maxShadows = 72;
        [Min(0f), Tooltip("Shadows every region holds at night whatever was killed there, so the whole map has them.")] public float ambientShadows = 1f;
        [Min(0f), Tooltip("More of those for each day after the first (the sum is rounded). 0: the neighbouring regions' shadows alone must not make a crowd at a lamp, or no place is safe to mine.")] public float ambientShadowsPerDay = 0f;
        [Min(0)] public int tutorialShadows = 4;
        [Min(0f), Tooltip("Cells from the player at which the tutorial night's shadows stop: they follow and watch, and never touch.")] public float tutorialShadowKeep = 5f;
        [Min(0f), Tooltip("A shadow's speed while it only roams.")] public float shadowSpeed = 3.4f;
        [Min(.1f), Tooltip("A hunting shadow's speed as a share of the player's walking speed: above 1, so only a sprint opens the distance.")] public float shadowChaseFactor = 1.12f;
        [Min(0f), Tooltip("Cells within which a shadow notices the player and comes: to the edge of the light the player stands in, or all the way in the dark.")] public float shadowSense = 26f;
        [Min(1f), Tooltip("Cells from the place a shadow rose at within which it hunts: a player farther from that place is left alone, and a shadow on the hunt turns back. It keeps the danger in and beside the regions where the kills fell, so the regions far from them can still be mined.")] public float shadowTerritory = 16f;
        [Min(0f), Tooltip("Cells past a lamp's edge within which a shadow counts toward the crowd at that lamp.")] public float lampGatherBand = 3.5f;
        [Range(0f, 1f), Tooltip("Share of the player's max HP one shadow touch takes.")] public float shadowHitShare = .15f;
        [Min(.1f)] public float shadowHitInterval = .9f;
        [Range(0f, 1f), Tooltip("Share of tonight's haul (everything mined or picked up since dusk, or since the last base visit that night) lost when the player dies at night; 1 = all of it, the rule since 2026-11-06. The base's stock, the rooms and the ship stay, and the next day starts as usual.")]
        public float nightDeathLoss = 1f;
        [Min(.5f), Tooltip("Seconds the fallen player lies in the dark (the death animation plays) before waking at the base.")]
        public float nightDeathSeconds = 2.8f;

        /// <summary>Shadows a region holds at night whatever was killed there, on this day.</summary>
        public int AmbientShadows(int day) => Mathf.Max(0, Mathf.RoundToInt(ambientShadows + ambientShadowsPerDay * Mathf.Max(0, day - 1)));
        /// <summary>Shadows this many kills raise in their region.</summary>
        public int KillShadows(int kills) => Mathf.Clamp(Mathf.FloorToInt(kills / Mathf.Max(1f, killsPerShadow)), 0, maxShadowsPerRegion);
        /// <summary>A hunting shadow's speed against a player who walks at this speed: a little faster, never slower than it roams.</summary>
        public float ShadowChaseSpeed(float playerWalkSpeed) => Mathf.Max(shadowSpeed, playerWalkSpeed * shadowChaseFactor);
        /// <summary>This many shadows at one lamp are a crowd: they go into its light.</summary>
        public bool Crowd(int shadows) => shadows >= overrunShadows;
        /// <summary>A shadow hunts a player it can sense who stands inside its territory, the ground round the place it rose at.</summary>
        public bool ShadowHunts(float shadowToPlayer, float homeToPlayer) => shadowToPlayer <= shadowSense && homeToPlayer <= shadowTerritory;
        /// <summary>Two regions of a grid cols wide touch: side by side or corner to corner, never one and the same.</summary>
        public static bool RegionsTouch(int a, int b, int cols)
        {
            cols = Mathf.Max(1, cols);
            return a != b && Mathf.Abs(a % cols - b % cols) <= 1 && Mathf.Abs(a / cols - b / cols) <= 1;
        }

        [Header("Base")]
        [Min(1f), Tooltip("Base max HP at durability level 1; each level multiplies it by baseHpGrowth (explosive growth, like the enemy pressure).")]
        public float baseHp = 300f;
        [Min(1f)] public float baseHpGrowth = 1.5f;
        [Range(0f, 1f), Tooltip("Shield cap per shield level, as a share of the base max HP.")] public float shieldShare = .15f;
        [Range(0f, 1f), Tooltip("HP the base repairs by itself each night, as a share of its max HP.")] public float nightSelfRepair = .3f;
        [Range(.05f, 1f), Tooltip("A side breaks its room once it takes this share of the base max HP in one day.")] public float sideBreakShare = .25f;
        [Min(0f), Tooltip("Every monster of a day wave raids the base (since 2026-11-06; bosses and monsters that cannot walk excepted). A raider within this many cells of the player fights the player instead; 0 = never.")]
        public float raiderAggroRadius = 7f;

        // ----- Since 2026-11-06 (values from the economy ledger): the campaign's own large map with terrain, an afternoon and a
        // night without a clock (the player moves on from the base door), a limited mineral budget each day, mineral trails that lead to
        // the black hole, and the black hole's side quest that opens the hidden boss. -----
        [Header("Big map, open-ended afternoon and night, mineral trails")]
        [Tooltip("The campaign map's size in cells (x, z). The classic arena is 180 x 120 (WreckField and DustFlats maps).")] public Vector2Int mapSize = new(200, 140);
        [Tooltip("No clock in the afternoon (dusk) or at night: the player goes into the night and on to the next day from the base door (its panel's Into the night and Wait for dawn). Off: both run on duskSeconds and NightLength as before.")] public bool openEndedEvenings = true;
        [Min(0f), Tooltip("Day monsters are born on a ring this many cells from the base centre (not on the far rim of the big map), then walk in. 56 sits between 42, which was too near, and 70, which was too far; tune it after a played day.")] public float daySpawnRing = 56f;
        [Min(0), Tooltip("Ore nodes among each night's nodes, each beside one of the night's dark matter clusters; the rest of the night's nodes are dark matter. 0 since 2026-11-06 (it was 1): its alloy went to the afternoon's scattered nodes (scatterNodes), and afternoon ore left unmined still lies there at night.")] public int nightOreNodes = 0;
        [Min(0), Tooltip("How many of each afternoon's ore nodes lie on the trail toward the black hole (newly seen minerals point the way). At least OreNodes(days) puts every afternoon node on the trail (once the hidden boss has turned the hole into ground, the trail still leads to where it stood); the rest lie at random over the map.")] public int trailNodes = 10;
        [Min(0f), Tooltip("Cells from the base that the trail's farthest node reaches on day 1.")] public float trailReach = 36f;
        [Min(0f), Tooltip("Cells the trail reaches further each later day, until it ends at the black hole.")] public float trailReachPerDay = 25f;
        [Min(1), Tooltip("Trail nodes up at once (2 since 2026-11-06): the next one toward the black hole shows while the nearer one is still there, and mining a trail node brings up the next hidden one, always further toward the hole.")] public int trailShown = 2;
        [Range(.2f, 1f), Tooltip("The trail's last gap (nearest the black hole) as a share of its first (nearest the base); the gaps in between shrink evenly, so the ore lies closer together the nearer the hole. 1 = evenly spaced. Where the nodes lie, not how many: the day's alloy stays the same.")] public float trailEndGap = .5f;
        [Min(0), Tooltip("Afternoon ore nodes off the trail, in the map's other directions: shown from the start, at random on open ground well off the line from the base to the black hole and within scatterReach cells of the base. They come on top of OreNodes(day) and replace the night's own ore node (nightOreNodes 0), so the day's alloy ceiling stays about where it was.")] public int scatterNodes = 2;
        [Min(1), Tooltip("Alloy from one scattered node, the same every day (a trail node gives OreYield(day)).")] public int scatterYield = 2;
        [Min(0f), Tooltip("Cells from the base within which the scattered nodes lie.")] public float scatterReach = 60f;
        [Header("Black hole side quest")]
        [Min(1), Tooltip("Monsters fed to the campaign's black hole to open the hidden boss portal; the three of its first sight count.")] public int holeFeedsToOpen = 20;
        [Min(.5f), Tooltip("Seconds between side-quest monsters while the player is near the black hole and the portal is not open.")] public float holeQuestSpawnSeconds = 3f;
        [Min(0), Tooltip("Most side-quest monsters alive at once.")] public int holeQuestMaxAlive = 5;
        [Min(0f), Tooltip("Cells from the player at which side-quest monsters appear.")] public float holeQuestSpawnDistance = 12f;
        [Min(0f), Tooltip("Cells from the black hole within which the side quest sends monsters.")] public float holeQuestRadius = 18f;
        [Header("Black hole hum (heard only while the hole is out)")]
        [Min(1f), Tooltip("Cells from the black hole's centre within which its hum is heard: silent this far out, growing to full at the hole's gravity ring.")] public float holeHumReach = 45f;
        [Range(0f, 1f), Tooltip("Volume of the hum at the gravity ring, times V3WorldAudio's master gain. The hum is the Nash Prophet's gravity pull loop (Resources/V5/Audio/loop-gravity), which plays at 0.116 during his pull.")] public float holeHumVolume = .15f;
        [Header("Raids and the shield top-up")]
        [Min(0f), Tooltip("Base damage per raider strike, as a share of that raider's attack damage. 0.15 since 2026-11-06, when every day monster became a raider (it was 0.6 for a fifth of the melee swarm).")] public float raidDamage = .15f;
        [Min(0), Tooltip("Energy cost of an emergency shield top-up at the base door by day: this + shieldTopUpCostPerDay per day.")] public int shieldTopUpCost = 4;
        [Min(0)] public int shieldTopUpCostPerDay = 1;
        [Range(0f, 1f)] public float shieldTopUpShare = .25f;
        [Min(0f)] public float shieldTopUpCooldown = 15f;

        [Header("Player levels (bought at the Armament Bay since 2026-11-06)")]
        [Tooltip("Price of the armor upgrade to level 2 (the player's level); each later level costs playerLevelCostPerLevel more. A level gives what an XP level gives in the classic run: max HP and max energy. Kills give no XP in the campaign.")]
        public CampaignCost playerLevelCost = new(2, 3);
        public CampaignCost playerLevelCostPerLevel = new(0, 1);

        [Header("Upgrades bought in the rooms (since 2026-11-06; each takes effect at once, each price rises with every purchase of its kind)")]
        [Tooltip("Armament Bay level 1: boost weapon damage. The weapon in hand (or else another held weapon) rises to a higher Mk.")]
        public CampaignCost weaponResearchCost = new(2, 0, 0);
        public CampaignCost weaponResearchCostPerBuy = new(1, 0, 0);
        [Tooltip("Armament Bay level 2: research a new weapon, which goes into a free weapon slot.")]
        public CampaignCost newWeaponCost = new(4, 0, 0);
        public CampaignCost newWeaponCostPerBuy = new(4, 0, 0);
        [Tooltip("Armament Bay level 2: the rocket launcher, bought once at Mk 1. It takes the place of the hand weapon not in use.")]
        public CampaignCost rocketLauncherCost = new(6);
        [Tooltip("Armament Bay level 2: tame a weapon (a random held weapon that can take it).")]
        public CampaignCost tameCost = new(5, 0, 0);
        public CampaignCost tameCostPerBuy = new(3, 0, 0);
        [Tooltip("Armament Bay level 3: a weapon twist (a random held weapon that can take it).")]
        public CampaignCost twistCost = new(8, 0, 0);
        public CampaignCost twistCostPerBuy = new(4, 0, 0);
        [Tooltip("Energy Lab level 1: upgrade the body (one random stat rises).")]
        public CampaignCost statCost = new(0, 2, 0);
        public CampaignCost statCostPerBuy = new(0, 2, 0);
        [Tooltip("Energy Lab level 2: unlock an alien skill (a new skill, or a level for one already learned).")]
        public CampaignCost skillCost = new(0, 5, 0);
        public CampaignCost skillCostPerBuy = new(0, 3, 0);
        [Tooltip("Energy Lab level 3: infuse a weapon with an element (a random held weapon that can take it).")]
        public CampaignCost infusionCost = new(0, 8, 0);
        public CampaignCost infusionCostPerBuy = new(0, 4, 0);

        [Header("Prices (to level 1, 2, 3)")]
        public CampaignCost[] armamentLevels = { new(8), new(4), new(10) };
        public CampaignCost[] energyLevels = { new(2, 6), new(2, 9), new(5, 24) };
        public CampaignCost[] shipLevels = { new(2), new(7), new(18) };
        [Tooltip("Durability levels 2 to 7.")] public CampaignCost[] durabilityLevels = { new(4), new(7), new(12), new(18), new(26), new(36) };
        [Tooltip("Shield levels 1 to 5.")] public CampaignCost[] shieldLevels = { new(0, 6), new(0, 10), new(0, 17), new(0, 25), new(0, 34) };
        public CampaignCost engineRepair = new(6);
        public CampaignCost armorRepair = new(9);
        [Min(1)] public int fuelUnits = 6;
        [Min(1)] public int darkPerFuelUnit = 5;
        [Min(0)] public int lampCost = 3;
        [Min(0), Tooltip("Room repair, alloy: this + roomRepairPerLevel x level + roomRepairPerDay x day.")] public int roomRepair = 2;
        [Min(0)] public int roomRepairPerLevel = 2;
        [Min(0)] public int roomRepairPerDay = 1;
        [Min(0), Tooltip("Hand repair of central control, alloy: this + controlRepairPerDay x day.")] public int controlRepair = 5;
        [Min(0)] public int controlRepairPerDay = 1;
        [Min(0f), Tooltip("Alloy for repairing the whole hull at durability level 1; each durability level adds hullRepairPerLevel.")]
        public float hullRepair = 6f;
        [Min(0f)] public float hullRepairPerLevel = 3f;

        [Header("Day 0 tutorial")]
        [Min(0)] public int tutorialOreAlloy = 8;
        [Min(0)] public int tutorialGrunts = 6;
        [Min(0), Tooltip("Reserve the base hands over on arrival (with the tutorial's ore and star matter it pays for the first two rooms).")]
        public int arrivalAlloy = 2;
        [Min(0)] public int arrivalEnergy = 4;
        [Min(0)] public int tutorialNightOre = 2;
        [Min(0)] public int tutorialNightOreAlloy = 2;

        public float NightLength(int day) => day <= 0 ? tutorialNightSeconds : nightSeconds + nightSecondsPerDay * (day - 1);
        public int OreYield(int day) => oreYield + oreYieldStep * ((Mathf.Max(1, day) - 1) / Mathf.Max(1, oreYieldStepDays));
        public int OreNodes(int day) => oreNodes + Mathf.Max(0, day - 1);
        public float BaseMaxHp(int durability) => baseHp * Mathf.Pow(baseHpGrowth, Mathf.Max(0, durability - 1));
        public int MaxDurability => durabilityLevels.Length + 1;
        public int MaxShield => shieldLevels.Length;
        public int FuelNeed => fuelUnits * darkPerFuelUnit;
        public int RoomRepairCost(int level, int day) => roomRepair + roomRepairPerLevel * level + roomRepairPerDay * Mathf.Max(0, day);
        public int ControlRepairCost(int day) => controlRepair + controlRepairPerDay * Mathf.Max(0, day);
        /// <summary>The table's wave number (from 1) of a day's k-th wave (k from 0): day d, wave k is ((d - 1) x wavesPerDay + k + 1) x waveStride.</summary>
        public int WaveNumber(int day, int waveOfDay) => ((Mathf.Max(1, day) - 1) * wavesPerDay + waveOfDay + 1) * Mathf.Max(1, waveStride);
        public int ShieldTopUpCost(int day) => shieldTopUpCost + shieldTopUpCostPerDay * Mathf.Max(0, day);
        /// <summary>A room purchase's price after `bought` earlier purchases of the same kind: the base price plus one step per purchase.</summary>
        public static CampaignCost Rising(CampaignCost price, CampaignCost step, int bought)
        {
            int n = Mathf.Max(0, bought);
            return new CampaignCost(price.alloy + step.alloy * n, price.energy + step.energy * n, price.dark + step.dark * n);
        }
        /// <summary>The price of the level after `level` (buying level 2 when the player is level 1).</summary>
        public CampaignCost PlayerLevelCost(int level)
        {
            int steps = Mathf.Max(0, level - 1);
            return new CampaignCost(playerLevelCost.alloy + playerLevelCostPerLevel.alloy * steps, playerLevelCost.energy + playerLevelCostPerLevel.energy * steps,
                playerLevelCost.dark + playerLevelCostPerLevel.dark * steps);
        }
        public int HullRepairCost(float missingShare, int durability) => Mathf.CeilToInt(Mathf.Clamp01(missingShare) * (hullRepair + hullRepairPerLevel * (durability - 1)));
    }
}
