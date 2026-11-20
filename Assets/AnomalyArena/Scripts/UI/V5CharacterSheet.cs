using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Which part of the character screen a line is in.</summary>
    public enum CharacterSection { Vitals, Weapon, Skill, Upgrade, Bonus }

    /// <summary>One line of the character screen in words, as a script reads it (V5CharacterScreen.Rows): its part, a stable key, its
    /// name, its second line and its number or level. The screen draws the same things from the sheet's own lists.</summary>
    public sealed class CharacterRow
    {
        public CharacterSection section;
        public string id, title, detail, value;

        public override string ToString() => $"{section} {id}: {title} | {detail} | {value}";
    }

    /// <summary>A mark on a weapon's card: an upgrade it has (its direction's icon and name), or the void's enchant on it.</summary>
    public sealed class CharacterTag
    {
        public string text;
        public Color colour;
        public Sprite art, flat;
    }

    /// <summary>A weapon the player holds, as the character screen shows it.</summary>
    public sealed class CharacterWeapon
    {
        public RunWeapon weapon;
        public bool auto, inUse;
        /// <summary>Its slot among the hand or the auto slots, from 0.</summary>
        public int slot;
        public string name, place, anomaly;
        /// <summary>Its anomaly has not shown yet (the anomaly line says so in a dimmer colour).</summary>
        public bool anomalyHidden;
        public int mk;
        /// <summary>The story's void Mk, bought with void alloy: its mark is black.</summary>
        public bool voidMk;
        /// <summary>Its element now (a void enchant's borrowed Void included); None for none.</summary>
        public ElementKind element;
        public CardRarity rarity;
        /// <summary>The weapon's picture and its element's icon, the art's and the whitebox's.</summary>
        public Sprite art, flat, elementArt, elementFlat;
        public readonly List<CharacterTag> tags = new();

        public string Id => (auto ? "weapon.auto" : "weapon.hand") + slot;
    }

    /// <summary>A skill the player has learned, as the character screen shows it.</summary>
    public sealed class CharacterSkill
    {
        public SkillId id;
        public int slot, level;
        /// <summary>A greater void skill: it has no levels.</summary>
        public bool greaterVoid;
        public string name, key;
        public float cost, cooldown;
        public CardRarity rarity;
        public Sprite art, flat;

        public string Id => "skill." + id;
    }

    /// <summary>One upgrade in the right column: a study purchase in the story, an upgrade card taken in the open mode (count: how many of
    /// the same card).</summary>
    public sealed class CharacterEntry
    {
        public string id, title, detail, value;
        public CardRarity rarity;
        public Sprite art, flat;
        public int count = 1;
    }

    /// <summary>What a bonus line is about: one of the player's numbers that has moved off its start.</summary>
    public enum CharacterBonusKind
    {
        MaxHp, MaxEnergy, Damage, AttackSpeed, CritChance, CritDamage, DamageTaken, MoveSpeed, Sprint, Regeneration, PickupRange,
        EnergyPerKill, Penetration, Knockback, WallSlam, WeaponDamage, SkillDamage, SkillCooldown, SkillCost, BossPenetration, StatusFloor,
        EnergyRegeneration
    }

    /// <summary>
    /// One bonus line of the character screen: what it is about and its amount in the unit the line shows: points (max HP, max energy,
    /// energy a kill, wall slam damage, the burn floor, the energy a second), percent (damage, attack speed, the crit chance, damage taken,
    /// move speed, penetration, knockback, the skills' and the weapons' factors), seconds (sprint), cells (pickup range), points every
    /// interval seconds (regeneration) or the factor itself (crit damage). Its words are plain and the same in both languages.
    /// </summary>
    public readonly struct CharacterBonus
    {
        public readonly CharacterBonusKind Kind;
        public readonly float Amount, Interval;

        public CharacterBonus(CharacterBonusKind kind, float amount, float interval = 0f)
        {
            Kind = kind;
            Amount = amount;
            Interval = interval;
        }

        public string Id => "bonus." + Kind;

        /// <summary>The amount shows as something other than zero at the precision it is written with.</summary>
        public bool Shown => Kind == CharacterBonusKind.CritDamage || Math.Abs(Amount) >= .05f;

        public string Name(bool chinese) => Kind switch
        {
            CharacterBonusKind.MaxHp => chinese ? "最大生命" : "Max HP",
            CharacterBonusKind.MaxEnergy => chinese ? "最大能量" : "Max energy",
            CharacterBonusKind.Damage => chinese ? "伤害" : "Damage",
            CharacterBonusKind.AttackSpeed => chinese ? "攻击速度" : "Attack speed",
            CharacterBonusKind.CritChance => chinese ? "暴击率" : "Crit chance",
            CharacterBonusKind.CritDamage => chinese ? "暴击伤害" : "Crit damage",
            CharacterBonusKind.DamageTaken => chinese ? "受到的伤害" : "Damage taken",
            CharacterBonusKind.MoveSpeed => chinese ? "移动速度" : "Move speed",
            CharacterBonusKind.Sprint => chinese ? "冲刺时长" : "Sprint duration",
            CharacterBonusKind.Regeneration => chinese ? "生命回复" : "Regeneration",
            CharacterBonusKind.PickupRange => chinese ? "拾取范围" : "Pickup range",
            CharacterBonusKind.EnergyPerKill => chinese ? "击杀回复能量" : "Energy per kill",
            CharacterBonusKind.Penetration => chinese ? "伤害穿透" : "Damage penetration",
            CharacterBonusKind.Knockback => chinese ? "击退" : "Knockback",
            CharacterBonusKind.WallSlam => chinese ? "撞墙伤害" : "Wall slam damage",
            CharacterBonusKind.WeaponDamage => chinese ? "武器伤害" : "Weapon damage",
            CharacterBonusKind.SkillDamage => chinese ? "技能伤害" : "Skill damage",
            CharacterBonusKind.SkillCooldown => chinese ? "技能冷却" : "Skill cooldowns",
            CharacterBonusKind.SkillCost => chinese ? "技能能量消耗" : "Skill energy cost",
            CharacterBonusKind.BossPenetration => chinese ? "对 Boss 的穿透" : "Penetration against bosses",
            CharacterBonusKind.StatusFloor => chinese ? "燃烧和中毒的保底伤害" : "Burn and poison floor",
            CharacterBonusKind.EnergyRegeneration => chinese ? "能量回复" : "Energy regeneration",
            _ => Kind.ToString(),
        };

        public string Value(bool chinese) => Kind switch
        {
            CharacterBonusKind.MaxHp or CharacterBonusKind.MaxEnergy or CharacterBonusKind.EnergyPerKill or CharacterBonusKind.WallSlam => Signed(Amount),
            CharacterBonusKind.CritDamage => "×" + Amount.ToString("0.##", CultureInfo.InvariantCulture),
            CharacterBonusKind.Sprint => Signed(Amount) + (chinese ? " 秒" : " s"),
            CharacterBonusKind.PickupRange => Signed(Amount) + (chinese ? " 格" : " cells"),
            CharacterBonusKind.Regeneration => chinese ? $"每 {Number(Interval)} 秒 {Signed(Amount)}" : $"{Signed(Amount)} every {Number(Interval)} s",
            CharacterBonusKind.StatusFloor => chinese ? $"每秒至少 {Number(Amount)}" : $"at least {Number(Amount)} a second",
            CharacterBonusKind.EnergyRegeneration => chinese ? $"每秒 {Signed(Amount)}" : $"{Signed(Amount)} a second",
            _ => SignedPercent(Amount),
        };

        /// <summary>One decimal at most ("15", "1.5").</summary>
        public static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>With its sign: "+15", "-0.5".</summary>
        public static string Signed(float value) => (value < 0f ? "-" : "+") + Number(Math.Abs(value));

        /// <summary>A percent with its sign, whole from ten on and to one decimal below it: "+26%", "-20%", "+4.5%".</summary>
        public static string SignedPercent(float value)
        {
            float size = Math.Abs(value);
            return (value < 0f ? "-" : "+") + size.ToString(size >= 10f ? "0" : "0.#", CultureInfo.InvariantCulture) + "%";
        }

        public override string ToString() => $"{Kind} {Amount}";
    }

    /// <summary>What moves the player's numbers besides RunStats: each as the game's own hook gives it (a factor of 1, or 0, where there is
    /// none), and which numbers the screen may show at all (the energy's once it is unlocked, the skills' once one is learned).</summary>
    public struct CharacterBonusExtras
    {
        /// <summary>Factors on skill damage, skill cooldowns, the skills' energy cost and weapon damage (1: none).</summary>
        public float skillDamage, skillCooldown, skillCost, weaponDamage;
        /// <summary>The share let through a boss's immunity, the least a burn or poison of the player's deals a second, the energy
        /// restored a second (0: none).</summary>
        public float bossPenetration, statusFloor, energyRegeneration;
        /// <summary>Seconds between two heals of the regeneration.</summary>
        public float regenerationInterval;
        public bool energyShown, skillsShown;

        public static CharacterBonusExtras None => new()
        {
            skillDamage = 1f, skillCooldown = 1f, skillCost = 1f, weaponDamage = 1f, regenerationInterval = 5f,
        };
    }

    /// <summary>The character screen's bonus lines, worked out from the player's stats at the run's start and now and the run's other
    /// effects; only what differs from the start makes a line, in a fixed order. Each number is read the way the game reads it: damage and
    /// damage taken through RunStats' own multipliers (damage cards multiply, armor divides), move speed against the starting speed, and
    /// the rest as the difference of the stat.</summary>
    public static class CharacterBonuses
    {
        public static List<CharacterBonus> From(RunStats start, RunStats now, CharacterBonusExtras extras)
        {
            var lines = new List<CharacterBonus>();
            if (start == null || now == null) return lines;
            Add(lines, CharacterBonusKind.MaxHp, now.maxHp - start.maxHp);
            if (extras.energyShown) Add(lines, CharacterBonusKind.MaxEnergy, now.maxEnergy - start.maxEnergy);
            Add(lines, CharacterBonusKind.Damage, Percent(now.DamageMultiplier, start.DamageMultiplier));
            Add(lines, CharacterBonusKind.AttackSpeed, (now.attackSpeedBonus - start.attackSpeedBonus) * 100f);
            Add(lines, CharacterBonusKind.CritChance, (now.critChance - start.critChance) * 100f);
            if (Math.Abs(now.CritMultiplier - start.CritMultiplier) >= .005f) lines.Add(new CharacterBonus(CharacterBonusKind.CritDamage, now.CritMultiplier));
            Add(lines, CharacterBonusKind.DamageTaken, Percent(now.DamageTakenMultiplier, start.DamageTakenMultiplier));
            Add(lines, CharacterBonusKind.MoveSpeed, Percent(now.moveSpeed, start.moveSpeed));
            Add(lines, CharacterBonusKind.Sprint, now.sprintDuration - start.sprintDuration);
            Add(lines, CharacterBonusKind.Regeneration, now.regeneration - start.regeneration, extras.regenerationInterval);
            Add(lines, CharacterBonusKind.PickupRange, now.pickupRange - start.pickupRange);
            if (extras.energyShown) Add(lines, CharacterBonusKind.EnergyPerKill, now.energySiphon - start.energySiphon);
            Add(lines, CharacterBonusKind.Penetration, (now.elementPenetration - start.elementPenetration) * 100f);
            Add(lines, CharacterBonusKind.Knockback, (now.knockbackBonus - start.knockbackBonus) * 100f);
            Add(lines, CharacterBonusKind.WallSlam, now.impact - start.impact);
            Add(lines, CharacterBonusKind.WeaponDamage, (extras.weaponDamage - 1f) * 100f);
            if (extras.skillsShown)
            {
                Add(lines, CharacterBonusKind.SkillDamage, (extras.skillDamage - 1f) * 100f);
                Add(lines, CharacterBonusKind.SkillCooldown, (extras.skillCooldown - 1f) * 100f);
                Add(lines, CharacterBonusKind.SkillCost, (extras.skillCost - 1f) * 100f);
            }
            Add(lines, CharacterBonusKind.BossPenetration, extras.bossPenetration * 100f);
            Add(lines, CharacterBonusKind.StatusFloor, extras.statusFloor);
            if (extras.energyShown) Add(lines, CharacterBonusKind.EnergyRegeneration, extras.energyRegeneration);
            return lines;
        }

        static float Percent(float now, float start) => start > 0f ? (now / start - 1f) * 100f : 0f;

        static void Add(List<CharacterBonus> lines, CharacterBonusKind kind, float amount, float interval = 0f)
        {
            var line = new CharacterBonus(kind, amount, interval);
            if (line.Shown) lines.Add(line);
        }
    }

    /// <summary>
    /// Everything the character screen shows, read from the run as it is at one moment (Read), in both modes: the health and the energy
    /// the skills spend, the weapons held (hand and auto) with their Mk, element and what was added to them, the skills learned with their
    /// level, the upgrades (the study's purchases in the story, the upgrade cards taken in the open mode) and the bonus lines. Only what the
    /// player has makes a line: no empty slot, nothing locked, the energy only once it is unlocked. Rows gives the same in words for scripts.
    /// </summary>
    public sealed class CharacterSheet
    {
        public bool story;
        /// <summary>Where the run stands, for the screen's banner: the day and the time of day, or the wave and the level.</summary>
        public string heading = "";
        public float hp, maxHp, shield, energy, maxEnergy;
        public bool energyShown;
        public readonly List<CharacterWeapon> weapons = new();
        public readonly List<CharacterSkill> skills = new();
        public readonly List<CharacterEntry> upgrades = new();
        public readonly List<CharacterBonus> bonuses = new();

        /// <summary>The run as it is now (an empty sheet without a run).</summary>
        public static CharacterSheet Read(GameManager gm)
        {
            var sheet = new CharacterSheet();
            if (!gm) return sheet;
            var progression = gm.progression;
            var campaign = gm.CampaignRunning ? V2Campaign.Active : null;
            var set = gm.hud && gm.hud.v2Hud ? gm.hud.v2Hud.art : null;
            sheet.story = campaign;
            ReadVitals(sheet, gm.player, progression);
            sheet.heading = campaign ? campaign.CharacterHeading : OpenHeading(gm, progression);
            if (gm.runWeapons) ReadWeapons(sheet, gm.runWeapons, campaign, set);
            if (gm.skills) ReadSkills(sheet, gm.skills, set);
            if (campaign) campaign.CharacterUpgrades(sheet.upgrades);
            else if (progression) ReadCards(sheet, progression, set);
            if (progression && progression.Rules)
                sheet.bonuses.AddRange(CharacterBonuses.From(progression.StartingStats(), progression.Stats, Extras(gm, progression, campaign)));
            return sheet;
        }

        static void ReadVitals(CharacterSheet sheet, PlayerController player, RunProgression progression)
        {
            sheet.hp = player ? Mathf.Max(0f, player.Hp) : 0f;
            sheet.maxHp = player ? player.maxHp : 0f;
            var status = player ? ElementStatus.For(player) : null;
            sheet.shield = status ? Mathf.Max(0f, status.Shield) : 0f;
            sheet.energyShown = progression && progression.EnergyUnlocked;
            sheet.energy = progression ? Mathf.Max(0f, progression.Energy) : 0f;
            sheet.maxEnergy = progression ? progression.Stats.maxEnergy : 0f;
        }

        /// <summary>The open mode's banner line: the wave (or the fight it is in) and the player's level.</summary>
        static string OpenHeading(GameManager gm, RunProgression progression)
        {
            int level = progression ? progression.Level : 1, wave = gm.waves ? Mathf.Max(1, gm.waves.WaveIndex + 1) : 1;
            string where = gm.RunPhase switch
            {
                ArenaUiState.FinalBoss => ArenaText.Pair("暴虐君王之战", "THE TYRANT KING"),
                ArenaUiState.VoidTrial => ArenaText.Pair("虚空试炼", "THE VOID TRIAL"),
                _ => ArenaText.Pair($"第 {wave} 波", $"WAVE {wave}"),
            };
            return where + ArenaText.Pair($" · 等级 {level}", $" · LEVEL {level}");
        }

        static void ReadWeapons(CharacterSheet sheet, RunWeapons weapons, V2Campaign campaign, V2ArtSet set)
        {
            for (int i = 0; i < weapons.Hand.Length; i++)
                if (weapons.Hand[i] != null) sheet.weapons.Add(Weapon(weapons.Hand[i], false, i, i == weapons.ActiveIndex, weapons, campaign, set));
            for (int i = 0; i < weapons.Auto.Length; i++)
                if (weapons.Auto[i] != null) sheet.weapons.Add(Weapon(weapons.Auto[i], true, i, false, weapons, campaign, set));
        }

        // The directions in the order a weapon's card lists them: the three the cards and the study still give, then the older three.
        static readonly UpgradeAxis[] AxisOrder = { UpgradeAxis.Tame, UpgradeAxis.Twist, UpgradeAxis.Infusion, UpgradeAxis.Power, UpgradeAxis.Tempo, UpgradeAxis.Reach };

        static CharacterWeapon Weapon(RunWeapon w, bool auto, int slot, bool inUse, RunWeapons weapons, V2Campaign campaign, V2ArtSet set)
        {
            var view = new CharacterWeapon
            {
                weapon = w, auto = auto, slot = slot, inUse = inUse, name = w.FormName, mk = Mathf.Max(1, w.mk), element = w.element,
                rarity = ArenaRarity.ForWeapon(w), art = At(set ? set.weaponSprites : null, (int)w.form), flat = WhiteboxIcons.Weapon((int)w.form),
                place = auto ? ArenaText.Pair($"自动 {slot + 1}", $"Auto {slot + 1}") : ArenaText.Pair($"手持 {slot + 1}", $"Hand {slot + 1}"),
                anomaly = "",
            };
            if (w.IsAnomalous)
            {
                view.anomalyHidden = !w.revealed;
                view.anomaly = w.revealed ? ArenaTerms.WeaponAnomaly(w.form, w.variant) : ArenaText.Pair("异常还没揭晓", "Anomaly not revealed yet");
            }
            view.voidMk = campaign && campaign.CharacterVoidMk(w);
            if (w.element != ElementKind.None)
            {
                // The art set's element order is ElementKind's, less None.
                int element = (int)w.element - 1;
                view.elementArt = At(set ? set.elementIcons : null, element);
                view.elementFlat = WhiteboxIcons.Element(element);
            }
            foreach (var axis in AxisOrder)
            {
                // A built-in element is not an infusion the weapon was given.
                if (!w.Has(axis) || axis == UpgradeAxis.Infusion && w.builtInElement) continue;
                int index = AxisIndex(axis);
                view.tags.Add(new CharacterTag
                {
                    text = AxisWords(w, axis), colour = ArenaRarity.InkFor(ArenaRarity.ForWeaponCard(axis)),
                    art = At(set ? set.axisIcons : null, index), flat = WhiteboxIcons.Axis(index),
                });
            }
            int voidIndex = (int)ElementKind.Void - 1;
            string enchant = null;
            if (campaign && campaign.IsVoidEnchanted(w)) enchant = ArenaText.Pair("虚空附魔", "Void enchant");
            // The open mode's Void Walk lends the hand weapons the Void for a while; the screen holds the run, so the time left stands still.
            else if (!campaign && weapons.VoidEnchantLeft > 0f && w.element == ElementKind.Void && weapons.ElementOf(w) != ElementKind.Void)
                enchant = ArenaText.Pair($"虚空附魔 · 还剩 {weapons.VoidEnchantLeft:0.#} 秒", $"Void enchant · {weapons.VoidEnchantLeft:0.#} s left");
            if (enchant != null)
                view.tags.Add(new CharacterTag { text = enchant, colour = V5HudKit.VioletHi, art = At(set ? set.elementIcons : null, voidIndex), flat = WhiteboxIcons.Element(voidIndex) });
            return view;
        }

        /// <summary>A direction's place in the art set's axisIcons (Infusion, Power, Tempo, Reach, Twist, Tame): its flag's bit.</summary>
        public static int AxisIndex(UpgradeAxis axis)
        {
            int bits = (int)axis;
            for (int i = 0; i < 6; i++) if (bits == 1 << i) return i;
            return -1;
        }

        /// <summary>A direction's name on a weapon; in the story Tame and Twist also give attack speed and damage (RunWeapons' rules for
        /// the weapon), which the words add.</summary>
        static string AxisWords(RunWeapon w, UpgradeAxis axis)
        {
            string name = ArenaTerms.Upgrade(axis);
            float factor = axis == UpgradeAxis.Tame ? RunWeapons.WeaponSpeedRule?.Invoke(w) ?? 1f : axis == UpgradeAxis.Twist ? RunWeapons.WeaponDamageRule?.Invoke(w) ?? 1f : 1f;
            if (factor <= 1.001f) return name;
            return axis == UpgradeAxis.Tame ? ArenaText.Pair($"{name} · 攻速 ×{factor:0.##}", $"{name} · attack speed ×{factor:0.##}")
                : ArenaText.Pair($"{name} · 伤害 ×{factor:0.##}", $"{name} · damage ×{factor:0.##}");
        }

        static void ReadSkills(CharacterSheet sheet, SkillLoadout skills, V2ArtSet set)
        {
            for (int i = 0; i < skills.Slots.Length; i++)
            {
                var slot = skills.Slots[i];
                if (slot.Empty) continue;
                var definition = slot.Definition;
                sheet.skills.Add(new CharacterSkill
                {
                    id = slot.Id, slot = i, level = slot.Level, greaterVoid = definition.greaterVoid, name = slot.Name, cost = slot.Cost,
                    cooldown = slot.Cooldown, rarity = ArenaRarity.ForSkill(definition, slot.Level),
                    key = i <= 2 ? ArenaKeys.Name(GameAction.Skill1 + i) : "",
                    art = At(set ? set.skillIcons : null, (int)slot.Id), flat = WhiteboxIcons.Skill((int)slot.Id),
                });
            }
        }

        /// <summary>The open mode's upgrade cards taken (RunProgression.TakenCards), the same card once with how many were taken, in the
        /// order each was first taken.</summary>
        static void ReadCards(CharacterSheet sheet, RunProgression progression, V2ArtSet set)
        {
            var seen = new List<TakenCard>();
            foreach (var card in progression.TakenCards)
            {
                if (!Names(card)) continue;
                int at = seen.FindIndex(other => other.Same(card));
                if (at >= 0)
                {
                    var entry = sheet.upgrades[at];
                    entry.count++;
                    entry.value = "×" + entry.count;
                    continue;
                }
                seen.Add(card);
                sheet.upgrades.Add(CardEntry(card, set));
            }
        }

        /// <summary>The record names something the game has (a save written by another build may not).</summary>
        static bool Names(TakenCard card) => card.kind switch
        {
            DraftCardKind.Stat => Enum.IsDefined(typeof(StatId), card.id),
            DraftCardKind.Weapon => Enum.IsDefined(typeof(WeaponForm), card.id) && AxisIndex(card.axis) >= 0,
            _ => Enum.IsDefined(typeof(SkillId), card.id),
        };

        static CharacterEntry CardEntry(TakenCard card, V2ArtSet set)
        {
            var entry = new CharacterEntry
            {
                id = $"card.{card.kind}.{card.id}.{card.axis}.{card.element}.{card.rarity}", rarity = card.rarity, value = "",
                detail = ArenaTerms.CardKind(card.kind) + " · " + ArenaTerms.Rarity(card.rarity),
            };
            switch (card.kind)
            {
                case DraftCardKind.Stat:
                    entry.title = new StatCardOffer((StatId)card.id, card.rarity, 0f).Title;
                    entry.art = At(set ? set.statIcons : null, card.id);
                    entry.flat = WhiteboxIcons.Stat(card.id);
                    break;
                case DraftCardKind.Weapon:
                    entry.title = ArenaTerms.WeaponForm((WeaponForm)card.id) + " · " + ArenaTerms.Upgrade(card.axis)
                        + (card.axis == UpgradeAxis.Infusion && card.element != ElementKind.None ? " · " + ArenaTerms.Element(card.element) : "");
                    int axis = AxisIndex(card.axis);
                    entry.art = At(set ? set.axisIcons : null, axis);
                    entry.flat = WhiteboxIcons.Axis(axis);
                    break;
                default:
                    var skill = (SkillId)card.id;
                    // A skill card's colour is the level it gave; the greater void skills have no levels.
                    entry.title = ArenaTerms.Skill(skill) + (WhiteboxIcons.SkillIsGreaterVoid(skill) ? ""
                        : ArenaText.Pair($" · 等级 {ArenaRarity.SkillLevel(card.rarity)}", $" · Level {ArenaRarity.SkillLevel(card.rarity)}"));
                    entry.art = At(set ? set.skillIcons : null, card.id);
                    entry.flat = WhiteboxIcons.Skill(card.id);
                    break;
            }
            return entry;
        }

        /// <summary>The run's effects on the player besides RunStats, each from the hook the game itself reads (null hooks: none).</summary>
        static CharacterBonusExtras Extras(GameManager gm, RunProgression progression, V2Campaign campaign)
        {
            var rules = progression.Rules;
            var extras = CharacterBonusExtras.None;
            extras.energyShown = progression.EnergyUnlocked;
            extras.skillsShown = gm.skills && gm.skills.OwnedCount > 0;
            extras.regenerationInterval = rules ? rules.regenerationInterval : 5f;
            extras.skillDamage = Mathf.Max(0f, SkillLoadout.DamageScale?.Invoke() ?? 1f);
            extras.skillCooldown = Mathf.Max(0f, SkillLoadout.CooldownScale?.Invoke() ?? 1f);
            extras.skillCost = Mathf.Max(0f, SkillLoadout.CostScale?.Invoke() ?? 1f);
            extras.weaponDamage = Mathf.Max(0f, RunWeapons.DamageBonus?.Invoke() ?? 1f);
            extras.statusFloor = gm.player ? Mathf.Max(0f, ElementStatus.StatusDamageFloor?.Invoke(gm.player) ?? 0f) : 0f;
            extras.bossPenetration = campaign ? campaign.CharacterBossPenetration : 0f;
            // RunProgression.Tick: the void energy's regeneration, at the campaign's rate while one runs.
            if (progression.VoidEnergy && progression.EnergyUnlocked && rules)
                extras.energyRegeneration = Mathf.Max(0f, RunProgression.VoidRegenRule?.Invoke(rules.voidEnergyRegeneration) ?? rules.voidEnergyRegeneration);
            return extras;
        }

        static Sprite At(Sprite[] sprites, int index) => sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;

        // ───── In words ─────

        /// <summary>Every line in words, in the screen's order: the vitals, the weapons, the skills, the upgrades, the bonuses.</summary>
        public List<CharacterRow> Rows()
        {
            var rows = new List<CharacterRow>();
            bool chinese = ArenaText.IsChinese;
            rows.Add(Row(CharacterSection.Vitals, "vitals.hp", ArenaText.Pair("生命", "HP"), "", $"{Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(maxHp)}"));
            if (shield > 0f) rows.Add(Row(CharacterSection.Vitals, "vitals.shield", ArenaText.Pair("护盾", "Shield"), "", $"{Mathf.CeilToInt(shield)}"));
            if (energyShown) rows.Add(Row(CharacterSection.Vitals, "vitals.energy", ArenaText.Pair("能量", "Energy"), "", $"{Mathf.CeilToInt(energy)} / {Mathf.CeilToInt(maxEnergy)}"));
            foreach (var w in weapons)
            {
                var parts = new List<string> { w.place };
                if (w.inUse) parts.Add(ArenaText.Pair("使用中", "In use"));
                if (w.anomaly.Length > 0) parts.Add(w.anomaly);
                if (w.element != ElementKind.None) parts.Add(ArenaTerms.Element(w.element));
                foreach (var tag in w.tags) parts.Add(tag.text);
                rows.Add(Row(CharacterSection.Weapon, w.Id, w.name, string.Join(" · ", parts), "MK " + V5BattleKit.Roman(w.mk)));
            }
            foreach (var s in skills) rows.Add(Row(CharacterSection.Skill, s.Id, s.name, SkillDetail(s), SkillLevel(s)));
            foreach (var e in upgrades) rows.Add(Row(CharacterSection.Upgrade, e.id, e.title, e.detail, e.value));
            foreach (var b in bonuses) rows.Add(Row(CharacterSection.Bonus, b.Id, b.Name(chinese), "", b.Value(chinese)));
            return rows;
        }

        /// <summary>A skill's level as its chip shows it: "LV 3", or the void's mark for a greater void skill.</summary>
        public static string SkillLevel(CharacterSkill skill) => skill.greaterVoid ? ArenaText.Pair("虚空", "VOID") : "LV " + skill.level;

        /// <summary>A skill's energy cost and cooldown, as the screen shows them.</summary>
        public static string SkillDetail(CharacterSkill skill) =>
            ArenaText.Pair($"{skill.cost:0} 能量 · 冷却 {skill.cooldown:0.#} 秒", $"{skill.cost:0} energy · {skill.cooldown:0.#} s cooldown");

        static CharacterRow Row(CharacterSection section, string id, string title, string detail, string value) =>
            new() { section = section, id = id, title = title ?? "", detail = detail ?? "", value = value ?? "" };
    }
}
