using System;
using UnityEngine;

namespace AnomalyArena
{
                                                                                                                                  
    [Serializable]
    public struct TierValues
    {
        public float fodder, special, elite;
        public TierValues(float fodder, float special, float elite) { this.fodder = fodder; this.special = special; this.elite = elite; }
        public float For(CampaignDropTier tier) => tier switch { CampaignDropTier.Elite => elite, CampaignDropTier.Special => special, _ => fodder };
    }

    public enum CampaignDropTier { Fodder, Special, Elite }

                                                              
    [Serializable]
    public struct CampaignCost
    {
        [Min(0)] public int alloy, energy, dark;
        public CampaignCost(int alloy, int energy = 0, int dark = 0) { this.alloy = alloy; this.energy = energy; this.dark = dark; }
        public bool IsFree => alloy <= 0 && energy <= 0 && dark <= 0;
    }

                 
                                                                                                                                            
                                                                                                                                              
                                                                                                                                             
                                                                                      
                  
    [CreateAssetMenu(menuName = "Anomaly Arena/Campaign/Rules")]
    public sealed class CampaignRules : ScriptableObject
    {
        [Header("Days")]
        [Min(1), Tooltip("Days before the ignition is forced: 7 since 2026-11-06, one wave a day. Day d fights wave d x waveStride of the table, so day 7 is wave 14 and the 15th (the Tyrant) comes after the failed ignition.")]
        public int days = 7;
        [Min(1), Tooltip("Waves of the wave table fought each day, back to back with a short lull between them (1 since 2026-11-06).")]
        public int wavesPerDay = 1;
        [Min(1), Tooltip("Waves of the table one day steps over: 2 since 2026-11-06, so the 7 days fight waves 2, 4 ... 14 and the difficulty meets the Tyrant.")]
        public int waveStride = 2;
        [Min(0f), Tooltip("Seconds between the waves of one day.")] public float lullSeconds = 4f;
        [Min(0f), Tooltip("Dusk after the last wave: rest and mining time, the light fades, ore nodes show, no shadows yet.")]
        public float duskSeconds = 15f;
        [Min(5f), Tooltip("Seconds of night on day 1; the night is longer by nightSecondsPerDay each day.")] public float nightSeconds = 35f;
        [Min(0f)] public float nightSecondsPerDay = 1f;
        [Min(5f), Tooltip("The Day 0 tutorial night.")] public float tutorialNightSeconds = 55f;
        [Min(0), Tooltip("Free air drops (armory rounds) per dawn: 0 since 2026-11-06. Every upgrade is bought in its room as the V2 doc places it and takes effect at once.")]
        public int armoryRoundsPerDawn = 0;

        [Header("Kill drops: alien energy (star matter) and alloy shards")]
        [Tooltip("Chance a kill drops alien energy, per enemy tier.")] public TierValues energyChance = new(.05f, .30f, 1f);
        [Tooltip("Alien energy in one drop, per enemy tier; grows by energyGrowthPerDay a day.")] public TierValues energyAmount = new(1f, 2f, 2f);
        [Min(0f)] public float energyGrowthPerDay = .06f;
        [Tooltip("Chance a kill drops an alloy shard, per enemy tier (most alloy is mined).")] public TierValues alloyChance = new(.04f, .15f, 1f);
        [Tooltip("Alloy in one shard, per enemy tier; grows by alloyGrowthPerDay a day.")] public TierValues alloyAmount = new(1f, 1f, 2f);
        [Min(0f)] public float alloyGrowthPerDay = .10f;
        [Min(.5f), Tooltip("Cells within which a dropped shard flies to the player.")] public float pickupMagnet = 3.5f;

        [Header("Mining (dusk and night)")]
        [Min(0), Tooltip("Alloy ore nodes that surface at dusk on day 1; one more each later day (16 since 2026-11-06, up from 9).")] public int oreNodes = 16;
        [Min(1), Tooltip("Alloy from one ore node: oreYield on days 1-2, + oreYieldStep every oreYieldStepDays days.")] public int oreYield = 3;
        [Min(0)] public int oreYieldStep = 2;
        [Min(1)] public int oreYieldStepDays = 2;
        [Min(0), Tooltip("Dark matter clusters that surface each night, at most one per region (14 since 2026-11-06, up from 9).")] public int darkClusters = 14;
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
        [Min(1f), Tooltip("Radius of a placed mining lamp, in cells.")] public float lampRadius = 6.5f;
        [Min(1f)] public float baseLightRadius = 11f;
        [Min(0f), Tooltip("The player's own faint glow: shows the feet, does not scare shadows, does not allow mining.")] public float playerGlowRadius = 2.4f;
        [Min(0), Tooltip("Mining lamps the base hands out each night.")] public int freeLampsPerNight = 2;
        [Min(1f), Tooltip("Seconds a lamp holds out against one shadow in a region overrun by shadows.")] public float lampHp = 4f;

