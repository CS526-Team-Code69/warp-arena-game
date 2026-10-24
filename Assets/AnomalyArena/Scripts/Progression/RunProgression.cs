using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

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

        Func<DraftCard, DraftCard> weaponCardFactory;
        Func<DraftCard, DraftCard> skillCardFactory;

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
            if (amount <= 0f) return;
            Xp += amount;
            while (Xp >= XpRequired)
            {
                Xp -= XpRequired;
                bool statOnly = Level >= Rules.maxLevel;
                if (!statOnly) { Level++; GrowLevelResources(); OnLevelGained(Level); }
                BankDraft(statOnly);
            }
            OnChanged();
        }

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

            if (Stats.caps != null && energyIndex < Stats.caps.Length && Stats.caps[energyIndex] >= 0f)
                Stats.caps[energyIndex] += energyStep;
            LastLevelEnergyGain = Stats.Add(StatId.MaxEnergy,energyStep);
            if (LastLevelHpGain > 0f) OnStatChanged(StatId.MaxHp,LastLevelHpGain);
            if (LastLevelEnergyGain > 0f) OnStatChanged(StatId.MaxEnergy,LastLevelEnergyGain);

            OnHealingRequested(float.MaxValue);
        }

        public void ConsumeDraft()
        {
            if (drafts.Count > 0) drafts.Dequeue();
            CurrentCards = Array.Empty<DraftCard>();
            OnChanged();
        }

        public void BeginWave() { RerollsRemaining = 1; OnChanged(); }

        public void BeginBuildPhase(int wave, Func<DraftCard, DraftCard> weaponFactory = null, Func<DraftCard, DraftCard> skillFactory = null)
        {
            draftWave = wave;
            weaponCardFactory = weaponFactory; skillCardFactory = skillFactory;
            OnChanged();
        }

        public DraftCard[] DrawCards(int wave, Func<DraftCard, DraftCard> weaponFactory = null, Func<DraftCard, DraftCard> skillFactory = null)
        {
            draftWave = wave;
            if (weaponFactory != null) weaponCardFactory = weaponFactory;
            if (skillFactory != null) skillCardFactory = skillFactory;
            if (BankedDrafts == 0) return CurrentCards = Array.Empty<DraftCard>();
            if (CurrentCards.Length > 0) { ReplaceOutgrownCards(); return CurrentCards; }
            return CurrentCards = Draw(null);
        }

        public bool Reroll()
        {
            if (RerollsRemaining <= 0 || BankedDrafts == 0) return false;
            RerollsRemaining--; CurrentCards = Draw(CurrentCards); OnChanged(); return true;
        }

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
                StatId? replaced = old?.Payload is StatCardOffer oldStat ? oldStat.Stat : null;
                var offer = RollStatCard(draftStats, shownStats, replaced, forcePenetration: i == 0 && MustOfferPenetration);
                draftStats.Add(offer.Stat); cards[i] = MakeStat(offer);
            }
            shownStats.Clear();
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

        public void ResolvePendingRandom()
        {
            while (BankedDrafts > 0)
            {
                DrawCards(draftWave);
                int eligibleCount = 0;
                foreach (var card in CurrentCards) if (!card.NeedsReplacement) eligibleCount++;
                int pick = UnityEngine.Random.Range(0, eligibleCount);
                bool applied = false;
                for (int i = 0; i < CurrentCards.Length; i++)
                {
                    if (CurrentCards[i].NeedsReplacement) continue;
                    if (pick-- > 0) continue;
                    applied = ApplyCard(i); break;
                }

                if (!applied) ApplyCard(0);
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

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (VoidEnergy && EnergyUnlocked && Energy < Stats.maxEnergy)
                GainEnergy(Rules.voidEnergyRegeneration * dt);
            regenerationClock += dt;
            while (regenerationClock >= Rules.regenerationInterval)
            {
                regenerationClock -= Rules.regenerationInterval;
                if (Stats.regeneration > 0f) OnHealingRequested(Stats.regeneration);
            }
        }

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

        StatCardOffer RollStatCard(List<StatId> taken, List<StatId> shown, StatId? replaced, bool forcePenetration)
        {
            if (CollectEligible(stat => taken.Contains(stat) || shown.Contains(stat), forcePenetration) == 0
                && (replaced == null || CollectEligible(stat => taken.Contains(stat) || stat == replaced, forcePenetration) == 0))
                CollectEligible(taken.Contains, forcePenetration);
            if (eligible.Count == 0) return new StatCardOffer(StatId.MaxHp, CardRarity.Common, 12f);
            var picked = eligible[UnityEngine.Random.Range(0, eligible.Count)];

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
