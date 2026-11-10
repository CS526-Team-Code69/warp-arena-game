using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>GDD §9. The run manager ticks this once; XP never opens a menu mid-fight.</summary>
    [DisallowMultipleComponent]
    public sealed class RunProgression : MonoBehaviour
    {
        [field: SerializeField, Tooltip("Levels, stat cards and energy rules (GDD §9).")]
        public ProgressionRules Rules { get; set; }
        [field: SerializeField] public RunStats Stats { get; private set; } = new();
        public int Level { get; private set; } = 1;
        public float Xp { get; private set; }
        public int XpRequired => Level >= Rules.maxLevel ? Rules.xpPerStatDraft
            : Rules.levelCosts[Mathf.Clamp(Level - 1, 0, Rules.levelCosts.Length - 1)];
        public int BankedDrafts => drafts.Count;
        public bool NextDraftIsStatOnly => drafts.Count > 0 && drafts.Peek();
        public float Energy { get; private set; }
        public bool EnergyUnlocked { get; private set; }
        public bool VoidEnergy { get; private set; }
        /// <summary>The void trial's extra level was granted: from then on the level reads in void purple, at any level (document 9.3).</summary>
        public bool VoidBadge { get; private set; }
        public bool MayOfferPenetration { get; set; }
        public bool MustOfferPenetration { get; set; }
        public DraftCard[] CurrentCards { get; private set; } = Array.Empty<DraftCard>();
        public int RerollsRemaining { get; private set; }
        public float LastLevelHpGain { get; private set; }
        public float LastLevelEnergyGain { get; private set; }
        public event Action Changed;
        public event Action<int> LevelGained;
        public event Action EnergyRefilled;
        public event Action<float> HealingRequested;
        public event Action<StatId, float> StatChanged;
        readonly Queue<bool> drafts = new();
        readonly List<StatCardValues> eligible = new();
        readonly List<StatId> draftStats = new(), shownStats = new();
        float regenerationClock;
        int statsWithoutHighTier;
        int draftWave;
        // Each factory gets the card it replaces (a rerolled or outgrown one), or null for a fresh draw.
        Func<DraftCard, DraftCard> weaponCardFactory;
        Func<DraftCard, DraftCard> skillCardFactory;
        /// <summary>V2 day campaign: stat cards come only with the Energy Lab (null in the classic run: always).</summary>
        public Func<bool> StatCardsAllowed { get; set; }
        /// <summary>V2 day campaign: XP turns into levels only with the Armament Bay; XP earned before is kept (null: always).</summary>
        public Func<bool> LevelingAllowed { get; set; }
        /// <summary>V2 day campaign since 2026-11-06: kills give no XP. The player's levels are bought at the Armament Bay (BuyLevel) and the
        /// card picks in the rooms (BankPick), as the V2 doc places every upgrade. Null in the classic run: XP raises the level as before.</summary>
        public Func<bool> XpIgnored { get; set; }
        /// <summary>V2 day campaign: the title of the card ResolvePendingRandom applied last, or null when it applied nothing.</summary>
        public string LastResolvedTitle { get; private set; }
        /// <summary>A card pick was used up (V2 day campaign: the room that sold it is done with it).</summary>
        public event Action DraftConsumed;

        /// <summary>V2 day campaign: one card pick bought at the base; it waits with the other banked picks for the build phase.</summary>
        public void BankPick()
        {
            BankDraft(false);
            OnChanged();
        }
        public bool AtMaxLevel => Level >= Rules.maxLevel;

        /// <summary>V2 day campaign: one level bought at the Armament Bay. It gives what an XP level gives in the classic run (Max HP, Max
        /// Energy, a full heal), without a card pick. False at the max level.</summary>
        public bool BuyLevel()
        {
            if (AtMaxLevel) return false;
            Level++;
            GrowLevelResources();
            OnLevelGained(Level);
            OnChanged();
            return true;
        }

        public void ResetRun()
        {
            Stats = Rules.startingStats.Copy(); Stats.maxEnergy = Rules.energyMax;
            Level = 1; Xp = 0f; Energy = 0f; EnergyUnlocked = false; VoidEnergy = false; VoidBadge = false;
            LastLevelHpGain = LastLevelEnergyGain = 0f;
            MayOfferPenetration = MustOfferPenetration = false; regenerationClock = 0f; statsWithoutHighTier = 0; draftWave = 0; drafts.Clear();
            CurrentCards = Array.Empty<DraftCard>(); RerollsRemaining = 0;
            OnChanged();
        }

        public void GainXp(float amount)
        {
            if (amount <= 0f || XpIgnored != null && XpIgnored()) return;
            Xp += amount;
            while (Xp >= XpRequired && (LevelingAllowed == null || LevelingAllowed()))
            {
                Xp -= XpRequired;
                bool statOnly = Level >= Rules.maxLevel;
                if (!statOnly) { Level++; GrowLevelResources(); OnLevelGained(Level); }
                BankDraft(statOnly);
            }
            OnChanged();
        }

        /// <summary>V2 day campaign: the Armament Bay was built, so the XP held back turns into levels now.</summary>
        public void ReleaseHeldLevels() { if (Xp >= XpRequired) GainXp(float.Epsilon); }

        /// <summary>Winning the void trial is one forced extra level, exactly like a level-up: Max HP and Max Energy grow,
        /// Energy refills and one card pick is banked (stat cards only at max level). XP, the bar, the level number and the next cost stay.</summary>
        public void GrantVoidLevel()
        {
            VoidBadge = true;
            GrowLevelResources();
            BankDraft(Level >= Rules.maxLevel);
            OnChanged();
        }

        void BankDraft(bool statOnly)
        {
            drafts.Enqueue(statOnly);
            if (EnergyUnlocked) { Energy = Stats.maxEnergy; OnEnergyRefilled(); }
        }

        void GrowLevelResources()
        {
            LastLevelHpGain = Stats.Add(StatId.MaxHp,Mathf.Max(0f,Rules.maxHpPerLevel));
            float energyStep = Mathf.Max(0f,Rules.maxEnergyPerLevel);
            int energyIndex = (int)StatId.MaxEnergy;
            // The original card budget is cap - baseline. Raising both by the same amount preserves that budget,
            // and cards taken earlier cannot prevent a later level from granting its capacity increase.
            if (Stats.caps != null && energyIndex < Stats.caps.Length && Stats.caps[energyIndex] >= 0f)
                Stats.caps[energyIndex] += energyStep;
            LastLevelEnergyGain = Stats.Add(StatId.MaxEnergy,energyStep);
            if (LastLevelHpGain > 0f) OnStatChanged(StatId.MaxHp,LastLevelHpGain);
            if (LastLevelEnergyGain > 0f) OnStatChanged(StatId.MaxEnergy,LastLevelEnergyGain);
            // A level-up restores HP to full (Energy refills when the draft is banked). StatChanged raised the maximum first.
            OnHealingRequested(float.MaxValue);
        }

        public void ConsumeDraft()
        {
            if (drafts.Count > 0) drafts.Dequeue();
            CurrentCards = Array.Empty<DraftCard>();
            OnChanged();
            DraftConsumed?.Invoke();
        }

        /// <summary>V2 day campaign (since 2026-11-06): a purchase at the base draws and applies a pick of its own kind while another pick may
        /// wait with its cards already drawn (the void trial's extra level, not opened yet or put off). The purchase takes those cards out
        /// of the way first, so it never applies one of them, and gives them back with PutCardsBack once it is done: the waiting pick keeps
        /// its cards, so putting it off stays no free reroll. The classic run never calls these.</summary>
        public DraftCard[] SetCardsAside()
        {
            var held = CurrentCards;
            CurrentCards = Array.Empty<DraftCard>();
            return held;
        }

        /// <summary>Gives back the cards SetCardsAside took, while a pick still waits for them.</summary>
        public void PutCardsBack(DraftCard[] held)
        {
            if (held == null || held.Length == 0 || BankedDrafts == 0) return;
            CurrentCards = held;
            OnChanged();
        }

        /// <summary>One reroll per wave, shared by the picks made during the wave and in its build phase. Call it when a wave starts.</summary>
        public void BeginWave() { RerollsRemaining = 1; OnChanged(); }

        /// <summary>A draft put off during the wave keeps its cards into the build phase.</summary>
        public void BeginBuildPhase(int wave, Func<DraftCard, DraftCard> weaponFactory = null, Func<DraftCard, DraftCard> skillFactory = null)
        {
            draftWave = wave;
            weaponCardFactory = weaponFactory; skillCardFactory = skillFactory;
            OnChanged();
        }

        /// <summary>Shows the next banked draft. A draft drawn earlier and put off comes back with the same cards, so putting a pick off
        /// is not a free reroll and its stat card does not count toward the rarity pity twice; only a card the build has outgrown since
        /// (its weapon replaced, its skill changed) is drawn anew.</summary>
        public DraftCard[] DrawCards(int wave, Func<DraftCard, DraftCard> weaponFactory = null, Func<DraftCard, DraftCard> skillFactory = null)
        {
            draftWave = wave;
            if (weaponFactory != null) weaponCardFactory = weaponFactory;
            if (skillFactory != null) skillCardFactory = skillFactory;
            if (BankedDrafts == 0) return CurrentCards = Array.Empty<DraftCard>();
            if (CurrentCards.Length > 0) { ReplaceOutgrownCards(); return CurrentCards; }
            return CurrentCards = Draw(null);
        }

        /// <summary>Document 9.4: a reroll swaps all three cards, once per wave.</summary>
        public bool Reroll()
        {
            if (RerollsRemaining <= 0 || BankedDrafts == 0) return false;
            RerollsRemaining--; CurrentCards = Draw(CurrentCards); OnChanged(); return true;
        }

        /// <summary>A, B, C = stat, weapon, skill card (three stat cards for a stat-only draft); a missing weapon or skill card becomes a
        /// stat card, and the stat cards of one draft differ. With before (a reroll), each new card differs from the one in its place
        /// whenever the pool has another, and stat cards avoid every stat the old draft showed while they can.</summary>
        DraftCard[] Draw(DraftCard[] before)
        {
            if (BankedDrafts == 0) return Array.Empty<DraftCard>();
            bool statOnly = NextDraftIsStatOnly;
            draftStats.Clear(); shownStats.Clear();
            if (before != null) foreach (var card in before) if (card?.Payload is StatCardOffer shown) shownStats.Add(shown.Stat);
            var cards = new DraftCard[3];
            for (int i = 0; i < cards.Length; i++)
            {
                var old = before != null && i < before.Length ? before[i] : null;
                if (!statOnly && i == 1) cards[i] = weaponCardFactory?.Invoke(old?.Kind == DraftCardKind.Weapon ? old : null);
                else if (!statOnly && i == 2) cards[i] = skillCardFactory?.Invoke(old?.Kind == DraftCardKind.Skill ? old : null);
                if (cards[i] != null) continue;
                if (StatCardsAllowed != null && !StatCardsAllowed()) continue;
                StatId? replaced = old?.Payload is StatCardOffer oldStat ? oldStat.Stat : null;
                var offer = RollStatCard(draftStats, shownStats, replaced, forcePenetration: i == 0 && MustOfferPenetration);
                draftStats.Add(offer.Stat); cards[i] = MakeStat(offer);
            }
            shownStats.Clear();
            // V2 day campaign: a locked card kind leaves its place empty and the rest close up (none left: the pick waits).
            if (Array.IndexOf(cards, null) >= 0) cards = Array.FindAll(cards, card => card != null);
            return cards;
        }

        void ReplaceOutgrownCards()
        {
            for (int i = 0; i < CurrentCards.Length; i++)
            {
                var old = CurrentCards[i];
                if (old == null || old.StillFits) continue;
                var card = old.Kind == DraftCardKind.Weapon ? weaponCardFactory?.Invoke(old) : old.Kind == DraftCardKind.Skill ? skillCardFactory?.Invoke(old) : null;
                if (card == null)
                {
                    draftStats.Clear(); shownStats.Clear();
                    foreach (var other in CurrentCards) if (other?.Payload is StatCardOffer stat) draftStats.Add(stat.Stat);
                    card = MakeStat(RollStatCard(draftStats, shownStats, null, false));
                }
                CurrentCards[i] = card;
            }
        }

        public bool ApplyCard(int index, int replacementIndex = -1)
        {
            if (index < 0 || index >= CurrentCards.Length || !CurrentCards[index].Apply(replacementIndex)) return false;
            ConsumeDraft(); return true;
        }

        /// <summary>GDD §2.3: random picks only among choices that need no replacement prompt.</summary>
        public void ResolvePendingRandom()
        {
            LastResolvedTitle = null;
            while (BankedDrafts > 0)
            {
                DrawCards(draftWave);
                if (CurrentCards.Length == 0) break; // V2 day campaign: no card kind is unlocked yet
                int eligibleCount = 0;
                foreach (var card in CurrentCards) if (!card.NeedsReplacement) eligibleCount++;
                int pick = UnityEngine.Random.Range(0, eligibleCount);
                DraftCard applied = null;
                for (int i = 0; i < CurrentCards.Length; i++)
                {
                    if (CurrentCards[i].NeedsReplacement) continue;
                    if (pick-- > 0) continue;
                    var chosen = CurrentCards[i];
                    if (ApplyCard(i)) applied = chosen;
                    break;
                }
                // A weapon callback can become ineligible between draws; the guaranteed Stat card remains valid.
                if (applied == null)
                {
                    var first = CurrentCards[0];
                    if (ApplyCard(0)) applied = first;
                }
                if (applied != null) LastResolvedTitle = applied.Title;
                // V2 day campaign: a draw limited to one kind may hold nothing that applies without a prompt (only a new skill while
                // every skill slot is taken). The classic draw always has its stat card, so this never happens there; without it the loop
                // would draw the same cards forever.
                else ConsumeDraft();
            }
        }

        DraftCard MakeStat(StatCardOffer offer) => new(DraftCardKind.Stat, offer.Title, offer.Description,
            _ => { ApplyStat(offer); return true; }, payload: offer, rarity: offer.Rarity);

        public void UnlockEnergy()
        {
            if (EnergyUnlocked) return;
            EnergyUnlocked = true; Energy = Stats.maxEnergy; OnChanged();
        }

        public void CompleteVoidTrial() { VoidEnergy = true; OnChanged(); }
        public bool TrySpendEnergy(float cost)
        {
            if (!EnergyUnlocked || Energy < cost) return false;
            Energy -= Mathf.Max(0f, cost); OnChanged(); return true;
        }
        public void GainEnergy(float amount)
        {
            if (!EnergyUnlocked) return;
            Energy = Mathf.Clamp(Energy + amount, 0f, Stats.maxEnergy); OnChanged();
        }
        public void GainDeathEnergy() => GainEnergy(Stats.energySiphon);
        /// <summary>V2 day campaign (since 2026-11-10): the void energy regeneration a second, given the rules' own (the void armor
        /// enchant's rate); null outside a running campaign, where ProgressionRules.voidEnergyRegeneration applies.</summary>
        public static Func<float, float> VoidRegenRule;

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (VoidEnergy && EnergyUnlocked && Energy < Stats.maxEnergy)
                GainEnergy((VoidRegenRule?.Invoke(Rules.voidEnergyRegeneration) ?? Rules.voidEnergyRegeneration) * dt);
            regenerationClock += dt;
            while (regenerationClock >= Rules.regenerationInterval)
            {
                regenerationClock -= Rules.regenerationInterval;
                if (Stats.regeneration > 0f) OnHealingRequested(Stats.regeneration);
            }
        }

        /// <summary>Changes max HP by delta but never below floor (a max HP already under floor is not lowered further).
        /// A loss requests no healing; a gain heals the gained amount like level and card gains.</summary>
        public void AdjustMaxHp(float delta, float floor)
        {
            float before = Stats.maxHp;
            float after = Mathf.Max(before + delta, Mathf.Min(before, floor));
            if (Mathf.Approximately(after, before)) return;
            Stats.maxHp = after;
            float actual = after - before;
            OnStatChanged(StatId.MaxHp, actual);
            if (actual > 0f) OnHealingRequested(actual);
            OnChanged();
        }

        public void ApplyStat(StatCardOffer offer)
        {
            float gained = Stats.Add(offer.Stat, offer.Value);
            if (offer.Bonus != StatBonus.None) Stats.AddBonus(offer.Bonus, offer.BonusValue);
            OnStatChanged(offer.Stat, gained);
            if (offer.Stat == StatId.MaxHp) OnHealingRequested(gained);
            OnChanged();
        }

        public StatCardOffer RollStatCard(StatId? excludeA = null, StatId? excludeB = null, bool forcePenetration = false)
        {
            draftStats.Clear(); shownStats.Clear();
            if (excludeA.HasValue) draftStats.Add(excludeA.Value);
            if (excludeB.HasValue) draftStats.Add(excludeB.Value);
            return RollStatCard(draftStats, shownStats, null, forcePenetration);
        }

        /// <summary>A stat card that is none of taken. On a reroll it also avoids every stat in shown while another is eligible, then at
        /// least replaced (the stat in its own place before), and repeats one only when nothing else is left.</summary>
        StatCardOffer RollStatCard(List<StatId> taken, List<StatId> shown, StatId? replaced, bool forcePenetration)
        {
            if (CollectEligible(stat => taken.Contains(stat) || shown.Contains(stat), forcePenetration) == 0
                && (replaced == null || CollectEligible(stat => taken.Contains(stat) || stat == replaced, forcePenetration) == 0))
                CollectEligible(taken.Contains, forcePenetration);
            if (eligible.Count == 0) return new StatCardOffer(StatId.MaxHp, CardRarity.Common, 12f);
            var picked = eligible[UnityEngine.Random.Range(0, eligible.Count)];
            // Decision list §1 #8: the band of the wave just finished; pity forces its high tier.
            bool pity = statsWithoutHighTier >= Mathf.Max(1, Rules.highTierPity);
            CardRarity rarity = Rules.StatRarity(draftWave, UnityEngine.Random.value * 100f, pity);
            ProgressionRules.RarityBand(draftWave, out _, out var high);
            statsWithoutHighTier = rarity == high ? 0 : statsWithoutHighTier + 1;
            return new StatCardOffer(picked.stat, rarity, picked.At(rarity), picked.bonus, picked.BonusAt(rarity));
        }

        int CollectEligible(Func<StatId, bool> skip, bool forcePenetration)
        {
            eligible.Clear();
            foreach (var card in Rules.statCards)
            {
                if (card.retired || skip(card.stat) || Stats.AtCap(card.stat)) continue;
                if(forcePenetration&&card.stat!=StatId.ElementPenetration)continue;
                if (!EnergyUnlocked && card.stat is StatId.MaxEnergy or StatId.EnergySiphon) continue;
                if (card.stat == StatId.ElementPenetration && !MayOfferPenetration) continue;
                eligible.Add(card);
            }
            return eligible.Count;
        }

        void OnChanged() => Changed?.Invoke();
        void OnLevelGained(int level) => LevelGained?.Invoke(level);
        void OnEnergyRefilled() => EnergyRefilled?.Invoke();
        void OnHealingRequested(float amount) => HealingRequested?.Invoke(amount);
        void OnStatChanged(StatId stat, float amount) => StatChanged?.Invoke(stat, amount);
    }
}
