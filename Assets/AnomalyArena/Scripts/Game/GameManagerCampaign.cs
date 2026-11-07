using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public partial class GameManager
    {
        V2Campaign campaign;

        public bool CampaignRunning => campaign && campaign.Running;

        void CampaignAwake() => campaign = GetComponent<V2Campaign>();

        internal void CampaignHoldTitle() => SetPhase(ArenaUiState.Story);

        internal void CampaignBeginLoadout() => SetPhase(ArenaUiState.Loadout);

        internal void CampaignEnterPlay()
        {
            State = GameState.Playing;
            BeginWaveDrafts();
            SetPhase(ArenaUiState.WaveActive);
            V2Audio.Instance?.PlayWave();
            RunAnalytics.RunStarted(this);
        }

        internal void CampaignSetPhase(ArenaUiState phase, float? timeScale = null)
        {
            SetPhase(phase);
            if (timeScale.HasValue) Time.timeScale = timeScale.Value;
        }

        internal void CampaignOpenBuild() => OpenBuild();

        internal void CampaignNextWave() => BeginNextV2Wave(false);

        internal void CampaignFinalBoss()
        {
            player.Heal(player.maxHp); player.stamina.Refill(); player.ClearWaveState(); BeginWaveDrafts();
            SetPhase(ArenaUiState.FinalBoss);
            waves.BeginFinalBoss();
        }

        internal void CampaignWin() => finalWaveCleared = true;

        internal void CampaignStartDay()
        {
            if (!CampaignRunning) return;
            snapshot.earlyStartConfirmation = false; pendingDraft = pendingOffer = -1;
            player.Heal(player.maxHp); player.stamina.Refill(); player.ClearWaveState(); BeginWaveDrafts();
            SetPhase(ArenaUiState.WaveIntro);
            campaign.StartNextWave();
            V2Audio.Instance?.PlayWave();
        }

        internal string CampaignApplyUpgrade()
        {
            if (!CampaignRunning) return null;
            UpdatePenetrationEligibility();
            progression.BankPick();
            var cards = progression.DrawCards(waves.WaveIndex + 1, WeaponCard, SkillCard);
            var order = new List<int>(cards.Length);
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] != null && !cards[i].NeedsReplacement) order.Insert(Random.Range(0, order.Count + 1), i);
            foreach (int i in order)
            {
                string title = cards[i].Title;
                if (!progression.ApplyCard(i)) continue;
                UpdatePenetrationEligibility();
                return title;
            }
            progression.ConsumeDraft();                                                  
            return null;
        }

        bool CampaignSkipsArmory => CampaignRunning && (!runWeapons.HasOffers || !campaign.ArmoryUpgrades || !campaign.ArmoryRoundActive);
    }
}
