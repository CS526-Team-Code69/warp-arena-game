using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// What the base's study window lists (2026-11-10): one tab per kind of purchase, grouped by room, each tile a StudyEntry with its
    /// rarity colour, its number before and after, its price and its purchase (V2Campaign.Rooms.cs). The Armament Bay sells the shared
    /// weapon Mk and armor, new weapons by form, Tame and Twist per weapon; the Energy Lab sells body, skill and infusion items and the
    /// three research tiers; the void tabs come from CollectVoidStudy; Central Control's overload shows on the last day. A tab whose room
    /// level is not built yet, and a tile whose research tier or day has not come, show only a question mark (the V2 doc: a level's new
    /// features are seen only once unlocked). No text here says "card": the campaign has no cards.
    /// </summary>
    public sealed partial class V2Campaign
    {
        /// <summary>One tab of the study window: its room's header, its name and its tiles. A locked tab shows "???" and lockedNote.</summary>
        public sealed class StudyTab
        {
            public string key, roomName, name, lockedNote, emptyNote;
            public bool locked;
            public readonly List<StudyEntry> entries = new();
        }

        static string StudyHidden => ArenaText.Pair("？？？", "???");
        static string RoomTitle(StudyRoom room) => room switch
        {
            StudyRoom.Armament => RoomName(CampaignRoom.Armament),
            StudyRoom.Energy => RoomName(CampaignRoom.Energy),
            _ => ArenaText.Pair("虚空", "Void")
        };

        /// <summary>Every tab of the study window with its tiles, as things stand now. Called when the window opens and after each purchase.</summary>
        public List<StudyTab> BuildStudy()
        {
            var tabs = new List<StudyTab>();
            if (!Running || !rules || !gm) return tabs;
            string armament = RoomTitle(StudyRoom.Armament), energy = RoomTitle(StudyRoom.Energy);
            tabs.Add(StudyTabOf("arm.upgrade", armament, ArenaText.Pair("武器升级", "Upgrades"), CampaignRoom.Armament, 1, FillUpgrades));
            tabs.Add(StudyTabOf("arm.new", armament, ArenaText.Pair("新武器", "New weapons"), CampaignRoom.Armament, 2, FillNewWeapons));
            tabs.Add(StudyTabOf("arm.tame", armament, ArenaTerms.Upgrade(UpgradeAxis.Tame), CampaignRoom.Armament, 2, t => FillTameTwist(t, UpgradeAxis.Tame)));
            tabs.Add(StudyTabOf("arm.twist", armament, ArenaTerms.Upgrade(UpgradeAxis.Twist), CampaignRoom.Armament, 3, t => FillTameTwist(t, UpgradeAxis.Twist)));
            tabs.Add(StudyTabOf("eng.body", energy, ArenaText.Pair("体质", "Body"), CampaignRoom.Energy, 1, FillBody));
            tabs.Add(StudyTabOf("eng.skill", energy, ArenaText.Pair("技能", "Skills"), CampaignRoom.Energy, 2, FillSkills));
            tabs.Add(StudyTabOf("eng.infuse", energy, ArenaText.Pair("附魔", "Infusion"), CampaignRoom.Energy, 3, FillInfusion));
            // The void's tiles (the void currencies' part), one tab per category they name; none before the hidden boss is beaten.
            var voids = new List<StudyEntry>();
            CollectVoidStudy(voids);
            foreach (var entry in voids)
            {
                if (entry == null) continue;
                string category = string.IsNullOrEmpty(entry.category) ? RoomTitle(StudyRoom.Void) : entry.category;
                var tab = tabs.Find(t => t.key == "void." + category);
                if (tab == null) tabs.Add(tab = new StudyTab { key = "void." + category, roomName = RoomTitle(StudyRoom.Void), name = category });
                tab.entries.Add(entry);
            }
            if (Day >= rules.overloadDay)
            {
                var overload = new StudyTab { key = "ctl.overload", roomName = RoomName(CampaignRoom.Control), name = ArenaText.Pair("决战过载", "Final overload") };
                FillOverload(overload);
                tabs.Add(overload);
            }
            return tabs;
        }

        StudyTab StudyTabOf(string key, string roomName, string name, CampaignRoom room, int level, Action<StudyTab> fill)
        {
            var tab = new StudyTab { key = key, roomName = roomName, name = name };
            if (!RoomAt(room, level))
            {
                tab.locked = true;
                tab.name = StudyHidden;
                tab.lockedNote = ArenaText.Pair($"{RoomName(room)}升到 {level} 级后揭晓。", $"Shows once the {RoomName(room)} reaches level {level}.");
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
                detail = detail, costText = price is { } p && !owned ? PriceText(p) : "", state = state, buy = buy
            };
        }

        static StudyEntry StudyUnknown(string id, StudyRoom room, string category, string unlock) => new()
        {
            id = id, room = room, category = category, rarity = CardRarity.Common, title = StudyHidden, now = "", after = "",
            detail = unlock, costText = "", state = StudyState.Locked, buy = null
        };

        public static string PriceText(CampaignCost cost)
        {
            var parts = new List<string>();
            if (cost.alloy > 0) parts.Add(ArenaText.Pair($"合金 {cost.alloy}", $"{cost.alloy} alloy"));
            if (cost.energy > 0) parts.Add(ArenaText.Pair($"能量 {cost.energy}", $"{cost.energy} energy"));
            if (cost.dark > 0) parts.Add(ArenaText.Pair($"暗物质 {cost.dark}", $"{cost.dark} dark matter"));
            return parts.Count == 0 ? ArenaText.Pair("免费", "free") : string.Join(" + ", parts);
        }

        string StudyShortOf(CampaignCost cost)
        {
            var missing = new List<string>();
            if (Alloy < cost.alloy) missing.Add(ArenaText.Pair($"合金差 {cost.alloy - Alloy}", $"{cost.alloy - Alloy} alloy short"));
            if (Energy < cost.energy) missing.Add(ArenaText.Pair($"能量差 {cost.energy - Energy}", $"{cost.energy - Energy} energy short"));
            if (Dark < cost.dark) missing.Add(ArenaText.Pair($"暗物质差 {cost.dark - Dark}", $"{cost.dark - Dark} dark matter short"));
            return missing.Count > 0 ? string.Join(ArenaText.Pair("，", ", "), missing) : ArenaText.Pair("现在不能买", "Not possible now");
        }

        string RoomBroken(CampaignRoom room) => ArenaText.Pair($"{RoomName(room)}损坏：先修复", $"The {RoomName(room)} is broken: repair it first");

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

        // ───── Armament Bay ─────

        void FillUpgrades(StudyTab tab)
        {
            string category = tab.name;
            bool works = Works(CampaignRoom.Armament, 1);
            // The weapon Mk: one tile, the shared Mk (with sharedWeaponMk off, the weapon a purchase raises).
            int from = NextMkFrom;
            var target = rules.sharedWeaponMk ? null : MkTarget;
            var mkPrice = rules.MkPrice(from);
            string who = rules.sharedWeaponMk ? ArenaText.Pair("全部异常武器", "Every anomaly weapon") : target != null ? target.FormName : ArenaText.Pair("异常武器", "Anomaly weapon");
            string mkDetail = rules.sharedWeaponMk
                ? ArenaText.Pair("武装区的武器型号是全队共用的：买一次，手里的和以后拿到的异常武器都按这个型号算伤害。开局的小手枪不吃型号。",
                    "The Armament Bay's Mk is shared: buy it once and every anomaly weapon, held now or taken later, deals that Mk's damage. The starting pistol takes no Mk.")
                : ArenaText.Pair("每把武器单独升级：先升手里这把。开局的小手枪不能升级。", "Each weapon rises on its own, the one in hand first. The starting pistol cannot rise.");
            string milestones = string.Join(" / ", Array.ConvertAll(rules.weaponMkMilestones ?? Array.Empty<int>(), m => "Mk " + m));
            mkDetail += "\n" + ArenaText.Pair($"到 {milestones} 时，各多 {rules.mkMilestoneDamage * 100f:0}% 固定伤害和 {rules.mkMilestonePenetration * 100f:0}% 无视 Boss 属性免疫的穿透（累计）。现在的穿透：{rules.MkPenetration(from) * 100f:0}%。",
                $"At {milestones} each adds {rules.mkMilestoneDamage * 100f:0}% fixed damage and {rules.mkMilestonePenetration * 100f:0}% penetration through a boss's element immunity (summed). Penetration now: {rules.MkPenetration(from) * 100f:0}%.");
            if (mkPrice == null) mkDetail += "\n" + ArenaText.Pair($"合金只能升到 Mk {rules.MaxAlloyMk}。", $"Alloy buys up to Mk {rules.MaxAlloyMk}.");
            tab.entries.Add(StudyItem("mk", StudyRoom.Armament, category, StudyLadder(Mathf.Min(from + 1, rules.MaxAlloyMk), rules.MaxAlloyMk), StudyAxisIcon(UpgradeAxis.Power),
                ArenaText.Pair($"{who} · 型号", $"{who} · Mk"),
                $"Mk {from} ×{rules.MkDamage(from):0.##}", mkPrice != null ? $"Mk {from + 1} ×{rules.MkDamage(from + 1):0.##}" : "",
                mkDetail, mkPrice, mkPrice == null,
                !works ? RoomBroken(CampaignRoom.Armament) : !rules.sharedWeaponMk && target == null ? ArenaText.Pair("先研究一把异常武器", "Research an anomaly weapon first") : null,
                TryBuyWeaponDamage));
            // Armor: five levels, each one player level (max HP and max energy).
            var progression = gm.progression;
            var growth = progression ? progression.Rules : null;
            var armorPrice = PlayerLevelCost;
            int armor = ArmorLevel;
            float hpStep = growth ? growth.maxHpPerLevel : 0f, energyStep = growth ? growth.maxEnergyPerLevel : 0f;
            float hp = progression ? progression.Stats.maxHp : 0f, maxEnergy = progression ? progression.Stats.maxEnergy : 0f;
            tab.entries.Add(StudyItem("armor", StudyRoom.Armament, category, StudyLadder(Mathf.Min(armor + 1, rules.MaxArmorLevel), rules.MaxArmorLevel), art ? art.armor : null,
                ArenaText.Pair("装甲", "Armor"),
                ArenaText.Pair($"{armor} 级 · 生命 {hp:0}", $"Lv {armor} · HP {hp:0}"),
                armorPrice != null ? ArenaText.Pair($"{armor + 1} 级 · 生命 {hp + hpStep:0}", $"Lv {armor + 1} · HP {hp + hpStep:0}") : "",
                ArenaText.Pair($"装甲每升一级：最大生命 +{hpStep:0}、最大能量 +{energyStep:0}，并回满生命。共 {rules.MaxArmorLevel} 级。现在最大能量 {maxEnergy:0}。",
                    $"Each armor level: max HP +{hpStep:0} and max energy +{energyStep:0}, and full HP. {rules.MaxArmorLevel} levels in all. Max energy now {maxEnergy:0}."),
                armorPrice, armorPrice == null, works ? null : RoomBroken(CampaignRoom.Armament), TryBuyPlayerLevel));
        }

        void FillNewWeapons(StudyTab tab)
        {
            var weapons = gm.runWeapons;
            var roster = weapons ? weapons.roster : null;
            if (roster == null || roster.definitions == null) return;
            bool works = Works(CampaignRoom.Armament, 2);
            int newMk = rules.sharedWeaponMk ? teamMk : 1;
            var forms = new List<WeaponRoster.Definition>();
            foreach (var d in roster.definitions) if (d != null && !d.retired && RunWeapon.IsAnomalousForm(d.form)) forms.Add(d);
            forms.Sort((a, b) => a.unlockWave != b.unlockWave ? a.unlockWave.CompareTo(b.unlockWave) : a.form.CompareTo(b.form));
            foreach (var d in forms)
            {
                var form = d.form;
                string id = "weapon." + form;
                if (d.unlockWave > ResearchWave)
                {
                    tab.entries.Add(StudyUnknown(id, StudyRoom.Armament, tab.name, ArenaText.Pair("之后的日子才能研究。", "Can be researched on a later day.")));
                    continue;
                }
                bool owned = weapons.OwnsForm(form), auto = RunWeapon.IsAutoForm(form), rocket = form == WeaponForm.RocketLauncher;
                var price = rocket ? RocketCost : NewWeaponCost;
                string place;
                string blocked = !works ? RoomBroken(CampaignRoom.Armament) : null;
                if (rocket)
                {
                    var gone = RocketReplaces;
                    place = gone == null ? ArenaText.Pair("放进空着的手。", "It goes into the free hand.")
                        : ArenaText.Pair($"会换下{(gone.form == WeaponForm.Pistol ? "" : "没拿在手上的")}{gone.FormName}。", $"It replaces the {gone.FormName}{(gone.form == WeaponForm.Pistol ? "" : " not in hand")}.");
                }
                else
                {
                    int slot = SlotFor(form);
                    var replaced = !auto && slot >= 0 ? weapons.Hand[slot] : null;
                    place = slot < 0 ? ArenaText.Pair(auto ? "自动武器栏已满。" : "手持武器栏已满。", auto ? "Every auto slot is taken." : "Both hands are taken.")
                        : replaced != null ? ArenaText.Pair($"会换下{replaced.FormName}。", $"It replaces the {replaced.FormName}.")
                        : ArenaText.Pair(auto ? "放进空着的自动武器栏。" : "放进空着的手。", auto ? "It goes into a free auto slot." : "It goes into the free hand.");
                    if (slot < 0 && blocked == null) blocked = place;
                }
                string detail = ArenaText.Pair($"异常武器（{(auto ? "自动" : "手持")}）：攻击方式和特性买到才揭晓。到手就是 Mk {newMk}。", $"An anomaly weapon ({(auto ? "auto" : "hand")}): how it attacks and its trait show once bought. It arrives at Mk {newMk}.")
                    + "\n" + place;
                tab.entries.Add(StudyItem(id, StudyRoom.Armament, tab.name, CampaignRules.WeaponRarity(d.unlockWave), StudyWeaponIcon(form), ArenaTerms.WeaponForm(form),
                    owned ? ArenaText.Pair("已拥有", "Held") : ArenaText.Pair("未拥有", "Not held"), owned ? "" : $"Mk {newMk}",
                    detail, price, owned, blocked, () => TryBuyWeaponForm(form)));
            }
        }

        void FillTameTwist(StudyTab tab, UpgradeAxis axis)
        {
            var weapons = gm.runWeapons;
            if (!weapons) return;
            bool works = Works(CampaignRoom.Armament, axis == UpgradeAxis.Tame ? 2 : 3);
            var price = new CampaignCost(axis == UpgradeAxis.Tame ? rules.tameAlloy : rules.twistAlloy);
            string name = ArenaTerms.Upgrade(axis);
            for (int hand = 0; hand < weapons.Hand.Length; hand++)
            {
                var w = weapons.Hand[hand];
                if (w == null || !w.IsAnomalous) continue;
                bool owned = w.Has(axis);
                string text = (w.revealed ? weapons.UpgradeDescription(w, axis) : ArenaText.Pair("这把武器的异常还没揭晓。", "This weapon's anomaly is not known yet."))
                    + "\n" + TameTwistBonusText(axis); // its attack speed or damage besides (since 2026-11-10, V2Campaign.Days.cs)
                string blocked = !works ? RoomBroken(CampaignRoom.Armament) : !w.revealed ? ArenaText.Pair("先用它打一次，揭晓它的异常", "Fire it once to learn its anomaly first") : null;
                var weapon = w;
                tab.entries.Add(StudyItem($"{(axis == UpgradeAxis.Tame ? "tame" : "twist")}.{w.form}", StudyRoom.Armament, tab.name, ArenaRarity.ForWeaponCard(axis), StudyWeaponIcon(w.form),
                    w.Label, owned ? ArenaText.Pair($"已{name}", $"{name} done") : ArenaText.Pair($"未{name}", $"No {name}"), owned ? "" : ArenaText.Pair($"已{name}", $"{name} done"),
                    text, price, owned, blocked, () => TryTameOrTwist(weapon, axis)));
            }
            if (tab.entries.Count == 0) tab.emptyNote = ArenaText.Pair("手上没有异常武器。", "No anomaly weapon in hand.");
        }

        // ───── Energy Lab ─────

        /// <summary>The research tier tile at the head of a line's tab: its next tier, coloured by the rarest items it opens.</summary>
        void AddResearchTile(StudyTab tab, ResearchLine line)
        {
            int tier = ResearchTierShown(line);
            var price = rules.ResearchPrice(tier);
            CardRarity opens = CardRarity.Common;
            foreach (CardRarity r in new[] { CardRarity.Common, CardRarity.Rare, CardRarity.Exceptional, CardRarity.Epic, CardRarity.Legendary })
                if (rules.RarityTier(r) == tier + 1) opens = r;
            tab.entries.Add(StudyItem("tier." + line, StudyRoom.Energy, tab.name, price != null ? opens : CardRarity.Legendary, art ? art.energyLab : null,
                ResearchName(line),
                ArenaText.Pair($"{tier} 阶", $"Tier {tier}"), price != null ? ArenaText.Pair($"{tier + 1} 阶", $"Tier {tier + 1}") : "",
                ArenaText.Pair($"升阶以后解锁更稀有、更强的内容（共 {rules.MaxResearchTier} 阶）。", $"A higher tier unlocks rarer, stronger items ({rules.MaxResearchTier} tiers in all)."),
                price, price == null, Works(CampaignRoom.Energy, (int)line + 1) ? null : RoomBroken(CampaignRoom.Energy), () => TryBuyResearchTier(line)));
        }

        static readonly CardRarity[] StudyRarities = { CardRarity.Common, CardRarity.Rare, CardRarity.Exceptional, CardRarity.Epic, CardRarity.Legendary };

        string StudyTierNote(ResearchLine line, CardRarity rarity) => ArenaText.Pair($"{ResearchName(line)}升到 {rules.RarityTier(rarity)} 阶后揭晓。", $"Shows at {ResearchName(line)} tier {rules.RarityTier(rarity)}.");

        void FillBody(StudyTab tab)
        {
            AddResearchTile(tab, ResearchLine.Body);
            // Stat lines (since 2026-11-10): one tile per stat, its next step only (V2Campaign.Days.cs).
            if (rules.statLineMode == StatLineMode.Replace) { FillBodyLines(tab); return; }
            var progression = gm.progression;
            var rows = progression && progression.Rules ? progression.Rules.statCards : null;
            if (rows == null) return;
            int tier = ResearchTierShown(ResearchLine.Body);
            bool works = Works(CampaignRoom.Energy, 1);
            foreach (var rarity in StudyRarities)
                foreach (var row in rows)
                {
                    if (row == null || row.retired) continue;
                    var stat = row.stat;
                    string id = $"stat.{stat}.{rarity}";
                    if (rules.RarityTier(rarity) > tier) { tab.entries.Add(StudyUnknown(id, StudyRoom.Energy, tab.name, StudyTierNote(ResearchLine.Body, rarity))); continue; }
                    var offer = new StatCardOffer(stat, rarity, row.At(rarity), row.bonus, row.BonusAt(rarity));
                    var stats = progression.Stats;
                    var after = stats.Copy();
                    after.Add(stat, offer.Value);
                    if (offer.Bonus != StatBonus.None) after.AddBonus(offer.Bonus, offer.BonusValue);
                    bool owned = StatBought(stat, rarity);
                    string blocked = !works ? RoomBroken(CampaignRoom.Energy) : stats.AtCap(stat) ? ArenaText.Pair("这项已到上限", "This stat is at its cap") : null;
                    tab.entries.Add(StudyItem(id, StudyRoom.Energy, tab.name, rarity, StudyArt ? StudyIcon(StudyArt.statIcons, (int)stat) : null, offer.Title,
                        StudyStatValue(stat, stats), owned ? "" : StudyStatValue(stat, after),
                        offer.Description + "\n" + ArenaText.Pair($"{ArenaTerms.Rarity(rarity)}：每项每种稀有度只能买一次，买了立刻生效。", $"{ArenaTerms.Rarity(rarity)}: each stat at each rarity is bought once and works at once."),
                        new CampaignCost(0, rules.RarityEnergy(rarity)), owned, blocked, () => TryBuyStat(stat, rarity)));
                }
        }

        /// <summary>A stat as the study window shows it.</summary>
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
            AddResearchTile(tab, ResearchLine.Skills);
            var skills = gm.skills;
            if (!skills || !skills.Catalog || skills.Catalog.skills == null) return;
            int tier = ResearchTierShown(ResearchLine.Skills);
            bool works = Works(CampaignRoom.Energy, 2);
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
                    tab.entries.Add(StudyUnknown(key, StudyRoom.Energy, tab.name, ArenaText.Pair($"{ResearchName(ResearchLine.Skills)}升到 {rules.SkillCategoryTier(definition.category)} 阶后揭晓。", $"Shows at {ResearchName(ResearchLine.Skills)} tier {rules.SkillCategoryTier(definition.category)}.")));
                    continue;
                }
                if (!top && rules.RarityTier(rarity) > tier) { tab.entries.Add(StudyUnknown(key, StudyRoom.Energy, tab.name, StudyTierNote(ResearchLine.Skills, rarity))); continue; }
                string blocked = !works ? RoomBroken(CampaignRoom.Energy)
                    : !learned && skills.OwnedCount >= skills.Slots.Length ? ArenaText.Pair($"技能栏已满（{skills.Slots.Length} 个）", $"Every skill slot is taken ({skills.Slots.Length})") : null;
                int shown = Mathf.Min(next, SkillDefinition.MaxLevel);
                string detail = definition.Description(shown) + "\n" + ArenaText.Pair($"消耗 {definition.cost * costScale:0} 能量 · 冷却 {definition.Cooldown(shown) * cooldownScale:0.#} 秒",
                    $"{definition.cost * costScale:0} energy · {definition.Cooldown(shown) * cooldownScale:0.#} s cooldown");
                tab.entries.Add(StudyItem(key, StudyRoom.Energy, tab.name, rarity, StudyArt ? StudyIcon(StudyArt.skillIcons, (int)id) : art ? art.skill : null, ArenaTerms.Skill(id),
                    learned ? ArenaText.Pair($"等级 {next - 1}", $"Level {next - 1}") : ArenaText.Pair("未学", "Not learned"),
                    top ? "" : ArenaText.Pair($"等级 {next}", $"Level {next}"),
                    detail, top ? null : new CampaignCost(0, rules.RarityEnergy(rarity)), top, blocked, () => TryBuySkill(id)));
            }
        }

        void FillInfusion(StudyTab tab)
        {
            AddResearchTile(tab, ResearchLine.Infusion);
            var weapons = gm.runWeapons;
            if (!weapons || rules.infusionChoices == null) return;
            int tier = ResearchTierShown(ResearchLine.Infusion);
            bool works = Works(CampaignRoom.Energy, 3);
            int before = tab.entries.Count;
            float floor = rules.InfusionFloor(Mathf.Max(1, Day), tier, TyrantFight);
            // Since 2026-11-10 the floor follows a table by day (CampaignRules.infusionFloorByDay); the linear rule only without it.
            float perTier = rules.InfusionFloor(Mathf.Max(1, Day), 1, TyrantFight);
            string floorNote = rules.infusionFloorByDay is { Length: > 0 }
                ? ArenaText.Pair($"附魔保底：你造成的燃烧和中毒每秒伤害不低于 {floor:0}（今天每阶 {perTier:0.#} × 附魔研究阶数，每天不同）。",
                    $"Infusion floor: your burns and poisons deal at least {floor:0} a second (today {perTier:0.#} per infusion tier; it changes each day).")
                : ArenaText.Pair($"附魔保底：你造成的燃烧和中毒每秒伤害不低于 {floor:0}（{rules.infusionFloorPerDay:0} × 天数 × 附魔研究阶数）。",
                    $"Infusion floor: your burns and poisons deal at least {floor:0} a second ({rules.infusionFloorPerDay:0} × day × infusion tier).");
            var held = new List<RunWeapon>();
            foreach (var w in weapons.Hand) if (w is { IsAnomalous: true, builtInElement: false }) held.Add(w);
            foreach (var w in weapons.Auto) if (w is { IsAnomalous: true, builtInElement: false }) held.Add(w);
            foreach (var w in held)
            {
                var weapon = w;
                var own = weapons.ElementOf(w);
                foreach (var choice in rules.infusionChoices)
                {
                    var element = choice.element;
                    if (element is ElementKind.None or ElementKind.Void) continue;
                    string id = $"infuse.{w.form}.{element}";
                    if (rules.RarityTier(choice.rarity) > tier) { tab.entries.Add(StudyUnknown(id, StudyRoom.Energy, tab.name, StudyTierNote(ResearchLine.Infusion, choice.rarity))); continue; }
                    bool owned = own == element;
                    string rule = ArenaTerms.ElementRule(element);
                    string detail = (string.IsNullOrEmpty(rule) ? "" : rule + "\n")
                        + (own != ElementKind.None && !owned ? ArenaText.Pair($"会替换现有的{ArenaTerms.Element(own)}。", $"It replaces its {ArenaTerms.Element(own)}.") + "\n" : "") + floorNote;
                    tab.entries.Add(StudyItem(id, StudyRoom.Energy, tab.name, choice.rarity, StudyElementIcon(element),
                        ArenaText.Pair($"{w.FormName} · {ArenaTerms.Element(element)}", $"{w.FormName} · {ArenaTerms.Element(element)}"),
                        ArenaTerms.Element(own), owned ? "" : ArenaTerms.Element(element),
                        detail, new CampaignCost(0, rules.RarityEnergy(choice.rarity)), owned, works ? null : RoomBroken(CampaignRoom.Energy), () => TryInfuse(weapon, element)));
                }
            }
            if (tab.entries.Count == before) tab.emptyNote = ArenaText.Pair("没有能附魔的异常武器（自带元素的武器不能附魔）。", "No anomaly weapon can take an infusion (weapons with a built-in element cannot).");
        }

        // ───── Central Control: the final overload ─────

        void FillOverload(StudyTab tab)
        {
            string blocked = !ControlWorks ? RoomBroken(CampaignRoom.Control) : null;
            foreach (bool shield in new[] { true, false })
                foreach (bool withEnergy in new[] { false, true })
                {
                    int bought = shield ? overloadShieldBuys : overloadDamageBuys;
                    float share = shield ? rules.overloadShieldShare : rules.overloadDamageShare;
                    bool full = bought >= rules.overloadMaxBuys;
                    var price = withEnergy ? new CampaignCost(0, rules.overloadEnergy) : new CampaignCost(rules.overloadAlloy);
                    bool s = shield, e = withEnergy;
                    // StudyRoom has no Central Control: these tiles carry Armament, and the window groups them under the tab's own room name.
                    tab.entries.Add(StudyItem($"overload.{(shield ? "shield" : "damage")}.{(withEnergy ? "energy" : "alloy")}", StudyRoom.Armament, tab.name, CardRarity.Legendary,
                        art ? shield ? art.shield : art.durability : null,
                        shield ? ArenaText.Pair("暴君战开局护盾", "Tyrant fight: opening shield") : ArenaText.Pair("暴君战最终伤害", "Tyrant fight: final damage"),
                        $"+{bought * share * 100f:0}%", full ? "" : $"+{(bought + 1) * share * 100f:0}%",
                        (shield ? ArenaText.Pair($"暴君战开始时，你获得最大生命 {share * 100f:0}% 的护盾（每次），超过平时的护盾上限；站几秒后会像平常的护盾一样慢慢消退。",
                                $"When the Tyrant's fight begins you get shield worth {share * 100f:0}% of max HP per purchase, above the usual cap; it fades like any shield after a few seconds.")
                            : ArenaText.Pair($"暴虐君王战中，武器和技能的伤害每次 +{share * 100f:0}%（累计）。", $"In the Tyrant's fight, weapon and skill damage +{share * 100f:0}% per purchase (summed)."))
                        + "\n" + ArenaText.Pair($"护盾和伤害各最多换 {rules.overloadMaxBuys} 次，合金和能量都算。", $"Up to {rules.overloadMaxBuys} of each, alloy and energy alike."),
                        price, full, blocked, () => TryBuyOverload(s, e)));
                }
        }
    }
}
