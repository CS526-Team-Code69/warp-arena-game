using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    [DisallowMultipleComponent]
    public sealed class RunProgression : MonoBehaviour
    {
        [field: SerializeField, Tooltip("All progression candidates from GDD §6–7.")]
        public ProgressionRules Rules { get; set; }
        [field: SerializeField] public RunStats Stats { get; private set; } = new();
        public int Level { get; private set; } = 1;
        public float Xp { get; private set; }
        public int XpRequired => Level >= Rules.maxLevel ? Rules.xpPerStatDraft : Rules.levelCosts[Level - 1];
        public int BankedDrafts => drafts.Count;
        public bool NextDraftIsStatOnly => drafts.Count > 0 && drafts.Peek();
        public float Energy { get; private set; }
        public bool EnergyUnlocked { get; private set; }
        public bool VoidEnergy { get; private set; }
        public bool RainbowBadge => Level >= Rules.maxLevel && VoidEnergy;
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
        float regenerationClock;
        int statsWithoutEpic;
        int draftWave;
        Func<DraftCard> weaponCardFactory;
        Func<DraftCard> skillCardFactory;

        public void ResetRun()
        {
            Stats = Rules.startingStats.Copy(); Stats.maxEnergy = Rules.energyMax;
            Level = 1; Xp = 0f; Energy = 0f; EnergyUnlocked = false; VoidEnergy = false;
            LastLevelHpGain = LastLevelEnergyGain = 0f;
            MayOfferPenetration = MustOfferPenetration = false; regenerationClock = 0f; statsWithoutEpic = 0; drafts.Clear();
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
                drafts.Enqueue(statOnly);
                if (EnergyUnlocked) { Energy = Stats.maxEnergy; OnEnergyRefilled(); }
            }
            OnChanged();
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

            if (LastLevelHpGain > 0f) OnHealingRequested(LastLevelHpGain);
        }

        public void ConsumeDraft()
        {
            if (drafts.Count > 0) drafts.Dequeue();
            CurrentCards = Array.Empty<DraftCard>();
            OnChanged();
        }

        public void BeginBuildPhase(int wave, Func<DraftCard> weaponFactory = null, Func<DraftCard> skillFactory = null)
        {
            RerollsRemaining = 1; draftWave = wave;
            weaponCardFactory = weaponFactory; skillCardFactory = skillFactory;
            CurrentCards = Array.Empty<DraftCard>();
            OnChanged();
        }

        public DraftCard[] DrawCards(int wave, Func<DraftCard> weaponFactory = null, Func<DraftCard> skillFactory = null)
        {
            draftWave = wave;
            if (weaponFactory != null) weaponCardFactory = weaponFactory;
            if (skillFactory != null) skillCardFactory = skillFactory;
            if (BankedDrafts == 0) return CurrentCards = Array.Empty<DraftCard>();
            StatCardOffer first = RollStatCard(forcePenetration: MustOfferPenetration);
            DraftCard middle = NextDraftIsStatOnly ? null : weaponCardFactory?.Invoke();
            DraftCard last = NextDraftIsStatOnly ? null : skillCardFactory?.Invoke();
            StatId? secondStat = null;
            if (middle == null) { var second = RollStatCard(first.Stat); secondStat = second.Stat; middle = MakeStat(second); }
            if (last == null) last = MakeStat(RollStatCard(first.Stat, secondStat));
            return CurrentCards = new[] { MakeStat(first), middle, last };
        }

        public bool Reroll()
        {
            if (RerollsRemaining <= 0 || BankedDrafts == 0) return false;
            RerollsRemaining--; DrawCards(draftWave); OnChanged(); return true;
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
                if (CurrentCards.Length == 0) DrawCards(draftWave);
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
            eligible.Clear();
            foreach (var card in Rules.statCards)
            {
                if (card.retired || card.stat == excludeA || card.stat == excludeB || Stats.AtCap(card.stat)) continue;
                if(forcePenetration&&card.stat!=StatId.ElementPenetration)continue;
                if (!EnergyUnlocked && card.stat is StatId.MaxEnergy or StatId.EnergySiphon) continue;
                if (card.stat == StatId.ElementPenetration && !MayOfferPenetration) continue;
                eligible.Add(card);
            }
            if (eligible.Count == 0) return new StatCardOffer(StatId.MaxHp, CardRarity.Common, 12f);
            var picked = eligible[UnityEngine.Random.Range(0, eligible.Count)];
            float roll = UnityEngine.Random.value * 100f;
            CardRarity rarity = Rules.StatRarityFromRoll(roll, Stats.luck, statsWithoutEpic >= Rules.epicPityCount);
            statsWithoutEpic = rarity is CardRarity.Epic or CardRarity.Legendary ? 0 : statsWithoutEpic + 1;
            float legendary = rarity == CardRarity.Legendary ? Mathf.Max(1f, Rules.legendaryStatMultiplier) : 1f;
            return new StatCardOffer(picked.stat, rarity, picked.At(rarity) * legendary, picked.bonus, picked.BonusAt(rarity) * legendary);
        }

        void OnChanged() => Changed?.Invoke();
        void OnLevelGained(int level) => LevelGained?.Invoke(level);
        void OnEnergyRefilled() => EnergyRefilled?.Invoke();
        void OnHealingRequested(float amount) => HealingRequested?.Invoke(amount);
        void OnStatChanged(StatId stat, float amount) => StatChanged?.Invoke(stat, amount);
    }
}
