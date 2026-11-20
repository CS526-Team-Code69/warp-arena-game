using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// What the character screen (V5CharacterScreen, CharacterSheet) reads of a story run, in the study window's own words and numbers:
    /// the day and the time of day for the screen's banner, the study's purchases as one line each, which weapons carry the void Mk,
    /// and the boss penetration the weapon Mk's milestones give, which the player's stats do not hold. It reads only; nothing here buys or
    /// changes anything. A weapon's own purchases (Tame, Twist, an infusion, the void enchant) show on its card, the skills in their list.
    /// </summary>
    public sealed partial class V2Campaign
    {
        /// <summary>The day and the time of day, as the character screen's banner shows them.</summary>
        public string CharacterHeading
        {
            get
            {
                if (!Running || !rules) return "";
                string day = Overtime ? ArenaText.Pair($"第 {Day} 天（超时）", $"DAY {Day} (OVERTIME)")
                    : Day == 0 ? ArenaText.Pair("第 0 天", "DAY 0") : ArenaText.Pair($"第 {Day} / {rules.days} 天", $"DAY {Day} / {rules.days}");
                string phase = WorldMap.Active && WorldMap.Active.TrialActive ? ArenaText.Pair("虚空试炼", "THE VOID TRIAL") : Stage switch
                {
                    CampaignStage.Day or CampaignStage.Lull => ArenaText.Pair("上午", "MORNING"),
                    CampaignStage.Dusk => ArenaText.Pair("下午", "AFTERNOON"),
                    CampaignStage.Night => ArenaText.Pair("夜晚", "NIGHT"),
                    CampaignStage.Tutorial => ArenaText.Pair("坠落之后", "AFTER THE FALL"),
                    CampaignStage.FinalBoss => ArenaText.Pair("最终决战", "FINAL BATTLE"),
                    CampaignStage.Core => ArenaText.Pair("暗物质核心", "THE CORE"),
                    _ => "",
                };
                return phase.Length > 0 ? day + " · " + phase : day;
            }
        }

        /// <summary>The share of an element's damage the weapon Mk's milestones let through a boss's immunity (the hook ElementStatus
        /// reads for the player's hits on a boss); 0 below the first milestone and outside a running campaign.</summary>
        public float CharacterBossPenetration => RoomsLive ? rules.MkPenetration(PenetrationMk) : 0f;

        /// <summary>The weapon fights at the void Mk (bought with void alloy past rules.voidMkFrom), whose mark is black.</summary>
        public bool CharacterVoidMk(RunWeapon weapon) => RoomsLive && weapon != null && weapon.IsAnomalous && EffectiveMk(weapon) > rules.voidMkFrom;

        /// <summary>The study's purchases this run, one line for each kind bought: the shared weapon Mk, the armor, each research line's
        /// tier, each body stat's step, the final overload and the void armor. A kind not bought yet makes no line.</summary>
        public void CharacterUpgrades(List<CharacterEntry> into)
        {
            if (into == null || !Running || !rules || !gm) return;
            if (rules.sharedWeaponMk && teamMk > 1)
            {
                bool voidMk = teamMk > rules.voidMkFrom;
                float damage = rules.MkDamage(teamMk);
                into.Add(new CharacterEntry
                {
                    id = "study.mk",
                    title = ArenaText.Pair("武器型号", "Weapon Mk"),
                    detail = voidMk ? ArenaText.Pair($"虚空型号：所有异常武器伤害 ×{damage:0.##}", $"The void Mk: every anomaly weapon deals ×{damage:0.##}")
                        : ArenaText.Pair($"所有异常武器伤害 ×{damage:0.##}", $"Every anomaly weapon deals ×{damage:0.##}"),
                    value = $"Mk {teamMk}",
                    rarity = voidMk ? CardRarity.Void : StudyLadder(Mathf.Min(teamMk, rules.MaxAlloyMk), rules.MaxAlloyMk),
                    art = StudyAxisIcon(UpgradeAxis.Power), flat = WhiteboxIcons.ForStudy("mk"),
                });
            }

            var progression = gm.progression;
            var growth = progression ? progression.Rules : null;
            int armor = ArmorLevel;
            if (armor > 0)
            {
                float hpStep = growth ? growth.maxHpPerLevel : 0f, energyStep = growth ? growth.maxEnergyPerLevel : 0f;
                into.Add(new CharacterEntry
                {
                    id = "study.armor",
                    title = ArenaText.Pair("装甲", "Armor"),
                    detail = ArenaText.Pair($"每级最大生命 +{hpStep:0}、最大能量 +{energyStep:0}", $"Each level: max HP +{hpStep:0}, max energy +{energyStep:0}"),
                    value = ArenaText.Pair($"{armor} 级", $"Lv {armor}"),
                    rarity = StudyLadder(Mathf.Min(armor, rules.MaxArmorLevel), rules.MaxArmorLevel),
                    art = art ? art.armor : null, flat = WhiteboxIcons.ForStudy("armor"),
                });
            }

            foreach (var line in new[] { ResearchLine.Body, ResearchLine.Skills, ResearchLine.Infusion })
            {
                int tier = ResearchTierShown(line);
                if (tier <= 0) continue;
                into.Add(new CharacterEntry
                {
                    id = "study.research." + line,
                    title = ResearchName(line),
                    detail = tier >= rules.MaxResearchTier ? ArenaText.Pair("已到最高阶", "At its top tier")
                        : ArenaText.Pair($"一共 {rules.MaxResearchTier} 阶", $"{rules.MaxResearchTier} tiers in all"),
                    value = ArenaText.Pair($"{tier} 阶", $"Tier {tier}"),
                    rarity = StudyLadder(tier, rules.MaxResearchTier),
                    art = art ? art.energyLab : null, flat = WhiteboxIcons.ForStudy("tier." + line),
                });
            }

            // The body research's stats: with stat lines (CampaignRules.statLineMode Replace) the highest step bought, which replaced the
            // ones below; otherwise how many rarities of the stat were bought.
            var rows = growth ? growth.statCards : null;
            if (rows != null && progression)
                foreach (var row in rows)
                {
                    if (row == null || row.retired) continue;
                    var stat = row.stat;
                    int steps = 0;
                    CardRarity top = CardRarity.Common;
                    if (rules.statLineMode == StatLineMode.Replace)
                    {
                        steps = StatLineSteps(stat);
                        if (steps > 0) top = CampaignRules.StatLineRarity(steps - 1) ?? CardRarity.Common;
                    }
                    else
                        foreach (var rarity in StudyRarities)
                            if (StatBought(stat, rarity)) { steps++; top = rarity; }
                    if (steps <= 0) continue;
                    bool lines = rules.statLineMode == StatLineMode.Replace;
                    into.Add(new CharacterEntry
                    {
                        id = "study.stat." + stat,
                        title = new StatCardOffer(stat, top, 0f).Title,
                        detail = ArenaText.Pair("体质研究 · 现在 ", "Body research · now ") + StudyStatValue(stat, progression.Stats),
                        value = lines ? ArenaText.Pair($"{ArenaTerms.Rarity(top)}档", ArenaTerms.Rarity(top)) : $"×{steps}",
                        rarity = top,
                        art = StudyArt ? StudyIcon(StudyArt.statIcons, (int)stat) : null, flat = WhiteboxIcons.ForStudy("stat." + stat),
                    });
                }

            if (overloadShieldBuys > 0)
                into.Add(new CharacterEntry
                {
                    id = "study.overload.shield",
                    title = ArenaText.Pair("最终决战开局护盾", "Final Battle: opening shield"),
                    detail = ArenaText.Pair($"决战过载，买了 {overloadShieldBuys} 次", $"Final overload, bought {overloadShieldBuys} times"),
                    value = $"+{overloadShieldBuys * rules.overloadShieldShare * 100f:0}%",
                    rarity = CardRarity.Legendary,
                    art = art ? art.shield : null, flat = WhiteboxIcons.ForStudy("overload.shield"),
                });
            if (overloadDamageBuys > 0)
                into.Add(new CharacterEntry
                {
                    id = "study.overload.damage",
                    title = ArenaText.Pair("最终决战伤害加成", "Final Battle: damage bonus"),
                    detail = ArenaText.Pair($"决战过载，买了 {overloadDamageBuys} 次", $"Final overload, bought {overloadDamageBuys} times"),
                    value = $"+{overloadDamageBuys * rules.overloadDamageShare * 100f:0}%",
                    rarity = CardRarity.Legendary,
                    art = art ? art.durability : null, flat = WhiteboxIcons.ForStudy("overload.damage"),
                });

            if (voidArmor)
                into.Add(new CharacterEntry
                {
                    id = "study.voidArmor",
                    title = ArenaText.Pair("虚空装甲附魔", "Void armor enchant"),
                    detail = ArenaText.Pair($"能量每秒恢复 {rules.voidArmorEnergyRegen:0.#}", $"Energy regenerates {rules.voidArmorEnergyRegen:0.#} a second"),
                    value = "",
                    rarity = CardRarity.Void,
                    art = art ? art.armor : null, flat = WhiteboxIcons.ForStudy("void.armor"),
                });
        }
    }
}
