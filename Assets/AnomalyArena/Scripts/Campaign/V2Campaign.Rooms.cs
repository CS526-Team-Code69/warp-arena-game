using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The rooms' levels at work (V2 doc section E, 2026-11-10): what a level gives besides what it unlocks, the weapon Mk the Armament
    /// Bay shares among every anomaly weapon, the Energy Lab's three research lines and what they sell item by item (each bought outright
    /// and in effect at once: no draw), and the last day's overload for the Tyrant's fight. Numbers: CampaignRules.Rooms.cs.
    /// A room's passive effect lasts while the room works (built to that level and not broken), like what it unlocks.
    /// The other parts of the game read these through static hooks set at the start of a run (RunWeapons, SkillLoadout, ElementStatus,
    /// PlayerController); each hook gives the neutral value outside a running campaign, so the open mode's 15 waves are untouched.
    /// </summary>
    public sealed partial class V2Campaign
    {
        /// <summary>The Energy Lab's three research lines, opened by its levels 1, 2 and 3.</summary>
        public enum ResearchLine { Body, Skills, Infusion }

        int teamMk = 1;
        // The first day whose afternoon gets the Ship Launch Bay's extra alloy: the day after its level 1 was built.
        int shipOreFromDay = int.MaxValue;
        // Research tiers bought past tier 1, per line.
        readonly int[] researchBought = new int[3];
        // Body items bought: each stat at each rarity once.
        readonly HashSet<(StatId stat, CardRarity rarity)> statsBought = new();
        int overloadShieldBuys, overloadDamageBuys;
        bool overloadShieldGiven;

        void ResetRooms()
        {
            teamMk = 1;
            shipOreFromDay = int.MaxValue;
            System.Array.Clear(researchBought, 0, researchBought.Length);
            statsBought.Clear();
            overloadShieldBuys = overloadDamageBuys = 0;
            overloadShieldGiven = false;
            afternoonOreScale = lampRadiusScale = 1f;
            InstallRoomHooks();
        }

        /// <summary>This campaign is the one running: the hooks give its rooms' values, otherwise the neutral ones.</summary>
        bool RoomsLive => this && Running && Active == this && rules;

        bool RoomAt(CampaignRoom room, int level) => rooms[(int)room].level >= level;

        // ───── What the levels give besides their unlocks ─────

        void OnRoomLevelBought(CampaignRoom room)
        {
            // Ship Launch Bay level 1 (the engine's survey radar): more alloy each afternoon, from the next day on.
            if (room == CampaignRoom.Ship && rooms[(int)room].level == 1) shipOreFromDay = Day + 1;
        }

        /// <summary>Ship Launch Bay level 2: the share of a raid strike the base still takes.</summary>
        float RaidDamageFactor => ShipWorks(2) ? 1f - rules.shipBaseDamageCut : 1f;

        /// <summary>Every frame of a run (TickBaseVisuals): the effects other parts read (StudyEntry.cs: afternoonOreScale for the afternoon's
        /// ore, lampRadiusScale for the night's lamps), the held weapons' Mk labels, and the overload's shield when the Tyrant's fight begins.</summary>
        void TickRooms()
        {
            afternoonOreScale = ShipWorks(1) && Day >= shipOreFromDay ? 1f + rules.shipOreBonus : 1f;
            lampRadiusScale = ShipWorks(2) ? 1f + rules.shipLampRadiusBonus : 1f;
            SyncWeaponMk();
            if (Stage == CampaignStage.FinalBoss && !overloadShieldGiven) GiveOverloadShield();
        }

        // ───── Hooks into the fight ─────

        void InstallRoomHooks()
        {
            RunWeapons.MkDamageRule = w => !RoomsLive ? 0f : w != null && w.IsAnomalous ? rules.MkDamage(EffectiveMk(w)) : 1f;
            RunWeapons.NewWeaponMk = () => !RoomsLive ? 0 : rules.sharedWeaponMk ? teamMk : 1;
            RunWeapons.DamageBonus = () => RoomsLive ? FinalDamageScale : 1f;
            SkillLoadout.CooldownScale = () => RoomsLive && ShipWorks(3) ? 1f - rules.shipSkillCooldownCut : 1f;
            SkillLoadout.CostScale = () => RoomsLive && ShipWorks(3) ? 1f - rules.shipSkillCostCut : 1f;
            SkillLoadout.DamageScale = () => RoomsLive ? SkillDayScale * FinalDamageScale : 1f;
            ElementStatus.ExtraPenetration = (target, source) =>
                RoomsLive && target is Enemy { IsBoss: true } && source && gm && source == gm.player ? rules.MkPenetration(PenetrationMk) : 0f;
            ElementStatus.StatusDamageFloor = source => RoomsLive && source && gm && source == gm.player ? InfusionFloorNow : 0f;
            PlayerController.HitShare = (player, amount) => RoomsLive ? GuardPlayer(player, amount) : amount;
        }

        /// <summary>Energy Lab level 2: skill damage grows with the days.</summary>
        float SkillDayScale => Works(CampaignRoom.Energy, 2) ? rules.SkillDamageScale(Day) : 1f;
        /// <summary>Energy Lab level 3: the least a burn or poison of the player's deals a second (since 2026-11-10 the day's
        /// infusionFloorByDay x infusion tier, tyrantInfusionFloor x tier in the Tyrant's fight).</summary>
        float InfusionFloorNow => Works(CampaignRoom.Energy, 3) ? rules.InfusionFloor(Day, ResearchTier(ResearchLine.Infusion), TyrantFight) : 0f;
        /// <summary>The overload's extra damage, in the Tyrant's fight only.</summary>
        float FinalDamageScale => Stage == CampaignStage.FinalBoss ? 1f + rules.overloadDamageShare * overloadDamageBuys : 1f;

        /// <summary>Central Control's shield guards a player near the base: while it has shield left, it takes shieldGuardShare of each hit
        /// on a player within shieldGuardRadius cells of the base's walls. Returns what is left for the player.</summary>
        float GuardPlayer(PlayerController player, float amount)
        {
            if (!player || amount <= 0f || ShieldLevel <= 0 || BaseShield <= 0f || BaseDestroyed) return amount;
            Vector3 d = Query.Flat(player.Position - BaseCentre);
            float outX = Mathf.Max(0f, Mathf.Abs(d.x) - BaseHalf), outZ = Mathf.Max(0f, Mathf.Abs(d.z) - BaseHalf);
            if (outX * outX + outZ * outZ > rules.shieldGuardRadius * rules.shieldGuardRadius) return amount;
            float taken = rules.ShieldGuardTakes(amount, BaseShield);
            if (taken <= 0f) return amount;
            BaseShield -= taken;
            // The shield's hit where it caught the blow (CampaignAudio plays the break itself when this emptied it).
            if (BaseShield > .01f) CampaignAudio.PlayAt(CampaignSfx.ShieldHit, player.Position);
            return amount - taken;
        }

        // ───── The shared weapon Mk ─────

        /// <summary>The Armament Bay's weapon Mk, shared by every anomaly weapon held now or taken later (CampaignRules.sharedWeaponMk on,
        /// the rule since 2026-11-10). 1 to CampaignRules.MaxWeaponMk; alloy buys up to Mk 10 (TryBuyWeaponDamage).</summary>
        public int TeamMk => teamMk;

        /// <summary>Sets the shared Mk (clamped to 1 .. CampaignRules.MaxWeaponMk, so 11 is the void Mk) and brings every anomaly weapon held to
        /// it at once. For the void alloy's purchase of Mk 11 and for checks; it spends nothing and records nothing itself.</summary>
        public void SetTeamMk(int mk)
        {
            teamMk = Mathf.Clamp(mk, 1, rules ? rules.MaxWeaponMk : 1);
            SyncWeaponMk();
        }

        /// <summary>The Mk a weapon fights at: the shared Mk, or with sharedWeaponMk off its own.</summary>
        int EffectiveMk(RunWeapon w) => rules.sharedWeaponMk ? teamMk : Mathf.Max(1, w.mk);

        /// <summary>With sharedWeaponMk off: the weapon a Mk purchase raises (the anomaly weapon in hand, else the first other one held).</summary>
        RunWeapon MkTarget
        {
            get
            {
                var weapons = gm ? gm.runWeapons : null;
                if (!weapons) return null;
                if (weapons.Active is { IsAnomalous: true } active) return active;
                foreach (var w in weapons.Hand) if (w is { IsAnomalous: true }) return w;
                foreach (var w in weapons.Auto) if (w is { IsAnomalous: true }) return w;
                return null;
            }
        }

        /// <summary>The Mk the next purchase raises from.</summary>
        int NextMkFrom => rules.sharedWeaponMk ? teamMk : Mathf.Max(1, MkTarget?.mk ?? 1);
        /// <summary>The Mk whose milestones give the boss penetration: the shared Mk, or the anomaly weapon in hand's (the hit itself does not
        /// say which weapon dealt it).</summary>
        int PenetrationMk => rules.sharedWeaponMk ? teamMk : gm && gm.runWeapons && gm.runWeapons.Active is { IsAnomalous: true } w ? w.mk : 1;

        /// <summary>The shared Mk on every anomaly weapon held, so a weapon's label and card show the Mk it fights at (a weapon taken from a
        /// crate or bought arrives at it through RunWeapons.NewWeaponMk; this keeps the held ones in step).</summary>
        void SyncWeaponMk()
        {
            if (!rules || !rules.sharedWeaponMk || !gm || !gm.runWeapons) return;
            foreach (var w in gm.runWeapons.Hand) if (w is { IsAnomalous: true }) w.mk = teamMk;
            foreach (var w in gm.runWeapons.Auto) if (w is { IsAnomalous: true }) w.mk = teamMk;
        }

        // ───── Energy Lab research ─────

        /// <summary>A research line's tier: 0 until its Energy Lab level works, then 1 plus the tiers bought.</summary>
        public int ResearchTier(ResearchLine line) => Works(CampaignRoom.Energy, (int)line + 1) ? 1 + researchBought[(int)line] : 0;
        /// <summary>The tier a line has reached, broken room or not (what the study window shows).</summary>
        int ResearchTierShown(ResearchLine line) => RoomAt(CampaignRoom.Energy, (int)line + 1) ? 1 + researchBought[(int)line] : 0;

        public bool TryBuyResearchTier(ResearchLine line)
        {
            int tier = ResearchTier(line);
            var price = tier > 0 ? rules.ResearchPrice(tier) : null;
            if (price == null || !TrySpend(price.Value)) return false;
            researchBought[(int)line]++;
            Bought("research_tier", $"{line} tier {tier + 1}", CampaignRoom.Energy, price.Value);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            ui.Flash(ArenaText.Pair($"{ResearchName(line)}升到了 {tier + 1} 阶，更稀有的项目已解锁", $"{ResearchName(line)} is now tier {tier + 1}. Rarer items are unlocked."));
            return true;
        }

        public static string ResearchName(ResearchLine line) => line switch
        {
            ResearchLine.Body => ArenaText.Pair("体质研究", "Body research"),
            ResearchLine.Skills => ArenaText.Pair("技能研究", "Skill research"),
            _ => ArenaText.Pair("附魔研究", "Infusion research")
        };

        /// <summary>The stat row of the progression rules that a body item raises (retired rows never sell).</summary>
        StatCardValues StatRow(StatId stat)
        {
            var rows = gm && gm.progression && gm.progression.Rules ? gm.progression.Rules.statCards : null;
            if (rows == null) return null;
            foreach (var row in rows) if (row != null && !row.retired && row.stat == stat) return row;
            return null;
        }

        /// <summary>One body item: a stat at a rarity, each pair once; the rarity needs its body research tier. With stat lines (the rule
        /// since 2026-11-10, CampaignRules.statLineMode) only the line's next step sells and replaces the one below (V2Campaign.Days.cs).</summary>
        public bool TryBuyStat(StatId stat, CardRarity rarity)
        {
            if (rules.statLineMode == StatLineMode.Replace) return TryBuyStatLine(stat, rarity);
            var row = StatRow(stat);
            if (row == null || !Works(CampaignRoom.Energy, 1) || rules.RarityTier(rarity) > ResearchTier(ResearchLine.Body)
                || statsBought.Contains((stat, rarity)) || gm.progression.Stats.AtCap(stat)) return false;
            var price = new CampaignCost(0, rules.RarityEnergy(rarity));
            if (!TrySpend(price)) return false;
            var offer = new StatCardOffer(stat, rarity, row.At(rarity), row.bonus, row.BonusAt(rarity));
            gm.progression.ApplyStat(offer);
            statsBought.Add((stat, rarity));
            Bought("constitution", $"{stat} {rarity}", CampaignRoom.Energy, price);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            ui.Flash(ArenaText.Pair($"体质：{offer.Title}（{ArenaTerms.Rarity(rarity)}）", $"Body: {offer.Title} ({ArenaTerms.Rarity(rarity)})"));
            return true;
        }

        bool StatBought(StatId stat, CardRarity rarity) => statsBought.Contains((stat, rarity));

        /// <summary>The level a skill purchase gives: the next level of one learned, or level 1 of a new one.</summary>
        int NextSkillLevel(SkillId id, out bool learned)
        {
            learned = false;
            var skills = gm ? gm.skills : null;
            if (!skills) return 1;
            foreach (var slot in skills.Slots)
                if (!slot.Empty && slot.Id == id) { learned = true; return slot.Level + 1; }
            return 1;
        }

        /// <summary>One skill item: a new skill at level 1 (its category needs its skill research tier and a free skill slot), or the next
        /// level of one learned (the level's rarity needs its tier).</summary>
        public bool TryBuySkill(SkillId id)
        {
            var skills = gm ? gm.skills : null;
            var definition = skills && skills.Catalog ? skills.Catalog.Find(id) : null;
            if (!definition || definition.greaterVoid || !Works(CampaignRoom.Energy, 2)) return false;
            int next = NextSkillLevel(id, out bool learned);
            int tier = ResearchTier(ResearchLine.Skills);
            var rarity = ArenaRarity.ForSkill(definition, next);
            if (next > SkillDefinition.MaxLevel || rules.RarityTier(rarity) > tier || !learned && (rules.SkillCategoryTier(definition.category) > tier || skills.OwnedCount >= skills.Slots.Length)) return false;
            var price = new CampaignCost(0, rules.RarityEnergy(rarity));
            if (!CanAfford(price)) { TrySpend(price); return false; }
            if (!skills.Learn(id, -1, next)) return false;
            TrySpend(price);
            Bought("unlock_skill", $"{id} level {next}", CampaignRoom.Energy, price);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            ui.Flash(learned ? ArenaText.Pair($"技能：{ArenaTerms.Skill(id)}升到 {next} 级", $"Skill: {ArenaTerms.Skill(id)} is now level {next}")
                : ArenaText.Pair($"学会了新技能：{ArenaTerms.Skill(id)}", $"New skill learned: {ArenaTerms.Skill(id)}"));
            return true;
        }

        /// <summary>The rarity of an infusion element, or null when infusion research does not sell it.</summary>
        CardRarity? InfusionRarity(ElementKind element)
        {
            if (rules.infusionChoices == null) return null;
            foreach (var choice in rules.infusionChoices) if (choice.element == element) return choice.rarity;
            return null;
        }

        /// <summary>One infusion item: an element on one held anomaly weapon without a built-in element (it replaces the element it has).</summary>
        public bool TryInfuse(RunWeapon weapon, ElementKind element)
        {
            var weapons = gm ? gm.runWeapons : null;
            var rarity = InfusionRarity(element);
            if (!weapons || weapon == null || rarity == null || !Works(CampaignRoom.Energy, 3) || !weapons.Holds(weapon) || !weapon.IsAnomalous
                || weapon.builtInElement || weapons.ElementOf(weapon) == element || rules.RarityTier(rarity.Value) > ResearchTier(ResearchLine.Infusion)) return false;
            var price = new CampaignCost(0, rules.RarityEnergy(rarity.Value));
            if (!TrySpend(price)) return false;
            weapons.Infuse(weapon, element);
            Bought("weapon_infusion", $"{weapon.form} {element}", CampaignRoom.Energy, price);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            ui.Flash(ArenaText.Pair($"{weapon.FormName}附上了{ArenaTerms.Element(element)}属性", $"Your {weapon.FormName} is now infused with {ArenaTerms.Element(element)}"));
            return true;
        }

        // ───── Armament Bay: Tame, Twist, new weapons ─────

        /// <summary>Tame (Armament Bay level 2) or Twist (level 3) on one held anomaly hand weapon, once each, at a fixed price per weapon.
        /// The weapon must have shown its anomaly (fired once), as before for these upgrades.</summary>
        public bool TryTameOrTwist(RunWeapon weapon, UpgradeAxis axis)
        {
            var weapons = gm ? gm.runWeapons : null;
            if (axis is not (UpgradeAxis.Tame or UpgradeAxis.Twist) || !weapons || weapon == null || !Works(CampaignRoom.Armament, axis == UpgradeAxis.Tame ? 2 : 3)
                || System.Array.IndexOf(weapons.Hand, weapon) < 0 || !weapon.IsAnomalous || !weapon.revealed || weapon.Has(axis)) return false;
            var price = new CampaignCost(axis == UpgradeAxis.Tame ? rules.tameAlloy : rules.twistAlloy);
            if (!TrySpend(price)) return false;
            weapon.axes |= axis;
            Bought(axis == UpgradeAxis.Tame ? "weapon_tame" : "weapon_twist", weapon.form.ToString(), CampaignRoom.Armament, price);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            ui.Flash(ArenaText.Pair($"{weapon.FormName}的{ArenaTerms.Upgrade(axis)}已完成", $"{ArenaTerms.Upgrade(axis)} added to the {weapon.FormName}"));
            return true;
        }

        /// <summary>The 1-based wave whose weapons the Armament Bay can research now (as its random research used: the day's wave + 1).</summary>
        int ResearchWave => gm && gm.waves ? gm.waves.WaveIndex + 2 : 1;

        /// <summary>Where a new weapon of this form would go: a free hand or auto slot, or for a hand weapon the starting pistol's hand when
        /// both hands are taken; -1 when there is no room.</summary>
        int SlotFor(WeaponForm form)
        {
            var weapons = gm ? gm.runWeapons : null;
            if (!weapons) return -1;
            if (RunWeapon.IsAutoForm(form)) return System.Array.FindIndex(weapons.Auto, w => w == null);
            int free = System.Array.FindIndex(weapons.Hand, w => w == null);
            return free >= 0 ? free : PistolHandIfFull(weapons);
        }

        /// <summary>One new weapon of a chosen form (Armament Bay level 2, the research price that rises with each weapon bought). The rocket
        /// launcher keeps its own price and place (TryBuyRocketLauncher). It arrives at the shared Mk and shows its weapon card.</summary>
        public bool TryBuyWeaponForm(WeaponForm form)
        {
            if (form == WeaponForm.RocketLauncher) return TryBuyRocketLauncher();
            var weapons = gm ? gm.runWeapons : null;
            var definition = weapons && weapons.roster ? weapons.roster.Get(form) : null;
            if (!NewWeaponForSale || definition == null || definition.retired || !RunWeapon.IsAnomalousForm(form) || definition.unlockWave > ResearchWave
                || weapons.OwnsForm(form)) return false;
            int slot = SlotFor(form);
            if (slot < 0) { ui.Flash(ArenaText.Pair("武器栏都满了，放不下这把武器", "There's no free slot for this weapon")); return false; }
            var price = NewWeaponCost;
            if (!TrySpend(price)) return false;
            bool auto = RunWeapon.IsAutoForm(form);
            var old = auto ? weapons.GiveToAuto(slot, form) : weapons.GiveToHand(slot, form);
            var weapon = auto ? weapons.Auto[slot] : weapons.Hand[slot];
            newWeaponsBought++;
            SyncWeaponMk();
            Bought("new_weapon", $"{form} Mk{weapon.mk} {(auto ? "auto" : "hand")}{(old != null ? ", replaces " + old.form : "")}", CampaignRoom.Armament, price);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            AnnounceWeapon(weapon, old != null
                ? ArenaText.Pair($"研究出了新的异常武器：{weapon.FormName}（{weapon.MkLabel}），换下了{old.FormName}", $"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel}), in place of the {old.FormName}")
                : ArenaText.Pair($"研究出了新的异常武器：{weapon.FormName}（{weapon.MkLabel}）", $"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel})"));
            return true;
        }

        // ───── The final overload (Central Control, last day) ─────

        /// <summary>From CampaignRules.overloadDay with Central Control working, leftover alloy or energy turns into the Tyrant fight's
        /// opening shield or its extra damage.</summary>
        public bool OverloadOpen => Running && Day >= rules.overloadDay && ControlWorks && Stage == CampaignStage.Base;
        public int OverloadShieldBuys => overloadShieldBuys;
        public int OverloadDamageBuys => overloadDamageBuys;

        public bool TryBuyOverload(bool shield, bool withEnergy)
        {
            ref int bought = ref shield ? ref overloadShieldBuys : ref overloadDamageBuys;
            if (!OverloadOpen || bought >= rules.overloadMaxBuys) return false;
            var price = withEnergy ? new CampaignCost(0, rules.overloadEnergy) : new CampaignCost(rules.overloadAlloy);
            if (!TrySpend(price)) return false;
            bought++;
            Bought("overload", $"{(shield ? "shield" : "damage")} {bought}", CampaignRoom.Control, price);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShield, BaseCentre);
            ui.Flash(shield ? ArenaText.Pair($"决战过载：暴君战开局护盾 +{bought * rules.overloadShieldShare * 100f:0}%", $"Final overload: the Tyrant's fight opens with +{bought * rules.overloadShieldShare * 100f:0}% shield")
                : ArenaText.Pair($"决战过载：暴君战最终伤害 +{bought * rules.overloadDamageShare * 100f:0}%", $"Final overload: +{bought * rules.overloadDamageShare * 100f:0}% damage in the Tyrant's fight"));
            return true;
        }

        /// <summary>The Tyrant's fight has begun: the overload's shield goes on the player, above the usual shield cap (it still wears off as
        /// any shield does once it has stood a few seconds).</summary>
        void GiveOverloadShield()
        {
            overloadShieldGiven = true;
            var player = gm ? gm.player : null;
            var status = player ? ElementStatus.For(player) : null;
            if (!status || overloadShieldBuys <= 0) return;
            status.SetShield(status.Shield + player.maxHp * rules.overloadShieldShare * overloadShieldBuys);
        }
    }
}
