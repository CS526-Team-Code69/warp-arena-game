using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The run flow's side of the V2 day campaign (V2Campaign): what the campaign may ask of the GameManager, kept apart from the classic
    /// flow in GameManagerV2.cs. A scene without a V2Campaign component, or a run started with the classic Start, never reaches these.
    /// </summary>
    public partial class GameManager
    {
        V2Campaign campaign;
        /// <summary>A V2 campaign run is in progress (the title's campaign Start was taken).</summary>
        public bool CampaignRunning => campaign && campaign.Running;

        void CampaignAwake() => campaign = GetComponent<V2Campaign>();

        /// <summary>The campaign's black title card: nothing of the title or the run shows meanwhile.</summary>
        internal void CampaignHoldTitle() => SetPhase(ArenaUiState.Story);

        /// <summary>The crash's supply crate and the first weapon, as in the classic start.</summary>
        internal void CampaignBeginLoadout() => SetPhase(ArenaUiState.Loadout);

        /// <summary>With the starting weapon in hand (since 2026-11-06 the pistol, no card): the run is on, Day 0 walks the map with combat
        /// allowed but no wave running.</summary>
        internal void CampaignEnterPlay()
        {
            State = GameState.Playing;
            BeginWaveDrafts();
            SetPhase(ArenaUiState.WaveActive);
            V2Audio.Instance?.PlayWave();
            RunAnalytics.RunStarted(this);
        }

        /// <summary>A campaign phase (Night, Base, Story, WaveIntro for the lull). A story beat may keep the world's time running.</summary>
        internal void CampaignSetPhase(ArenaUiState phase, float? timeScale = null)
        {
            SetPhase(phase);
            if (timeScale.HasValue) Time.timeScale = timeScale.Value;
        }

        /// <summary>The dawn build: air drops (if the Armament Bay allows), card picks, ready; its start button begins the next day.
        /// Not reached since 2026-11-06: dawn starts the next day at once (CampaignStartDay).</summary>
        internal void CampaignOpenBuild() => OpenBuild();

        /// <summary>The day's next wave after the lull.</summary>
        internal void CampaignNextWave() => BeginNextV2Wave(false);

        /// <summary>After the failed ignition: the Tyrant, the existing final boss.</summary>
        internal void CampaignFinalBoss()
        {
            player.Heal(player.maxHp); player.stamina.Refill(); player.ClearWaveState(); BeginWaveDrafts();
            SetPhase(ArenaUiState.FinalBoss);
            waves.BeginFinalBoss();
        }

        /// <summary>The ending is over: the run is won (the result screen follows at the frame's end).</summary>
        internal void CampaignWin() => finalWaveCleared = true;

        /// <summary>V2 day campaign: dawn starts the next day at once (no build phase since 2026-11-06), the way BeginNextV2Wave starts a
        /// campaign day: full HP and stamina, the last wave's state cleared, a fresh reroll, then the day's first wave.</summary>
        internal void CampaignStartDay()
        {
            if (!CampaignRunning) return;
            snapshot.earlyStartConfirmation = false; pendingDraft = pendingOffer = -1;
            player.Heal(player.maxHp); player.stamina.Refill(); player.ClearWaveState(); BeginWaveDrafts();
            SetPhase(ArenaUiState.WaveIntro);
            campaign.StartNextWave();
            V2Audio.Instance?.PlayWave();
        }

        /// <summary>V2 day campaign: one upgrade bought at the base takes effect at once (one random eligible card of the kinds the campaign
        /// allows at this moment: StatCardsAllowed, RunWeapons.AxisAllowed, SkillCard); returns the title of what it gave, or null when
        /// nothing could be given, and then the pick is taken back so nothing is left waiting. Only a card that needs no replacement
        /// prompt is taken (a new skill with all three slots full needs one). It does not call RunProgression.ResolvePendingRandom: with
        /// the stat cards locked and every card left needing a prompt, that loop draws the same card again and never ends.
        /// A pick already waiting with its cards drawn (since 2026-11-06 the void trial's extra level) is set aside meanwhile: DrawCards
        /// would hand back those cards, so the purchase would apply one of them instead of its own kind. They come back afterwards.</summary>
        internal string CampaignApplyUpgrade()
        {
            if (!CampaignRunning) return null;
            UpdatePenetrationEligibility();
            var waiting = progression.SetCardsAside();
            try
            {
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
                progression.ConsumeDraft(); // nothing could be given: the pick is taken back
                return null;
            }
            finally { progression.PutCardsBack(waiting); }
        }

        /// <summary>V2 day campaign: the weapon card of a weapon just bought at the base (since 2026-11-06 an anomaly weapon shows its card the
        /// moment it is bought, as a weapon taken in the classic run does); then runs once the card is dismissed (the campaign puts its own
        /// phase back there). Outside a campaign run, or with no weapon, then runs at once.</summary>
        internal void CampaignShowWeaponCard(RunWeapon weapon, System.Action then)
        {
            if (!CampaignRunning || weapon == null) { then?.Invoke(); return; }
            ShowWeaponIntro(weapon, then);
        }

        /// <summary>The dawn build's weapon offers: since 2026-11-06 the campaign has no free air drops, only the weapon research bought at
        /// the Armament Bay (one research is one round of offers). True when no round is due, so the build goes straight to the card picks.</summary>
        bool CampaignSkipsArmory => CampaignRunning && (!runWeapons.HasOffers || !campaign.ArmoryUpgrades || !campaign.ArmoryRoundActive);
    }
}
