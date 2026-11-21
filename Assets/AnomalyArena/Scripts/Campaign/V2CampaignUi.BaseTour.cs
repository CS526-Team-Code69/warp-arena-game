using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>
    /// The guided tour of the base on the arrival at the end of Day 0 (V2Campaign.BaseTour.cs starts it once the base system has finished
    /// talking). In the forced tutorial's style (V2RectSpotlight: the dimmed screen, the bright box with its pulsing ring, the green
    /// flash) it walks the player through four steps, one sentence each, and goes on only once the player has done what the step asks:
    /// learn a skill on the workshop's skills tab, raise the weapon Mk, open the rocket page from its button, and head out (BaseTour holds
    /// the steps, their words and their rules). Day 0 pays for both purchases. Only the highlighted part takes a click, and the page's keys
    /// do nothing meanwhile (BaseTourRunning), so Day 0's alloy and energy go where the tour shows. A step is done once what it asks for
    /// has happened, read every frame (BaseTour.Passed). A step whose part is not on the page for a moment, or that the stock cannot pay
    /// for, is left out rather than holding the player. The tour ends at once when the base closes or the run ends. It is drawn on the
    /// fifth pass's pages only (V5HudKit.On).
    /// The same plate and highlight carry Day 1's guide, once a run on that day's first visit (BuyGuide holds its rule and words,
    /// V2Campaign.TickBuyGuide starts it): one step at the workshop's button to the rocket page, where the night's mining lamps are made.
    /// It holds the page's keys as the tour does, and Esc skips it.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // A step whose part stays off the page this long (unscaled seconds) is left out.
        const float TourMissingSeconds = .5f;

        V2RectSpotlight tourView;
        BaseTourStep tourStep = BaseTourStep.None;
        // tourHolding: the page is held for the first step (every click taken, nothing shown); tourShown: the step's box is up;
        // tourPassed: the step is done and its green flash is playing.
        bool tourHolding, tourShown, tourPassed;
        float tourMissing;
        // What the tour's purchases are measured against: the skills held and the weapon Mk when the tour began.
        int tourSkillsAtStart, tourMkAtStart;
        static readonly Vector3[] tourCorners = new Vector3[4];

        // Day 1's guide: its step, when it began (unscaled seconds), and the frame in which Esc skipped it (that Esc does nothing else).
        BuyGuideStep guideStep = BuyGuideStep.None;
        int guideSkipFrame = -1;
        float guideBegunAt;

        /// <summary>The base tour or Day 1's guide is on, or the tour holds the page for its first step: only the highlighted part takes a
        /// click, and the keys that would change the page do nothing.</summary>
        public bool BaseTourRunning => tourHolding || tourStep is not (BaseTourStep.None or BaseTourStep.Done) || GuideRunning
            || guideSkipFrame == Time.frameCount;

        bool GuideRunning => guideStep is not (BuyGuideStep.None or BuyGuideStep.Done);

        /// <summary>For automated checks: the guide's step now (None before it, Done after it).</summary>
        public BuyGuideStep BuyGuideStepNow => guideStep;

        /// <summary>Day 1's guide can start now: the fifth pass's base page open with nothing over it (no question, no weapon card) and
        /// neither the tour nor the guide on.</summary>
        public bool BuyGuideCanStart => CanTour && !BaseTourRunning && !StudyOpen && !ConfirmOpen && gm && gm.RunPhase == ArenaUiState.Base;

        /// <summary>For automated checks: the tour's step now (None before the tour, Done after it).</summary>
        public BaseTourStep BaseTourStepNow => tourStep;

        bool CanTour => V5HudKit.On && b5Built && BaseOpen && campaign && campaign.Running;

        /// <summary>Holds the open base page for the tour: every click is taken and nothing is shown until StartBaseTour (while the base
        /// system is still talking, a purchase could otherwise spend what the tour's steps are paid with).</summary>
        public void HoldBaseForTour()
        {
            if (tourHolding || BaseTourRunning || !CanTour) return;
            TourView.Seal();
            tourHolding = true;
            KeepTourOnTop();
        }

        /// <summary>Starts the tour on the open base page at its first step, on the workshop's skills tab. False when it cannot run: the
        /// old page (V5HudKit off), the base page closed, or a tour already on.</summary>
        public bool StartBaseTour()
        {
            if (tourStep is not (BaseTourStep.None or BaseTourStep.Done)) return false;
            if (!CanTour)
            {
                EndBaseTour();
                return false;
            }
            tourHolding = false;
            tourSkillsAtStart = SkillsHeld;
            tourMkAtStart = campaign.TeamMk;
            if (b5Screen != V2Campaign.BaseScreen.Workshop) campaign.RequestBaseScreen(V2Campaign.BaseScreen.Workshop);
            ShowResearchTab("eng.skill");
            BeginTourStep(BaseTourStep.Skill);
            return tourStep != BaseTourStep.Done;
        }

        /// <summary>Ends the tour, or the guide, at once: its dim, its plate and the backing that takes the clicks go, and the page's keys
        /// work again.</summary>
        public void EndBaseTour()
        {
            if (tourView) tourView.Hide();
            if (tourStep != BaseTourStep.None) tourStep = BaseTourStep.Done;
            if (guideStep != BuyGuideStep.None) guideStep = BuyGuideStep.Done;
            tourHolding = tourShown = tourPassed = false;
        }

        /// <summary>Starts Day 1's guide on the open base page (V2Campaign.TickBuyGuide asks on that day's first visit; room is not read
        /// any more). False when it cannot run: the old page, the base page closed or covered, the tour or the guide already on, nothing to
        /// lead to, or the rocket page shown already.</summary>
        public bool StartBuyGuide(BuyGuideAim aim, CampaignRoom room)
        {
            if (aim == BuyGuideAim.None || !BuyGuideCanStart) return false;
            guideBegunAt = Time.unscaledTime;
            BeginGuideStep(BuyGuideStep.Rocket);
            return GuideRunning;
        }

        /// <summary>Every frame (Update). The tour ends with the base page or the run; otherwise a done step's flash plays out and the
        /// next step begins, the step's box goes up once its part is on the page (or the step is left out when the part stays away), and
        /// the step is done once what it asks for has happened, with Day 0's sound for a step done.</summary>
        void TickBaseTour()
        {
            if (!BaseTourRunning) return;
            if (!BaseOpen || !campaign || !campaign.Running || campaign.Stage != CampaignStage.Base)
            {
                EndBaseTour();
                return;
            }
            KeepTourOnTop();
            if (GuideRunning)
            {
                TickBuyGuide();
                return;
            }
            if (tourHolding || tourStep is BaseTourStep.None or BaseTourStep.Done) return;
            var view = TourView;
            if (tourPassed)
            {
                if (!view.Busy) BeginTourStep(BaseTour.After(tourStep));
                return;
            }
            if (tourShown && BaseTour.Passed(tourStep, TourState()))
            {
                tourPassed = true;
                view.Pass();
                PlayTourStepDone();
                return;
            }
            if (TourTarget(tourStep) == null)
            {
                tourMissing += Time.unscaledDeltaTime;
                if (tourMissing >= TourMissingSeconds) BeginTourStep(BaseTour.After(tourStep));
                return;
            }
            tourMissing = 0f;
            if (tourShown) return;
            var step = tourStep;
            // One sentence a step: the plate has no second line.
            view.Show(() => TourTarget(step), BaseTour.Text(step), "", campaign.rules ? campaign.rules.spotlightDim : .75f);
            tourShown = true;
        }

        /// <summary>Goes on to the first step from step on that has something to show (BaseTour.FirstToRun); past the last one the tour
        /// ends. The box of the step before stays up, still taking every click, until the new step's box slides over from it.</summary>
        void BeginTourStep(BaseTourStep step)
        {
            tourShown = tourPassed = false;
            tourMissing = 0f;
            tourStep = BaseTour.FirstToRun(step, TourState());
            if (tourStep == BaseTourStep.Done) EndBaseTour();
        }

        BaseTourState TourState() => new()
        {
            skillBuyable = W5SkillBuyable(),
            skillLearned = SkillsHeld > tourSkillsAtStart,
            mkBuyable = W5MkBuyable(),
            mkRaised = campaign && campaign.TeamMk > tourMkAtStart,
            rocketShown = BaseScreenShown == V2Campaign.BaseScreen.Rocket,
            baseOpen = BaseOpen
        };

        /// <summary>The skills the player holds now (a learned skill takes a slot).</summary>
        int SkillsHeld => gm && gm.skills ? gm.skills.OwnedCount : 0;

        void PlayTourStepDone()
        {
            var audio = V3WorldAudio.Instance;
            if (audio) audio.Play(ArenaWorldCue.LevelUp, gm && gm.player ? gm.player.Position : Vector3.zero, 1.3f, .75f);
        }

        // ───── Day 1's guide ─────

        /// <summary>Every frame while the guide runs (TickBaseTour). Esc skips it. Otherwise, as in the tour, the step is done once the rocket
        /// page shows, and left out when its button stays off the page.</summary>
        void TickBuyGuide()
        {
            var keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame && Time.unscaledTime - guideBegunAt > .2f)
            {
                guideSkipFrame = Time.frameCount; // this Esc does not leave the base
                V5UiSound.Play(V5UiSound.Ui.Back);
                EndBaseTour();
                return;
            }
            var view = TourView;
            if (tourPassed)
            {
                if (!view.Busy) BeginGuideStep(BuyGuide.After(guideStep));
                return;
            }
            if (tourShown && BuyGuide.Passed(guideStep, GuideState()))
            {
                tourPassed = true;
                view.Pass();
                PlayTourStepDone();
                return;
            }
            if (GuideTarget(guideStep) == null)
            {
                tourMissing += Time.unscaledDeltaTime;
                if (tourMissing >= TourMissingSeconds) BeginGuideStep(BuyGuide.After(guideStep));
                return;
            }
            tourMissing = 0f;
            if (tourShown) return;
            var step = guideStep;
            view.Show(() => GuideTarget(step), BuyGuide.Text(step), "", campaign.rules ? campaign.rules.spotlightDim : .75f);
            tourShown = true;
        }

        /// <summary>Goes on to the first step from step on that has something to show (BuyGuide.FirstToRun); past the last one the guide
        /// ends.</summary>
        void BeginGuideStep(BuyGuideStep step)
        {
            tourShown = tourPassed = false;
            tourMissing = 0f;
            guideStep = BuyGuide.FirstToRun(step, GuideState());
            if (guideStep == BuyGuideStep.Done) EndBaseTour();
        }

        BuyGuideState GuideState() => new() { baseOpen = BaseOpen, rocketShown = BaseScreenShown == V2Campaign.BaseScreen.Rocket };

        // ───── Where each step's part is ─────

        /// <summary>Where a step's part is on the screen now (canvas units from the bottom-left corner), or null while it is not on the
        /// page: the workshop's skill cards, the weapon Mk's card, the button to the rocket page, the main way out.</summary>
        Rect? TourTarget(BaseTourStep step) => step switch
        {
            BaseTourStep.Skill => TourRect(W5SkillCards),
            BaseTourStep.WeaponMk => TourRect(W5MkCard),
            BaseTourStep.Rocket => TourRect(B5RocketButton),
            BaseTourStep.Leave => TourRect(B5GoWay),
            _ => null
        };

        Rect? GuideTarget(BuyGuideStep step) => step == BuyGuideStep.Rocket ? TourRect(B5RocketButton) : null;

        /// <summary>The box round one part of the page, or round two, in canvas units from the screen's bottom-left corner (the frame the
        /// tour's spotlight draws in: it covers the whole canvas); null while neither is shown.</summary>
        Rect? TourRect(RectTransform part, RectTransform other = null)
        {
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            bool any = TourCorners(part, ref min, ref max) | TourCorners(other, ref min, ref max);
            return any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : (Rect?)null;
        }

        bool TourCorners(RectTransform part, ref Vector2 min, ref Vector2 max)
        {
            if (!part || !part.gameObject.activeInHierarchy) return false;
            part.GetWorldCorners(tourCorners);
            Vector2 origin = root.rect.min;
            foreach (var corner in tourCorners)
            {
                Vector2 at = (Vector2)root.InverseTransformPoint(corner) - origin;
                min = Vector2.Min(min, at);
                max = Vector2.Max(max, at);
            }
            return true;
        }

        // ───── The tour's layer ─────

        V2RectSpotlight TourView
        {
            get
            {
                if (tourView) return tourView;
                tourView = V2RectSpotlight.Create(root);
                KeepTourOnTop();
                return tourView;
            }
        }

        /// <summary>The tour's layer stands right over the base page and the purchase flash; the subtitles, the CG screens, a yes / no
        /// question and a card's details stay over it. The layer's place is checked every frame.</summary>
        void KeepTourOnTop()
        {
            if (!tourView) return;
            int top = Mathf.Max(baseScreen.GetSiblingIndex(), flashBack.transform.GetSiblingIndex());
            if (studyScreen) top = Mathf.Max(top, studyScreen.GetSiblingIndex());
            var layer = tourView.transform;
            int at = layer.GetSiblingIndex();
            if (at < top) layer.SetSiblingIndex(top);
            else if (at > top + 1) layer.SetSiblingIndex(top + 1);
        }
    }
}
