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

        bool CampaignSkipsArmory => CampaignRunning && (!runWeapons.HasOffers || !campaign.ArmoryUpgrades || !campaign.ArmoryRoundActive);
    }
}
