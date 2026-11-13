using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The campaign's numbers by day at work (2026-11-10; the numbers are CampaignRules.Days.cs, from the number ledger). Through static
    /// hooks set at the start of a run, each neutral outside a running campaign so the open mode's 15 waves are untouched: each day's
    /// morning brings only the kinds its day allows (WaveManager.KindWeightRule; the wave's count and pace stay); every body gets the
    /// campaign's HP by day and the two bosses their fixed HP and shields (Enemy.CampaignBudget); skills and statuses follow the day's
    /// scale (WaveManager.CombatScaleRule) and monsters hit as hard as the day's table says (WaveManager.DamageFactorRule); what is paid as
    /// a share of damage shrinks by day (ElementStatus.PayoffScale); Tame and Twist
    /// add their attack speed and damage (RunWeapons.WeaponSpeedRule, WeaponDamageRule); the void armor restores energy at the campaign's
    /// rate (RunProgression.VoidRegenRule). Here too: the energy bar filled at each morning, one point of energy for each death, the day's
    /// alien energy shared out over its kills so that killing every monster pays exactly the table, the body research's stat lines, and
    /// Day 0's gifts.
    /// </summary>
    public sealed partial class V2Campaign
    {
        // What is left of today's kill energy, and the weight (seconds to kill) of the bodies killed today.
        int killEnergyLeft;
        float killedWeight;
        // Kill energy held back by kills that dropped none (paid with the next drop), and how many kills in a row dropped none.
        int energyHeld, killsWithoutDrop;
        // Before this many seconds of a wave the pace of its arrivals is not judged (the first bodies all come at once).
        const float MinPacingSeconds = 3f;

        /// <summary>This campaign is the one running: the hooks give its numbers, otherwise the neutral ones.</summary>
        bool DaysLive => this && Running && Active == this && rules;
        bool TyrantFight => Stage == CampaignStage.FinalBoss;

        /// <summary>For automated checks: what is left of today's kill energy.</summary>
        public int KillEnergyLeft => killEnergyLeft;

        void InstallDayHooks()
        {
            WaveManager.KindWeightRule = (kind, saved) => DaysLive && Day > 0 ? rules.DayKindWeight(Day, kind, saved) : saved;
            WaveManager.CombatScaleRule = () => DaysLive ? rules.CombatScaleOn(Day, TyrantFight) : 0f;
            WaveManager.DamageFactorRule = () => DaysLive ? rules.EnemyDamageOn(Day, TyrantFight) : 0f;
            Enemy.CampaignBudget = (data, elite, affix, mini) => DaysLive && data ? BudgetFor(data, elite, affix, mini) : (EnemyBudget?)null;
            ElementStatus.PayoffScale = body => DaysLive && body && gm && body == gm.player ? rules.PayoffScaleOn(Day, TyrantFight) : 1f;
            RunProgression.VoidRegenRule = rate => DaysLive ? rules.voidArmorEnergyRegen : rate;
            RunWeapons.WeaponDamageRule = w => DaysLive && w != null && w.Has(UpgradeAxis.Twist) ? Mathf.Max(1f, rules.twistDamage) : 1f;
            RunWeapons.WeaponSpeedRule = w => DaysLive && w != null && w.Has(UpgradeAxis.Tame) ? Mathf.Max(1f, rules.tameAttackSpeed) : 1f;
            // The mornings: the same length every day and more monsters (V2Campaign.cs puts part of them on a near ring); every story
            // monster walks a little slower than the player, so a black hole always takes it.
            WaveManager.DurationRule = () => Mornings ? rules.morningSeconds : 0f;
            WaveManager.CrowdRule = () => Mornings ? rules.morningCrowd : 0f;
            WaveManager.WalkScaleRule = () => DaysLive ? rules.monsterWalkScale : 0f;
            WaveManager.WalkCapRule = () => DaysLive && gm && gm.player ? rules.monsterWalkCap * gm.player.moveSpeed : 0f;
        }

        bool Mornings => DaysLive && Day > 0 && Stage is CampaignStage.Day or CampaignStage.Lull;

        static void ClearDayHooks()
        {
            WaveManager.KindWeightRule = null;
            WaveManager.CombatScaleRule = null;
            WaveManager.DamageFactorRule = null;
            Enemy.CampaignBudget = null;
            ElementStatus.PayoffScale = null;
            RunProgression.VoidRegenRule = null;
            RunWeapons.WeaponDamageRule = RunWeapons.WeaponSpeedRule = null;
            WaveManager.DurationRule = WaveManager.CrowdRule = WaveManager.WalkScaleRule = WaveManager.WalkCapRule = null;
        }

        /// <summary>A body's HP and shields in the campaign: the Nash Prophet without a shield, the Tyrant with its two shield shares, every
        /// other body by the day's reference DPS (the Tyrant fight's for the monsters it calls) with its usual opening shield.</summary>
        EnemyBudget BudgetFor(EnemyDefinition data, bool elite, ElementKind affix, bool mini)
        {
            var balance = gm && gm.waves && gm.waves.waveSet ? gm.waves.waveSet.encounterBalance : null;
            float direct = balance ? balance.ShieldDirectFactor : .7f;
            float miniShare = !mini ? 1f : balance ? balance.MiniShare(true) : .4f;
            bool tyrant = data.kind == EnemyKind.Tyrant;
            float start = tyrant ? rules.tyrantStartShieldShare : data.kind == EnemyKind.Hollow ? 0f : EncounterBalance.InitialShieldShare(data, affix);
            float hp = rules.BodyHp(data.kind, Day, elite, start, direct, miniShare, TyrantFight);
            return new EnemyBudget(hp, start, tyrant ? rules.tyrantPhaseShieldShare : 0f);
        }

        // ───── The morning: the energy bar, and the day's alien energy over its kills ─────

        /// <summary>A new day's morning begins (StartNextWave): the energy bar fills, and the day's kill energy is ready to drop.</summary>
        void BeginDayNumbers()
        {
            killEnergyLeft = rules.DayKillEnergy(Day);
            killedWeight = 0f;
            energyHeld = killsWithoutDrop = 0;
            var progression = gm ? gm.progression : null;
            if (rules.dawnEnergyRefill && progression) progression.GainEnergy(progression.Stats.maxEnergy);
        }

        /// <summary>A body's weight in the day's kill energy: its seconds to kill (a mini Splitter its share of a Splitter's).</summary>
        float BodyWeight(EnemyDefinition data, bool elite, bool mini)
        {
            var balance = gm && gm.waves && gm.waves.waveSet ? gm.waves.waveSet.encounterBalance : null;
            float share = !mini ? 1f : balance ? balance.MiniShare(true) : .4f;
            return rules.TargetSeconds(data.kind, elite) * share;
        }

        /// <summary>DropCurrencies by day: what this kill drops of the day's kill energy (CampaignRules.KillEnergyShare).</summary>
        int KillEnergyFor(Enemy enemy)
        {
            if (killEnergyLeft <= 0 && energyHeld <= 0 || !enemy.definition) return 0;
            float weight = BodyWeight(enemy.definition, enemy.Elite, enemy.Mini);
            killedWeight += weight;
            float stillToCome = WeightStillToCome();
            int amount = CampaignRules.KillEnergyShare(killEnergyLeft, weight, stillToCome, Random.value);
            killEnergyLeft -= amount;
            energyHeld += amount;
            // Not every kill drops: one that does pays what the kills before it held back. The pity and the day's last kill always drop.
            bool last = stillToCome <= 0f;
            float chance = rules.DropChance(enemy.definition.kind, enemy.Elite, enemy.Mini);
            if (!last && !CampaignRules.KillDrops(chance, killsWithoutDrop, rules.dropPity, Random.value))
            {
                killsWithoutDrop++;
                return 0;
            }
            killsWithoutDrop = 0;
            int paid = energyHeld;
            energyHeld = 0;
            return paid;
        }

        /// <summary>The weight of the day's bodies still to come after this kill: those standing and announced; once the day's last wave
        /// has run out of time nothing more comes, before that as many more as came so far in the time gone by, for the time left (and for
        /// each later wave of the day).</summary>
        float WeightStillToCome()
        {
            var waves = gm ? gm.waves : null;
            if (!waves) return 0f;
            float standing = 0f;
            foreach (var e in waves.Alive)
                if (e && e.IsAlive && !e.IsBoss && e.definition) standing += BodyWeight(e.definition, e.Elite, e.Mini);
            int markers = 0;
            if (waves.preparedMarkers != null)
                foreach (var m in waves.preparedMarkers)
                    if (m && m.gameObject.activeSelf && m.definition && !m.bossSummon) { standing += BodyWeight(m.definition, m.elite, false); markers++; }
            // The rest of what is pending (a batch not yet announced, a dead Splitter's minis) counts as melee bodies.
            standing += Mathf.Max(0, waves.PendingCount - markers) * rules.TargetSeconds(EnemyKind.Grunt, false);
            int laterWaves = Mathf.Max(0, rules.wavesPerDay - WaveOfDay - 1);
            if (waves.TimeExpired && laterWaves == 0) return standing;
            float length = waves.WaveSeconds;
            float left = waves.TimeExpired ? 0f : waves.Phase == WavePhase.WaveIntro ? length : waves.TimeLeft;
            float rate = (killedWeight + standing) / Mathf.Max(MinPacingSeconds, length - left);
            return standing + rate * (left + laterWaves * length);
        }

        /// <summary>The morning is over (InterceptBuild, before the afternoon): kill energy no kill dropped (a monster lost in a gap drops
        /// nothing) lies at the player's feet, so the day pays its table entry in full.</summary>
        void DropKillEnergyLeft()
        {
            int left = killEnergyLeft + energyHeld; // with what the last kills held back
            if (left > 0 && gm && gm.player) SpawnPickup(PickupKind.Energy, left, gm.player.Position);
            killEnergyLeft = energyHeld = killsWithoutDrop = 0;
        }

        /// <summary>OnEnemyEliminated: a monster's death restores rules.energyPerKill of the player's energy (the skill bar), a boss's and
        /// that of a monster a boss called excepted.</summary>
        void RestoreEnergyOnKill(Enemy enemy)
        {
            if (!DaysLive || !enemy || enemy.IsBoss || enemy.SuppressRewards || rules.energyPerKill <= 0f || !gm.progression) return;
            gm.progression.GainEnergy(rules.energyPerKill);
        }

        // ───── The body research's stat lines (CampaignRules.statLineMode Replace) ─────

        /// <summary>Steps of a stat's line bought, counted from white up (0 none, 5 at orange).</summary>
        int StatLineSteps(StatId stat)
        {
            int steps = 0;
            while (CampaignRules.StatLineRarity(steps) is { } rarity && statsBought.Contains((stat, rarity))) steps++;
            return steps;
        }

        /// <summary>The step after `steps` of a stat's line, as what it adds: from the value of the step below to its own (Damage by
        /// multiplying), its second effect likewise.</summary>
        static StatCardOffer StatLineOffer(StatCardValues row, int steps)
        {
            var rarity = CampaignRules.StatLineRarity(steps) ?? CardRarity.Legendary;
            var below = CampaignRules.StatLineRarity(steps - 1);
            float from = below is { } b ? row.At(b) : 0f, bonusFrom = below is { } bb ? row.BonusAt(bb) : 0f;
            return new StatCardOffer(row.stat, rarity, CampaignRules.StatLineStep(row.stat, from, row.At(rarity)), row.bonus, row.BonusAt(rarity) - bonusFrom);
        }

        /// <summary>TryBuyStat with stat lines: only the line's next step sells (white first), its rarity needs its body research tier, and
        /// its value takes the place of the step below.</summary>
        bool TryBuyStatLine(StatId stat, CardRarity rarity)
        {
            var row = StatRow(stat);
            int steps = StatLineSteps(stat);
            if (row == null || CampaignRules.StatLineRarity(steps) != rarity || !Works(CampaignRoom.Energy, 1)
                || rules.RarityTier(rarity) > ResearchTier(ResearchLine.Body) || gm.progression.Stats.AtCap(stat)) return false;
            var price = new CampaignCost(0, rules.RarityEnergy(rarity));
            if (!TrySpend(price)) return false;
            gm.progression.ApplyStat(StatLineOffer(row, steps));
            statsBought.Add((stat, rarity));
            Bought("constitution", $"{stat} {rarity}", CampaignRoom.Energy, price);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            var shown = new StatCardOffer(stat, rarity, row.At(rarity));
            ui.Flash(ArenaText.Pair($"体质：{shown.Title}升到{ArenaTerms.Rarity(rarity)}", $"Body: {shown.Title} is now {ArenaTerms.Rarity(rarity)}"));
            return true;
        }

        /// <summary>The body tab with stat lines: one tile per stat, its next step only (or its orange once the line is full), showing the
        /// stat as it is and as that step would make it.</summary>
        void FillBodyLines(StudyTab tab)
        {
            var progression = gm.progression;
            var rows = progression && progression.Rules ? progression.Rules.statCards : null;
            if (rows == null) return;
            int tier = ResearchTierShown(ResearchLine.Body);
            bool works = Works(CampaignRoom.Energy, 1);
            string rule = ArenaText.Pair("每项体质都按白、绿、蓝、紫、橙的顺序一档一档地买。买下高一档后，这一项的数值直接换成高一档的，不和低档叠加。买下立刻生效。",
                "Each stat is bought one step at a time: white, green, blue, purple, then orange. A higher step replaces the value of the one below instead of adding to it. Each purchase takes effect at once.");
            foreach (var row in rows)
            {
                if (row == null || row.retired) continue;
                var stat = row.stat;
                int steps = StatLineSteps(stat);
                var next = CampaignRules.StatLineRarity(steps);
                var rarity = next ?? CardRarity.Legendary;
                var full = new StatCardOffer(stat, rarity, row.At(rarity), row.bonus, row.BonusAt(rarity));
                var stats = progression.Stats;
                var icon = StudyArt ? StudyIcon(StudyArt.statIcons, (int)stat) : null;
                string have = steps > 0 ? ArenaText.Pair($"目前已买到{ArenaTerms.Rarity(CampaignRules.StatLineRarity(steps - 1) ?? CardRarity.Common)}档。", $"Bought up to {ArenaTerms.Rarity(CampaignRules.StatLineRarity(steps - 1) ?? CardRarity.Common)}.")
                    : ArenaText.Pair("这一项还没买过。", "You haven't bought this stat yet.");
                string detail = full.Description + "\n" + have + "\n" + rule;
                if (next == null)
                {
                    tab.entries.Add(StudyItem($"stat.{stat}", StudyRoom.Energy, tab.name, rarity, icon, full.Title, StudyStatValue(stat, stats), "", detail, null, true, null, null));
                    continue;
                }
                var step = StatLineOffer(row, steps);
                var after = stats.Copy();
                after.Add(stat, step.Value);
                if (step.Bonus != StatBonus.None) after.AddBonus(step.Bonus, step.BonusValue);
                var stepRarity = rarity;
                string blocked = !works ? RoomBroken(CampaignRoom.Energy)
                    : rules.RarityTier(rarity) > tier ? ArenaText.Pair($"{ResearchName(ResearchLine.Body)}升到 {rules.RarityTier(rarity)} 阶后，才能买{ArenaTerms.Rarity(rarity)}档", $"The {ArenaTerms.Rarity(rarity)} step needs {ResearchName(ResearchLine.Body)} tier {rules.RarityTier(rarity)}")
                    : stats.AtCap(stat) ? ArenaText.Pair("这项已到上限", "This stat is at its cap") : null;
                tab.entries.Add(StudyItem($"stat.{stat}", StudyRoom.Energy, tab.name, rarity, icon, full.Title, StudyStatValue(stat, stats), StudyStatValue(stat, after),
                    detail, new CampaignCost(0, rules.RarityEnergy(rarity)), false, blocked, () => TryBuyStat(stat, stepRarity)));
            }
        }

        /// <summary>The line the Tame and Twist tiles add: the attack speed or damage they give the weapon besides.</summary>
        string TameTwistBonusText(UpgradeAxis axis) => axis == UpgradeAxis.Tame
            ? ArenaText.Pair($"此外，这把武器的攻速 ×{rules.tameAttackSpeed:0.##}。", $"This weapon also attacks ×{rules.tameAttackSpeed:0.##} as fast.")
            : ArenaText.Pair($"此外，这把武器的伤害 ×{rules.twistDamage:0.##}。", $"This weapon also deals ×{rules.twistDamage:0.##} damage.");

        // ───── Day 0's gifts ─────

        /// <summary>The alloy of Day 0's ore on the way, the first (0) or the second (1).</summary>
        int DayZeroOre(int which)
        {
            var yields = rules.DayZeroOreAlloy(2);
            return which >= 0 && which < yields.Length ? yields[which] : 0;
        }

        /// <summary>Arrival: what tops Day 0's alloy and energy up to rules.dayZeroAlloy and dayZeroEnergy; energy still on the ground
        /// counts as brought (it can be picked up later).</summary>
        CampaignCost DayZeroArrivalGift()
        {
            int lying = 0;
            foreach (var pickup in pickups) if (pickup.kind == PickupKind.Energy) lying += pickup.amount;
            return rules.DayZeroArrival(EarnedAlloy, EarnedEnergy + lying);
        }
    }
}
