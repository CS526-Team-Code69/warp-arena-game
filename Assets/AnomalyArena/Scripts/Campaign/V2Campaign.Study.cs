using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// What the workshop lists (BuildStudy): one tab per kind of purchase, in two columns, each tile a StudyEntry with its rarity colour,
    /// its number before and after, its price and its purchase (V2Campaign.Rooms.cs). The weapon column (alloy): the shared weapon Mk
    /// and armor, then Tame and Twist per hand weapon. The energy research column (alien energy): body, skill and infusion items, each
    /// line's research level last. The void tabs come from CollectVoidStudy; the overload shows from its day. A tab whose day has not come
    /// is locked and says when it opens (lockedNote); Tame and Twist still list the hand weapons then, each tile locked. No text here says
    /// "card": the campaign has no cards.
    /// </summary>
    public sealed partial class V2Campaign
    {
        /// <summary>One tab of the workshop: its column's header (roomName), its name and its tiles. A locked tab's lockedNote says when it
        /// opens; emptyNote says why an open tab has nothing in it.</summary>
        public sealed class StudyTab
        {
            public string key, roomName, name, lockedNote, emptyNote;
            public bool locked;
            public readonly List<StudyEntry> entries = new();
        }

        static string StudyHidden => ArenaText.Pair("？？？", "???");
        /// <summary>A column's header: the weapons (alloy), the energy research (alien energy), the void.</summary>
        static string RoomTitle(StudyRoom room) => room switch
        {
            StudyRoom.Armament => ArenaText.Pair("武器", "Weapons"),
            StudyRoom.Energy => ArenaText.Pair("能量研究", "Energy research"),
            _ => ArenaText.Pair("虚空", "Void")
        };

        /// <summary>Every tab of the workshop with its tiles, as things stand now. Call it when a page opens and after each purchase.
        /// Keys: arm.upgrade (the weapon Mk and armor), arm.tame, arm.twist, eng.body, eng.skill, eng.infuse, void.* (once the hidden boss
        /// is beaten), ctl.overload (from the overload day).</summary>
        public List<StudyTab> BuildStudy()
        {
            var tabs = new List<StudyTab>();
            if (!Running || !rules || !gm) return tabs;
            string weapons = RoomTitle(StudyRoom.Armament), energy = RoomTitle(StudyRoom.Energy);
            tabs.Add(StudyTabOf("arm.upgrade", weapons, ArenaText.Pair("武器升级", "Upgrades"), null, FillUpgrades));
            tabs.Add(StudyTabOf("arm.tame", weapons, FeatureName(StudyFeature.Tame), StudyFeature.Tame, t => FillTameTwist(t, UpgradeAxis.Tame), true));
            tabs.Add(StudyTabOf("arm.twist", weapons, FeatureName(StudyFeature.Twist), StudyFeature.Twist, t => FillTameTwist(t, UpgradeAxis.Twist), true));
            tabs.Add(StudyTabOf("eng.body", energy, FeatureName(StudyFeature.Body), StudyFeature.Body, FillBody));
            tabs.Add(StudyTabOf("eng.skill", energy, FeatureName(StudyFeature.Skills), StudyFeature.Skills, FillSkills));
            tabs.Add(StudyTabOf("eng.infuse", energy, FeatureName(StudyFeature.Infuse), StudyFeature.Infuse, FillInfusion));
            // The void's tiles (the void currencies' part), one tab per category they name; none before the hidden boss is beaten.
            var voids = new List<StudyEntry>();
            CollectVoidStudy(voids);
            foreach (var entry in voids)
            {
                if (entry == null) continue;
                StudyVoidPrice(entry);
                string category = string.IsNullOrEmpty(entry.category) ? RoomTitle(StudyRoom.Void) : entry.category;
                var tab = tabs.Find(t => t.key == "void." + category);
                if (tab == null) tabs.Add(tab = new StudyTab { key = "void." + category, roomName = RoomTitle(StudyRoom.Void), name = category });
                tab.entries.Add(entry);
            }
            if (Day >= rules.overloadDay)
            {
                var overload = new StudyTab { key = "ctl.overload", roomName = weapons, name = ArenaText.Pair("决战过载", "Final overload") };
                FillOverload(overload);
                tabs.Add(overload);
            }
            return tabs;
        }

        /// <summary>A tab, locked until its feature's day (none: always open). A locked tab says when it opens (OpensNote) and, with
        /// listLocked, still lists its tiles, each locked (Tame and Twist name the hand weapons they will work on).</summary>
        StudyTab StudyTabOf(string key, string roomName, string name, StudyFeature? feature, Action<StudyTab> fill, bool listLocked = false)
        {
            var tab = new StudyTab { key = key, roomName = roomName, name = name };
            if (feature is { } opens && !FeatureOpen(opens))
            {
                tab.locked = true;
                tab.lockedNote = OpensNote(opens);
                if (listLocked) fill(tab);
                return tab;
            }
            fill(tab);
            return tab;
        }

        // ───── Tile builders ─────

        StudyEntry StudyItem(string id, StudyRoom room, string category, CardRarity rarity, Sprite icon, string title, string now, string after,
            string detail, CampaignCost? price, bool owned, string blocked, Func<bool> buy)
        {
            var state = owned || price == null ? StudyState.Owned : blocked != null || !CanAfford(price.Value) ? StudyState.Unaffordable : StudyState.Available;
            if (state == StudyState.Unaffordable) detail += "\n\n<color=#ff8a6a>" + (blocked ?? StudyShortOf(price.Value)) + "</color>";
            return new StudyEntry
            {
                id = id, room = room, category = category, rarity = rarity, icon = icon, title = title, now = now ?? "", after = after ?? "",
                detail = detail, costText = price is { } p && !owned ? PriceText(p) : "", price = owned ? null : price, state = state, buy = buy
            };
        }

        static StudyEntry StudyUnknown(string id, StudyRoom room, string category, string unlock) => new()
        {
            id = id, room = room, category = category, rarity = CardRarity.Common, title = StudyHidden, now = "", after = "",
            detail = unlock, costText = "", state = StudyState.Locked, buy = null
        };

        /// <summary>A Void tile's price as numbers, so a page can draw it as icons (the void currencies' part writes it in words only):
        /// what each kind of Void tile costs, as its purchase charges it. A tile that carries its numbers already, or has no price, is left
        /// as it is.</summary>
        void StudyVoidPrice(StudyEntry entry)
        {
            if (entry.voidEnergyCost > 0 || entry.voidAlloyCost > 0 || entry.state == StudyState.Owned || string.IsNullOrEmpty(entry.costText) || entry.id == null) return;
            if (entry.id.StartsWith("void.skill.", StringComparison.Ordinal)) entry.voidEnergyCost = rules.voidSkillCost;
            else if (entry.id.StartsWith("void.enchant.", StringComparison.Ordinal)) entry.voidAlloyCost = rules.voidWeaponEnchantCost;
            else if (entry.id == "void.mk") entry.voidAlloyCost = rules.voidMkCost;
            else if (entry.id == "void.armor") entry.voidAlloyCost = rules.voidArmorCost;
        }

        public static string PriceText(CampaignCost cost)
        {
            var parts = new List<string>();
            if (cost.alloy > 0) parts.Add(ArenaText.Pair($"合金 {cost.alloy}", $"{cost.alloy} alloy"));
            if (cost.energy > 0) parts.Add(ArenaText.Pair($"异星能量 {cost.energy}", $"{cost.energy} alien energy"));
            if (cost.dark > 0) parts.Add(ArenaText.Pair($"暗物质 {cost.dark}", $"{cost.dark} dark matter"));
            return parts.Count == 0 ? ArenaText.Pair("免费", "free") : string.Join(" + ", parts);
        }

        string StudyShortOf(CampaignCost cost)
        {
            var missing = new List<string>();
            if (Alloy < cost.alloy) missing.Add(ArenaText.Pair($"合金还差 {cost.alloy - Alloy}", $"{cost.alloy - Alloy} alloy short"));
            if (Energy < cost.energy) missing.Add(ArenaText.Pair($"异星能量还差 {cost.energy - Energy}", $"{cost.energy - Energy} alien energy short"));
            if (Dark < cost.dark) missing.Add(ArenaText.Pair($"暗物质还差 {cost.dark - Dark}", $"{cost.dark - Dark} dark matter short"));
            return missing.Count > 0 ? string.Join(ArenaText.Pair("，", ", "), missing) : ArenaText.Pair("现在还买不了", "Can't buy this yet");
        }

        V2ArtSet StudyArt => gm && gm.hud && gm.hud.v2Hud ? gm.hud.v2Hud.art : null;
        static Sprite StudyIcon(Sprite[] sprites, int index) => sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
        Sprite StudyWeaponIcon(WeaponForm form) => StudyArt ? StudyIcon(StudyArt.weaponSprites, (int)form) : null;
        Sprite StudyAxisIcon(UpgradeAxis axis) => StudyArt ? StudyIcon(StudyArt.axisIcons, axis switch { UpgradeAxis.Infusion => 0, UpgradeAxis.Power => 1, UpgradeAxis.Tempo => 2, UpgradeAxis.Reach => 3, UpgradeAxis.Twist => 4, _ => 5 }) : null;
        Sprite StudyElementIcon(ElementKind element) => StudyArt ? StudyIcon(StudyArt.elementIcons, (int)element - 1) : null;

        /// <summary>A colour for steps along a ladder of `top` steps, white first and orange last.</summary>
        static CardRarity StudyLadder(int step, int top)
        {
            float share = top <= 1 ? 1f : (step - 1) / (float)(top - 1);
            return share < .2f ? CardRarity.Common : share < .4f ? CardRarity.Rare : share < .65f ? CardRarity.Exceptional : share < .9f ? CardRarity.Epic : CardRarity.Legendary;
        }

        /// <summary>A rarity by its colour, as the research levels name what they open.</summary>
        static string RarityColourName(CardRarity rarity) => rarity switch
        {
            CardRarity.Common => ArenaText.Pair("白色", "white"),
            CardRarity.Rare => ArenaText.Pair("绿色", "green"),
            CardRarity.Exceptional => ArenaText.Pair("蓝色", "blue"),
            CardRarity.Epic => ArenaText.Pair("紫色", "purple"),
            CardRarity.Legendary => ArenaText.Pair("橙色", "orange"),
            _ => ArenaTerms.Rarity(rarity)
        };

        // ───── The weapon column ─────

        void FillUpgrades(StudyTab tab)
        {
            string category = tab.name;
            // The weapon Mk: one tile, the shared Mk (with sharedWeaponMk off, the weapon a purchase raises).
            int from = NextMkFrom;
            var target = rules.sharedWeaponMk ? null : MkTarget;
            var mkPrice = rules.MkPrice(from);
            float damageNow = rules.MkDamage(from), damageNext = rules.MkDamage(from + 1);
            string who = rules.sharedWeaponMk ? ArenaText.Pair("所有异常武器", "every anomaly weapon") : target != null ? target.FormName : ArenaText.Pair("异常武器", "the anomaly weapon");
            string mkDetail = rules.sharedWeaponMk
                ? ArenaText.Pair("武器型号对所有异常武器一起生效：升一次，手上的和以后拿到的异常武器都按新型号计算伤害。开局的小手枪不受型号影响。",
                    "The weapon Mk covers all your anomaly weapons at once: raise it once, and every anomaly weapon you hold now or get later deals damage at that Mk. The starting pistol isn't affected.")
                : ArenaText.Pair("每把武器的型号单独升级，先升手上这一把。开局的小手枪不能升级。", "Each weapon's Mk is raised on its own, starting with the one in your hand. The starting pistol can't be upgraded.");
            string milestones = string.Join(" / ", Array.ConvertAll(rules.weaponMkMilestones ?? Array.Empty<int>(), m => "Mk " + m));
            mkDetail += "\n" + ArenaText.Pair($"型号每达到 {milestones} 中的一档，武器就额外获得 {rules.mkMilestoneDamage * 100f:0}% 固定伤害和 {rules.mkMilestonePenetration * 100f:0}% 穿透，这些加成可以叠加。穿透能打穿 Boss 的属性免疫，你现在的穿透是 {rules.MkPenetration(from) * 100f:0}%。",
                $"At each of {milestones}, your weapons gain another {rules.mkMilestoneDamage * 100f:0}% fixed damage and {rules.mkMilestonePenetration * 100f:0}% penetration, and these bonuses stack. Penetration gets through a boss's element immunity; yours is now {rules.MkPenetration(from) * 100f:0}%.");
            if (mkPrice == null) mkDetail += "\n" + ArenaText.Pair($"用合金最多只能升到 Mk {rules.MaxAlloyMk}。", $"Alloy can raise it only as far as Mk {rules.MaxAlloyMk}.");
            var mk = StudyItem("mk", StudyRoom.Armament, category, StudyLadder(Mathf.Min(from + 1, rules.MaxAlloyMk), rules.MaxAlloyMk), StudyAxisIcon(UpgradeAxis.Power),
                ArenaText.Pair("武器型号", "Weapon Mk"),
                $"Mk {from}", mkPrice != null ? $"Mk {from + 1}" : "",
                mkDetail, mkPrice, mkPrice == null,
                !rules.sharedWeaponMk && target == null ? ArenaText.Pair("先从武器箱拿一把异常武器", "Take an anomaly weapon from a weapon crate first") : null,
                TryBuyWeaponDamage);
            mk.effect = mkPrice != null
                ? ArenaText.Pair($"{who}的伤害 ×{damageNow:0.##} → ×{damageNext:0.##}", $"Damage of {who} ×{damageNow:0.##} → ×{damageNext:0.##}")
                : ArenaText.Pair($"{who}的伤害 ×{damageNow:0.##}", $"Damage of {who} ×{damageNow:0.##}");
            tab.entries.Add(mk);
            // Armor: five levels, each one player level (max HP and max charge).
            var progression = gm.progression;
            var growth = progression ? progression.Rules : null;
            var armorPrice = PlayerLevelCost;
            int armor = ArmorLevel;
            float hpStep = growth ? growth.maxHpPerLevel : 0f, energyStep = growth ? growth.maxEnergyPerLevel : 0f;
            float hp = (progression ? progression.Stats.maxHp : 0f) + DayHpBonus, maxEnergy = progression ? progression.Stats.maxEnergy : 0f;
            var armorItem = StudyItem("armor", StudyRoom.Armament, category, StudyLadder(Mathf.Min(armor + 1, rules.MaxArmorLevel), rules.MaxArmorLevel), art ? art.armor : null,
                ArenaText.Pair("护甲", "Armor"),
                ArenaText.Pair($"{armor} 级", $"Level {armor}"),
                armorPrice != null ? ArenaText.Pair($"{armor + 1} 级", $"Level {armor + 1}") : "",
                ArenaText.Pair($"护甲每升一级，生命上限 +{hpStep:0}、充能上限 +{energyStep:0}，并立刻回满生命。护甲一共 {rules.MaxArmorLevel} 级，你现在的充能上限是 {maxEnergy:0}。",
                    $"Each armor level adds {hpStep:0} max HP and {energyStep:0} max charge, and heals you to full. Armor has {rules.MaxArmorLevel} levels in all, and your max charge is now {maxEnergy:0}."),
                armorPrice, armorPrice == null, null, TryBuyPlayerLevel);
            armorItem.effect = armorPrice != null
                ? ArenaText.Pair($"生命上限 {hp:0} → {hp + hpStep:0}", $"Max HP {hp:0} → {hp + hpStep:0}")
                : ArenaText.Pair($"生命上限 {hp:0}", $"Max HP {hp:0}");
            tab.entries.Add(armorItem);
        }

        /// <summary>Tame or Twist, one tile per anomaly weapon in hand (hand: which one). Before the feature's day the tiles are locked and
        /// say when it opens, so a page can still show the weapons.</summary>
        void FillTameTwist(StudyTab tab, UpgradeAxis axis)
        {
            var weapons = gm.runWeapons;
            if (!weapons) return;
            var price = new CampaignCost(axis == UpgradeAxis.Tame ? rules.tameAlloy : rules.twistAlloy);
            string name = ArenaTerms.Upgrade(axis), kind = axis == UpgradeAxis.Tame ? "tame" : "twist";
            for (int hand = 0; hand < weapons.Hand.Length; hand++)
            {
                var w = weapons.Hand[hand];
                if (w == null || !w.IsAnomalous) continue;
                StudyEntry entry;
                if (tab.locked)
                {
                    entry = StudyUnknown($"{kind}.{w.form}", StudyRoom.Armament, tab.name, tab.lockedNote);
                    entry.title = w.Label;
                    entry.icon = StudyWeaponIcon(w.form);
                    entry.rarity = ArenaRarity.ForWeaponCard(axis);
                }
                else
                {
                    bool owned = w.Has(axis);
                    string text = (w.revealed ? weapons.UpgradeDescription(w, axis) : ArenaText.Pair("这把武器的异常还没揭晓。", "This weapon's anomaly hasn't been revealed yet."))
                        + "\n" + TameTwistBonusText(axis); // its attack speed or damage besides (V2Campaign.Days.cs)
                    string blocked = !w.revealed ? ArenaText.Pair("先用它攻击一次，揭晓它的异常", "Fire it once first to reveal its anomaly") : null;
                    var weapon = w;
                    entry = StudyItem($"{kind}.{w.form}", StudyRoom.Armament, tab.name, ArenaRarity.ForWeaponCard(axis), StudyWeaponIcon(w.form),
                        w.Label, owned ? ArenaText.Pair($"已{name}", $"Has {name}") : ArenaText.Pair($"未{name}", $"No {name} yet"), owned ? "" : ArenaText.Pair($"已{name}", $"Has {name}"),
                        text, price, owned, blocked, () => TryTameOrTwist(weapon, axis));
                }
                entry.hand = hand;
                tab.entries.Add(entry);
            }
            if (tab.entries.Count == 0) tab.emptyNote = ArenaText.Pair("这一项只能用在手持的异常武器上，你手上现在没有。", "This works only on an anomaly weapon in your hand, and you aren't holding one.");
        }

        // ───── The energy research column ─────

        /// <summary>The research level tile, last in a line's tab: its next level, coloured by the rarest items it opens; effect names the
        /// colours it opens.</summary>
        void AddResearchTile(StudyTab tab, ResearchLine line)
        {
            int tier = ResearchTierShown(line);
            var price = rules.ResearchPrice(tier);
            CardRarity opens = CardRarity.Common;
            var colours = new List<string>();
            foreach (var r in StudyRarities)
                if (rules.RarityTier(r) == tier + 1) { opens = r; colours.Add(RarityColourName(r)); }
            var tile = StudyItem("tier." + line, StudyRoom.Energy, tab.name, price != null ? opens : CardRarity.Legendary, art ? art.energyLab : null,
                ResearchName(line),
                ArenaText.Pair($"{tier} 级", $"Level {tier}"), price != null ? ArenaText.Pair($"{tier + 1} 级", $"Level {tier + 1}") : "",
                ArenaText.Pair($"研究每升一级，就开放更稀有、更强的项目（一共 {rules.MaxResearchTier} 级）。", $"Each research level opens rarer, stronger items ({rules.MaxResearchTier} levels in all)."),
                price, price == null, null, () => TryBuyResearchTier(line));
            string opened = string.Join(ArenaText.Pair("和", " and "), colours);
            tile.effect = price != null && colours.Count > 0 ? ArenaText.Pair($"开放{opened}品质", $"Opens {opened} items") : "";
            tab.entries.Add(tile);
        }

        static readonly CardRarity[] StudyRarities = { CardRarity.Common, CardRarity.Rare, CardRarity.Exceptional, CardRarity.Epic, CardRarity.Legendary };

        string StudyTierNote(ResearchLine line, int level) => ArenaText.Pair($"{ResearchName(line)}到 {level} 级后揭晓。", $"Revealed at {ResearchName(line)} level {level}.");

        void FillBody(StudyTab tab)
        {
            // Stat lines (CampaignRules.statLineMode Replace): one tile per stat, its next step only (V2Campaign.Days.cs).
            if (rules.statLineMode == StatLineMode.Replace) FillBodyLines(tab);
            else FillBodyStacks(tab);
            AddResearchTile(tab, ResearchLine.Body);
        }

        /// <summary>The body tab with every stat at every rarity once (CampaignRules.statLineMode Stack).</summary>
        void FillBodyStacks(StudyTab tab)
        {
            var progression = gm.progression;
            var rows = progression && progression.Rules ? progression.Rules.statCards : null;
            if (rows == null) return;
            int tier = ResearchTierShown(ResearchLine.Body);
            foreach (var rarity in StudyRarities)
                foreach (var row in rows)
                {
                    if (row == null || row.retired || !rules.BodySells(row.stat)) continue;
                    var stat = row.stat;
                    string id = $"stat.{stat}.{rarity}";
                    if (rules.RarityTier(rarity) > tier) { tab.entries.Add(StudyUnknown(id, StudyRoom.Energy, tab.name, StudyTierNote(ResearchLine.Body, rules.RarityTier(rarity)))); continue; }
                    var offer = new StatCardOffer(stat, rarity, row.At(rarity), row.bonus, row.BonusAt(rarity));
                    var stats = progression.Stats;
                    var after = stats.Copy();
                    after.Add(stat, offer.Value);
                    if (offer.Bonus != StatBonus.None) after.AddBonus(offer.Bonus, offer.BonusValue);
                    bool owned = StatBought(stat, rarity);
                    string blocked = stats.AtCap(stat) ? ArenaText.Pair("这项已到上限", "This stat is at its cap") : null;
                    tab.entries.Add(StudyItem(id, StudyRoom.Energy, tab.name, rarity, StudyArt ? StudyIcon(StudyArt.statIcons, (int)stat) : null, offer.Title,
                        StudyStatValue(stat, stats), owned ? "" : StudyStatValue(stat, after),
                        offer.Description + "\n" + ArenaText.Pair($"{ArenaTerms.Rarity(rarity)}：每项属性在每种稀有度下只能买一次，买下后立刻生效。", $"{ArenaTerms.Rarity(rarity)}: each stat can be bought once at each rarity, and it takes effect right away."),
                        new CampaignCost(0, rules.RarityEnergy(rarity)), owned, blocked, () => TryBuyStat(stat, rarity)));
                }
        }

        /// <summary>A stat as the workshop shows it.</summary>
        static string StudyStatValue(StatId stat, RunStats s) => stat switch
        {
            StatId.MaxHp => $"{s.maxHp:0}",
            StatId.MoveSpeed => $"{s.moveSpeed:0.#}",
            StatId.Damage => $"×{s.DamageMultiplier:0.##}",
            StatId.AttackSpeed => $"+{s.attackSpeedBonus * 100f:0}%",
            StatId.Protection => $"{s.armor:0}",
            StatId.MaxEnergy => $"{s.maxEnergy:0}",
            StatId.ElementPenetration => $"{s.elementPenetration * 100f:0}%",
            StatId.CritChance => $"{s.critChance * 100f:0}%",
            _ => $"{s.Value(stat):0.##}"
        };

        void FillSkills(StudyTab tab)
        {
            var skills = gm.skills;
            if (skills && skills.Catalog && skills.Catalog.skills != null)
            {
                int tier = ResearchTierShown(ResearchLine.Skills);
                float costScale = Mathf.Max(0f, SkillLoadout.CostScale?.Invoke() ?? 1f), cooldownScale = Mathf.Max(0f, SkillLoadout.CooldownScale?.Invoke() ?? 1f);
                var list = new List<SkillDefinition>();
                foreach (var definition in skills.Catalog.skills) if (definition && !definition.greaterVoid) list.Add(definition);
                list.Sort((a, b) => a.category != b.category ? a.category.CompareTo(b.category) : a.id.CompareTo(b.id));
                foreach (var definition in list)
                {
                    var id = definition.id;
                    int next = NextSkillLevel(id, out bool learned);
                    string key = "skill." + id;
                    bool top = next > SkillDefinition.MaxLevel;
                    var rarity = ArenaRarity.ForSkill(definition, Mathf.Min(next, SkillDefinition.MaxLevel));
                    if (!learned && rules.SkillCategoryTier(definition.category) > tier)
                    {
                        tab.entries.Add(StudyUnknown(key, StudyRoom.Energy, tab.name, StudyTierNote(ResearchLine.Skills, rules.SkillCategoryTier(definition.category))));
                        continue;
                    }
                    if (!top && rules.RarityTier(rarity) > tier) { tab.entries.Add(StudyUnknown(key, StudyRoom.Energy, tab.name, StudyTierNote(ResearchLine.Skills, rules.RarityTier(rarity)))); continue; }
                    string blocked = !learned && skills.OwnedCount >= skills.Slots.Length ? ArenaText.Pair($"技能栏已满（一共 {skills.Slots.Length} 个）", $"All {skills.Slots.Length} skill slots are full") : null;
                    int shown = Mathf.Min(next, SkillDefinition.MaxLevel);
                    string detail = definition.Description(shown) + "\n" + ArenaText.Pair($"每次使用消耗 {definition.cost * costScale:0} 充能，冷却 {definition.Cooldown(shown) * cooldownScale:0.#} 秒。",
                        $"Each cast costs {definition.cost * costScale:0} charge and has a {definition.Cooldown(shown) * cooldownScale:0.#} s cooldown.");
                    tab.entries.Add(StudyItem(key, StudyRoom.Energy, tab.name, rarity, StudyArt ? StudyIcon(StudyArt.skillIcons, (int)id) : art ? art.skill : null, ArenaTerms.Skill(id),
                        learned ? ArenaText.Pair($"等级 {next - 1}", $"Level {next - 1}") : ArenaText.Pair("未学", "Not learned"),
                        top ? "" : ArenaText.Pair($"等级 {next}", $"Level {next}"),
                        detail, top ? null : new CampaignCost(0, rules.RarityEnergy(rarity)), top, blocked, () => TryBuySkill(id)));
                }
            }
            AddResearchTile(tab, ResearchLine.Skills);
        }

        void FillInfusion(StudyTab tab)
        {
            var weapons = gm.runWeapons;
            if (weapons && rules.infusionChoices != null)
            {
                int tier = ResearchTierShown(ResearchLine.Infusion);
                float floor = rules.InfusionFloor(Mathf.Max(1, Day), tier, TyrantFight);
                // The floor follows a table by day (CampaignRules.infusionFloorByDay); the linear rule only without it.
                float perTier = rules.InfusionFloor(Mathf.Max(1, Day), 1, TyrantFight);
                string floorNote = rules.infusionFloorByDay is { Length: > 0 }
                    ? ArenaText.Pair($"附魔保底：身上有附魔过的武器时，你造成的燃烧和中毒每秒伤害至少 {floor:0}。保底按附魔研究的等级计算，今天每级 {perTier:0.#}，每天都不一样。",
                        $"Infusion floor: while you carry an infused weapon, your burns and poisons always deal at least {floor:0} per second. The floor is {perTier:0.#} per Infusion research level today, and it changes from day to day.")
                    : ArenaText.Pair($"附魔保底：身上有附魔过的武器时，你造成的燃烧和中毒每秒伤害至少 {floor:0}（{rules.infusionFloorPerDay:0} × 天数 × 附魔研究等级）。",
                        $"Infusion floor: while you carry an infused weapon, your burns and poisons always deal at least {floor:0} per second ({rules.infusionFloorPerDay:0} × day × Infusion research level).");
                var held = new List<(RunWeapon weapon, int hand)>();
                for (int i = 0; i < weapons.Hand.Length; i++) if (weapons.Hand[i] is { IsAnomalous: true, builtInElement: false } w) held.Add((w, i));
                foreach (var w in weapons.Auto) if (w is { IsAnomalous: true, builtInElement: false }) held.Add((w, -1));
                foreach (var (w, hand) in held)
                {
                    var weapon = w;
                    var own = weapons.ElementOf(w);
                    foreach (var choice in rules.infusionChoices)
                    {
                        var element = choice.element;
                        if (element is ElementKind.None or ElementKind.Void) continue;
                        string id = $"infuse.{w.form}.{element}";
                        StudyEntry entry;
                        if (rules.RarityTier(choice.rarity) > tier) entry = StudyUnknown(id, StudyRoom.Energy, tab.name, StudyTierNote(ResearchLine.Infusion, rules.RarityTier(choice.rarity)));
                        else
                        {
                            bool owned = own == element;
                            string rule = ArenaTerms.ElementRule(element);
                            string detail = (string.IsNullOrEmpty(rule) ? "" : rule + "\n")
                                + (own != ElementKind.None && !owned ? ArenaText.Pair($"会替换这把武器现有的{ArenaTerms.Element(own)}属性。", $"This replaces the weapon's current {ArenaTerms.Element(own)}.") + "\n" : "") + floorNote;
                            entry = StudyItem(id, StudyRoom.Energy, tab.name, choice.rarity, StudyElementIcon(element),
                                ArenaText.Pair($"{w.FormName} · {ArenaTerms.Element(element)}", $"{w.FormName} · {ArenaTerms.Element(element)}"),
                                ArenaTerms.Element(own), owned ? "" : ArenaTerms.Element(element),
                                detail, new CampaignCost(0, rules.RarityEnergy(choice.rarity)), owned, null, () => TryInfuse(weapon, element));
                        }
                        entry.hand = hand;
                        tab.entries.Add(entry);
                    }
                }
                if (tab.entries.Count == 0) tab.emptyNote = ArenaText.Pair("你没有能附魔的异常武器。自带元素的武器不能附魔。", "None of your anomaly weapons can be infused. Weapons with a built-in element can't take an infusion.");
            }
            AddResearchTile(tab, ResearchLine.Infusion);
        }

        // ───── The final overload ─────

        void FillOverload(StudyTab tab)
        {
            foreach (bool shield in new[] { true, false })
                foreach (bool withEnergy in new[] { false, true })
                {
                    int bought = shield ? overloadShieldBuys : overloadDamageBuys;
                    float share = shield ? rules.overloadShieldShare : rules.overloadDamageShare;
                    bool full = bought >= rules.overloadMaxBuys;
                    var price = withEnergy ? new CampaignCost(0, rules.overloadEnergy) : new CampaignCost(rules.overloadAlloy);
                    bool s = shield, e = withEnergy;
                    // StudyRoom has no column of its own for these: they carry Armament, and the tab sits in the weapon column.
                    tab.entries.Add(StudyItem($"overload.{(shield ? "shield" : "damage")}.{(withEnergy ? "energy" : "alloy")}", StudyRoom.Armament, tab.name, CardRarity.Legendary,
                        art ? shield ? art.shield : art.durability : null,
                        shield ? ArenaText.Pair("最终决战开局护盾", "Final Battle: opening shield") : ArenaText.Pair("最终决战伤害加成", "Final Battle: damage bonus"),
                        $"+{bought * share * 100f:0}%", full ? "" : $"+{(bought + 1) * share * 100f:0}%",
                        (shield ? ArenaText.Pair($"最终决战开始时，每买一次，你开场就多得到相当于生命上限 {share * 100f:0}% 的护盾，可以超过平时的护盾上限。几秒后，它会像普通护盾一样慢慢消退。",
                                $"When the Final Battle begins, each purchase gives you a shield worth {share * 100f:0}% of your max HP, even above the usual cap. After a few seconds it fades like any other shield.")
                            : ArenaText.Pair($"最终决战中，每买一次，武器和技能的伤害就 +{share * 100f:0}%，可以叠加。", $"In the Final Battle, each purchase adds +{share * 100f:0}% to your weapon and skill damage. Purchases stack."))
                        + "\n" + ArenaText.Pair($"护盾和伤害各最多买 {rules.overloadMaxBuys} 次，用合金买和用异星能量买都算在内。", $"You can buy the shield and the damage up to {rules.overloadMaxBuys} times each, whether you pay with alloy or alien energy."),
                        price, full, null, () => TryBuyOverload(s, e)));
                }
        }
    }
}