        [Header("Night: shadow monsters")]
        [Tooltip("Columns and rows of regions the map is cut into for the kills-to-shadows rule.")] public Vector2Int regions = new(6, 4);
        [Min(1f), Tooltip("Day kills in a region per shadow that region gets at night.")] public float killsPerShadow = 4f;
        [Min(1), Tooltip("A region with this many shadows is overrun: its shadows break lamps there and chase into the light.")] public int overrunShadows = 3;
        [Min(1)] public int maxShadowsPerRegion = 6;
        [Min(1)] public int maxShadows = 40;
        [Min(0)] public int tutorialShadows = 4;
        [Min(0f)] public float shadowSpeed = 3.4f;
        [Min(0f)] public float overrunShadowSpeed = 4.4f;
        [Min(0f), Tooltip("Cells within which a shadow notices a player standing in the dark.")] public float shadowSense = 9f;
        [Range(0f, 1f), Tooltip("Share of the player's max HP one shadow touch takes.")] public float shadowHitShare = .15f;
        [Min(.1f)] public float shadowHitInterval = .9f;
        [Range(0f, 1f), Tooltip("Share of tonight's haul (everything mined or picked up since dusk, or since the last base visit that night) lost when the player dies at night; 1 = all of it, the rule since 2026-11-06. The base's stock, the rooms and the ship stay, and the next day starts as usual.")]
        public float nightDeathLoss = 1f;
        [Min(.5f), Tooltip("Seconds the fallen player lies in the dark (the death animation plays) before waking at the base.")]
        public float nightDeathSeconds = 2.8f;

        [Header("Base")]
        [Min(1f), Tooltip("Base max HP at durability level 1; each level multiplies it by baseHpGrowth (explosive growth, like the enemy pressure).")]
        public float baseHp = 300f;
        [Min(1f)] public float baseHpGrowth = 1.5f;
        [Range(0f, 1f), Tooltip("Shield cap per shield level, as a share of the base max HP.")] public float shieldShare = .15f;
        [Range(0f, 1f), Tooltip("HP the base repairs by itself each night, as a share of its max HP.")] public float nightSelfRepair = .3f;
        [Range(.05f, 1f), Tooltip("A side breaks its room once it takes this share of the base max HP in one day.")] public float sideBreakShare = .25f;
        [Min(0f), Tooltip("Every monster of a day wave raids the base (since 2026-11-06; bosses and monsters that cannot walk excepted). A raider within this many cells of the player fights the player instead; 0 = never.")]
        public float raiderAggroRadius = 7f;
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
        [Min(1)] public int darkPerFuelUnit = 6;
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
                                                                                                                                                               
        public int WaveNumber(int day, int waveOfDay) => ((Mathf.Max(1, day) - 1) * wavesPerDay + waveOfDay + 1) * Mathf.Max(1, waveStride);
        public int ShieldTopUpCost(int day) => shieldTopUpCost + shieldTopUpCostPerDay * Mathf.Max(0, day);
                                                                                                                                                    
        public static CampaignCost Rising(CampaignCost price, CampaignCost step, int bought)
        {
            int n = Mathf.Max(0, bought);
            return new CampaignCost(price.alloy + step.alloy * n, price.energy + step.energy * n, price.dark + step.dark * n);
        }
                                                                                                                
        public CampaignCost PlayerLevelCost(int level)
        {
            int steps = Mathf.Max(0, level - 1);
            return new CampaignCost(playerLevelCost.alloy + playerLevelCostPerLevel.alloy * steps, playerLevelCost.energy + playerLevelCostPerLevel.energy * steps,
                playerLevelCost.dark + playerLevelCostPerLevel.dark * steps);
        }
        public int HullRepairCost(float missingShare, int durability) => Mathf.CeilToInt(Mathf.Clamp01(missingShare) * (hullRepair + hullRepairPerLevel * (durability - 1)));
    }
}
