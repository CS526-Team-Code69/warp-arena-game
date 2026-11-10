using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The void currencies (2026-11-10, the implementation spec's section D). Beating the Nash Prophet in the void trial behind the black
    /// hole's gate gives the gift of the void: rules.voidEnergyReward void energy and rules.voidAlloyReward void alloy (instead of the
    /// classic run's extra level and greater void pick), and from the next afternoon on rules.voidOreNodes nodes of void alloy grow
    /// each afternoon round where the hole stood (AfternoonVoidOre). Both currencies show on screen from the first one gained.
    /// They buy, in the study window's Void room (CollectVoidStudy; the window itself is another part's): a void skill for
    /// rules.voidSkillCost void energy (Pocket Void, Void Walk or Singularity, no levels); a weapon's void enchant (its hits restore
    /// energy as the Void element's do, on top of its own element); the void Mk (the armament's Mk from rules.voidMkFrom to the next,
    /// for every weapon, the mark turning black); and the armor's void enchant (energy restores over time, the progression's own
    /// void energy regeneration).
    /// </summary>
    public sealed partial class V2Campaign
    {
        int voidEnergy, voidAlloy;
        bool voidWalletSeen, prophetBeaten, voidArmor;
        Vector3? voidOreCentre;
        // Weapons with the void enchant, and the bodies whose damage is watched for their hits (VoidWeaponHit).
        readonly HashSet<RunWeapon> voidWeapons = new();
        readonly HashSet<Combatant> drainWatched = new();
        readonly Dictionary<Texture2D, Sprite> voidIcons = new();

        public int VoidEnergy => voidEnergy;
        public int VoidAlloy => voidAlloy;
        /// <summary>The Nash Prophet has been beaten in this run (the Void room opens, the void alloy grows).</summary>
        public bool ProphetBeaten => prophetBeaten;
        public bool VoidArmor => voidArmor;
        public bool IsVoidEnchanted(RunWeapon weapon) => weapon != null && voidWeapons.Contains(weapon);

        /// <summary>ResetEconomy (a new run): no void currency, nothing void bought.</summary>
        void ResetVoid()
        {
            voidEnergy = voidAlloy = 0;
            voidWalletSeen = prophetBeaten = voidArmor = false;
            voidOreCentre = null;
            voidWeapons.Clear();
            StopVoidDrain();
        }

        /// <summary>The Nash Prophet fell (V2Campaign.Hole.cs, OnVoidTrialWon): the gift of the void, and void alloy where the hole stood.</summary>
        void OnProphetBeaten(Vector3 holeAt)
        {
            if (prophetBeaten) return;
            prophetBeaten = true;
            CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneProphet, "", Day, Day);
            voidOreCentre = Query.Flat(holeAt);
            GainVoid(rules.voidEnergyReward, rules.voidAlloyReward);
            // The gift shows on the return's banner (GameManagerFlow.BeginTrialReturn); no second line over it.
        }

        void GainVoid(int energyAmount, int alloyAmount)
        {
            voidEnergy += Mathf.Max(0, energyAmount);
            voidAlloy += Mathf.Max(0, alloyAmount);
            if (energyAmount > 0 || alloyAmount > 0) voidWalletSeen = true;
        }

        bool TrySpendVoid(int energyCost, int alloyCost)
        {
            if (voidEnergy < energyCost || voidAlloy < alloyCost) { V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel); return false; }
            voidEnergy -= Mathf.Max(0, energyCost);
            voidAlloy -= Mathf.Max(0, alloyCost);
            V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);
            return true;
        }

        /// <summary>For automated checks: void currency as if earned.</summary>
        public void DebugGrantVoid(int energyAmount, int alloyAmount) => GainVoid(energyAmount, alloyAmount);

        /// <summary>Every frame while the campaign's map is in place (TickHoleQuest): the wallet on screen and the enchanted weapons' watch.</summary>
        void TickVoid()
        {
            if (ui) ui.ShowVoidWallet(voidWalletSeen, voidEnergy, voidAlloy);
            WatchVoidHits();
        }

        // ───── The void alloy round where the hole stood ─────

        /// <summary>Each afternoon once the Prophet is beaten (BeginDusk): rules.voidOreNodes nodes of rules.voidOreYield void alloy round
        /// where the black hole stood, on open ground within rules.voidOreReach cells. Like the day's ore, what is not mined is gone at dawn.</summary>
        partial void AfternoonVoidOre()
        {
            if (!prophetBeaten || voidOreCentre is not Vector3 centre || rules.voidOreNodes <= 0) return;
            float turn = Random.value * 360f, reach = Mathf.Max(2f, rules.voidOreReach);
            for (int i = 0; i < rules.voidOreNodes; i++)
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    float a = (turn + 360f * i / rules.voidOreNodes + attempt * 23f) * Mathf.Deg2Rad;
                    Vector3 at = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(reach * .4f, reach);
                    if (!OreSpotOk(at, 1f)) at = FindOpen(at, 1f, 4f);
                    if (!OreSpotOk(at, 1f)) continue;
                    AddNode(NodeKind.VoidAlloy, at, rules.voidOreYield);
                    break;
                }
        }

        /// <summary>A void alloy node mined (CompleteMining).</summary>
        void MinedVoidAlloy(int amount)
        {
            GainVoid(0, amount);
            // The ore's sound (CampaignAudio hears the other ores through the alloy earned, which void alloy does not change).
            CampaignAudio.PlayAt(CampaignSfx.OrePickupE, PlayerPoint());
        }

        // ───── The weapons' void enchant: their hits restore energy ─────

        /// <summary>While any weapon carries the void enchant, every enemy-side body is watched for the damage it takes, once each.</summary>
        void WatchVoidHits()
        {
            if (voidWeapons.Count == 0) return;
            foreach (var body in Combatant.All)
            {
                if (!body || body.Team != Team.Enemy || drainWatched.Contains(body)) continue;
                body.DamageTaken += VoidWeaponHit;
                drainWatched.Add(body);
            }
        }

        void StopVoidDrain()
        {
            foreach (var body in drainWatched) if (body) body.DamageTaken -= VoidWeaponHit;
            drainWatched.Clear();
        }

        /// <summary>A hit by a void-enchanted weapon of the player's pays energy as a Void hit does (ElementStatus.PayWielder: its share of
        /// the damage dealt). A weapon whose own element is Void already paid through that hit, so it is not paid twice.</summary>
        void VoidWeaponHit(DamageInfo info, float amount)
        {
            if (!Running || amount <= 0f || info.weapon == null || info.element == ElementKind.Void || !voidWeapons.Contains(info.weapon)) return;
            var p = gm ? gm.player : null;
            if (!p || info.dealer == null || info.dealer.Owner != (Combatant)p) return;
            var board = ElementStatus.For(p);
            if (board) board.PayWielder(ElementKind.Void, amount);
        }

        // ───── The study window's Void room ─────

        static string VoidCost(int energyCost, int alloyCost)
        {
            if (energyCost > 0 && alloyCost > 0) return ArenaText.Pair($"{energyCost} 虚空能量 · {alloyCost} 虚空合金", $"{energyCost} void energy · {alloyCost} void alloy");
            return energyCost > 0 ? ArenaText.Pair($"{energyCost} 虚空能量", $"{energyCost} void energy") : ArenaText.Pair($"{alloyCost} 虚空合金", $"{alloyCost} void alloy");
        }

        /// <summary>The Void room's tiles, once the Prophet is beaten: the three void skills, each held weapon's void enchant, the void Mk
        /// and the armor's void enchant.</summary>
        partial void CollectVoidStudy(List<StudyEntry> into)
        {
            if (into == null || !Running || !prophetBeaten || !gm) return;
            var skills = gm.skills;
            if (skills && skills.Catalog)
                for (var id = SkillId.PocketVoid; id <= SkillId.Singularity; id++) AddVoidSkill(into, skills, id);
            var weapons = gm.runWeapons;
            if (weapons)
            {
                for (int i = 0; i < weapons.Hand.Length; i++) AddVoidEnchant(into, weapons.Hand[i], "hand" + i);
                for (int i = 0; i < weapons.Auto.Length; i++) AddVoidEnchant(into, weapons.Auto[i], "auto" + i);
            }
            AddVoidMk(into);
            AddVoidArmor(into);
        }

        StudyState VoidState(bool owned, int energyCost, int alloyCost) =>
            owned ? StudyState.Owned : voidEnergy >= energyCost && voidAlloy >= alloyCost ? StudyState.Available : StudyState.Unaffordable;

        void AddVoidSkill(List<StudyEntry> into, SkillLoadout skills, SkillId id)
        {
            var definition = skills.Catalog.Find(id);
            if (!definition) return;
            bool owned = SkillSlotOf(skills, id) >= 0;
            int replace = owned ? -1 : VoidSkillPlace(skills);
            string replaced = replace >= 0 ? skills.Slots[replace].Name : null;
            int cost = rules.voidSkillCost;
            into.Add(new StudyEntry
            {
                id = "void.skill." + id,
                room = StudyRoom.Void,
                category = ArenaText.Pair("虚空技能", "Void skills"),
                rarity = ArenaRarity.ForSkill(definition, 1),
                icon = StudyArt ? StudyIcon(StudyArt.skillIcons, (int)id) ?? IconOf(definition.icon) : IconOf(definition.icon),
                title = ArenaTerms.Skill(id),
                now = owned ? ArenaText.Pair("已学会", "Learned") : ArenaText.Pair("未学会", "Not learned"),
                after = owned ? "" : replaced != null ? ArenaText.Pair($"学会（替换{replaced}）", $"Learned (replaces {replaced})") : ArenaText.Pair("学会", "Learned"),
                detail = definition.Description(1) + ArenaText.Pair($"\n每次使用消耗 {definition.cost:0} 能量。虚空技能没有等级。", $"\nEach cast costs {definition.cost:0} energy. Void skills have no levels.")
                    + (replaced != null ? ArenaText.Pair($"\n技能栏已满：学会它会替换等级最低的{replaced}。", $"\nThe skill slots are full: it replaces the lowest-level skill, {replaced}.") : ""),
                costText = VoidCost(cost, 0),
                state = VoidState(owned, cost, 0),
                buy = () => BuyVoidSkill(id, cost),
            });
        }

        bool BuyVoidSkill(SkillId id, int cost)
        {
            var skills = gm ? gm.skills : null;
            if (!Running || !prophetBeaten || !skills || SkillSlotOf(skills, id) >= 0 || voidEnergy < cost) { V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel); return false; }
            if (!skills.Learn(id, VoidSkillPlace(skills))) { V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel); return false; }
            CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneVoidPurchase, "skill:" + id, cost, Day);
            return TrySpendVoid(cost, 0);
        }

        static int SkillSlotOf(SkillLoadout skills, SkillId id)
        {
            for (int i = 0; i < skills.Slots.Length; i++) if (!skills.Slots[i].Empty && skills.Slots[i].Id == id) return i;
            return -1;
        }

        /// <summary>Where a new void skill goes: an empty slot (-1, Learn takes the first), else the slot with the lowest level (the first
        /// of them). The study window offers no choice, so the tile says which skill it replaces.</summary>
        static int VoidSkillPlace(SkillLoadout skills)
        {
            int lowest = -1;
            for (int i = 0; i < skills.Slots.Length; i++)
            {
                if (skills.Slots[i].Empty) return -1;
                if (lowest < 0 || skills.Slots[i].Level < skills.Slots[lowest].Level) lowest = i;
            }
            return lowest;
        }

        void AddVoidEnchant(List<StudyEntry> into, RunWeapon weapon, string slot)
        {
            if (weapon == null) return;
            bool owned = voidWeapons.Contains(weapon);
            int cost = rules.voidWeaponEnchantCost;
            var board = gm.player ? ElementStatus.For(gm.player) : null;
            int share = board && board.Rules ? Mathf.RoundToInt(board.Rules.voidShare * 100f) : 0;
            into.Add(new StudyEntry
            {
                id = "void.enchant." + slot,
                room = StudyRoom.Void,
                category = ArenaText.Pair("武器", "Weapons"),
                rarity = CardRarity.Void,
                icon = StudyWeaponIcon(weapon.form),
                title = ArenaText.Pair($"虚空附魔：{weapon.Label}", $"Void enchant: {weapon.Label}"),
                now = owned ? ArenaText.Pair("命中吸蓝", "Hits restore energy") : ArenaText.Pair("无", "None"),
                after = owned ? "" : ArenaText.Pair($"命中伤害的 {share}% 变成能量", $"{share}% of hit damage as energy"),
                detail = ArenaText.Pair($"给这把武器附上虚空：它每次命中，按造成伤害的 {share}% 给你回复能量（和虚空元素相同的比例）。它原来的元素不变。武器换下以后，附魔跟着这把武器。",
                    $"Enchant this weapon with the void: each hit restores {share}% of the damage it deals as energy (the Void element's share). Its own element stays. The enchant stays with this weapon if it leaves your hands."),
                costText = VoidCost(0, cost),
                state = VoidState(owned, 0, cost),
                buy = () => BuyVoidEnchant(weapon, cost),
            });
        }

        bool BuyVoidEnchant(RunWeapon weapon, int cost)
        {
            if (!Running || !prophetBeaten || weapon == null || voidWeapons.Contains(weapon) || !TrySpendVoid(0, cost)) return false;
            voidWeapons.Add(weapon);
            CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneVoidPurchase, "enchant:" + weapon.form, cost, Day);
            return true;
        }

        void AddVoidMk(List<StudyEntry> into)
        {
            int mk = TeamMk, from = rules.voidMkFrom, to = from + 1, cost = rules.voidMkCost;
            bool owned = mk >= to;
            into.Add(new StudyEntry
            {
                id = "void.mk",
                room = StudyRoom.Void,
                category = ArenaText.Pair("武器", "Weapons"),
                rarity = CardRarity.Void,
                icon = StudyAxisIcon(UpgradeAxis.Power),
                title = ArenaText.Pair($"虚空 MK{to}", $"Void Mk {to}"),
                now = mk > 0 ? $"MK{mk}" : "",
                after = owned ? "" : ArenaText.Pair($"MK{to}（黑色标记）", $"Mk {to} (black mark)"),
                detail = ArenaText.Pair($"全队武器从 MK{from} 升到 MK{to}：手里和以后拿到的所有武器一起升，等级标记变成黑色。全队 MK 到 {from} 以后才能升。",
                    $"Raises the whole armament from Mk {from} to Mk {to}: every weapon in hand and every weapon taken later, its mark turning black. Needs the armament at Mk {from}."),
                costText = VoidCost(0, cost),
                state = owned ? StudyState.Owned : mk < from ? StudyState.Locked : VoidState(false, 0, cost),
                buy = () => BuyVoidMk(from, to, cost),
            });
        }

        bool BuyVoidMk(int from, int to, int cost)
        {
            if (!Running || !prophetBeaten || TeamMk != from || !TrySpendVoid(0, cost)) return false;
            SetTeamMk(to); // V2Campaign.Rooms.cs: every anomaly weapon held follows at once
            if (TeamMk == to) CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneVoidPurchase, "mk:" + to, cost, Day);
            if (TeamMk == to) return true;
            voidAlloy += cost; // the armament did not take the level: nothing is spent
            return false;
        }

        void AddVoidArmor(List<StudyEntry> into)
        {
            int cost = rules.voidArmorCost;
            // The campaign's own rate since 2026-11-10 (RunProgression.VoidRegenRule, V2Campaign.Days.cs).
            float regen = rules.voidArmorEnergyRegen;
            into.Add(new StudyEntry
            {
                id = "void.armor",
                room = StudyRoom.Void,
                category = ArenaText.Pair("装甲", "Armor"),
                rarity = CardRarity.Void,
                icon = art ? art.armor : null,
                title = ArenaText.Pair("虚空装甲附魔", "Void armor enchant"),
                now = voidArmor ? ArenaText.Pair($"能量每秒 +{regen:0.#}", $"Energy +{regen:0.#}/s") : ArenaText.Pair("能量不会自动恢复", "No energy regeneration"),
                after = voidArmor ? "" : ArenaText.Pair($"能量每秒 +{regen:0.#}", $"Energy +{regen:0.#}/s"),
                detail = ArenaText.Pair($"给你的装甲附上虚空：能量随时间自动恢复，每秒 {regen:0.#} 点，到上限为止。", $"Enchant your armor with the void: energy restores over time, {regen:0.#} a second, up to its maximum."),
                costText = VoidCost(0, cost),
                state = VoidState(voidArmor, 0, cost),
                buy = BuyVoidArmor,
            });
        }

        bool BuyVoidArmor()
        {
            if (!Running || !prophetBeaten || voidArmor || !gm.progression || !TrySpendVoid(0, rules.voidArmorCost)) return false;
            voidArmor = true;
            CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneVoidPurchase, "armor", rules.voidArmorCost, Day);
            // The progression's own void energy regeneration (RunProgression.Tick: voidEnergyRegeneration a second while energy is unlocked).
            gm.progression.CompleteVoidTrial();
            return true;
        }

        Sprite IconOf(Texture2D texture)
        {
            if (!texture) return null;
            if (voidIcons.TryGetValue(texture, out var sprite) && sprite) return sprite;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f));
            voidIcons[texture] = sprite;
            return sprite;
        }
    }
}
