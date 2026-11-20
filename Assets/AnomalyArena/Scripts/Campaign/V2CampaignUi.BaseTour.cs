using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>
    /// The guided tour of the base page on the arrival at the end of Day 0 (V2Campaign.BaseTour.cs starts it once the base system has
    /// said B01). In the forced tutorial's style (V2RectSpotlight: the dimmed screen, the bright box with its pulsing ring, the green flash)
    /// it walks the player through the page one click at a time: Central Control's zone, the Armament Bay's zone, building the bay, its
    /// study window opened and closed again, the Energy Lab's zone, building the lab, the Ship Launch Bay's zone, and the way out
    /// (BaseTour holds the steps, their words and their rules). Only the highlighted part takes a click, and the keys that would change
    /// the page do nothing meanwhile (BaseTourRunning: W and S and the arrows on the page, Esc, every key of the study window), so the
    /// alloy and energy Day 0 gives go to the two rooms the tour builds. A step is done once what it asks for has happened, read every
    /// frame (BaseTour.Passed): the page's rows are reused and drawn again, so no step listens to a row's button. Only the click on
    /// Central Control's own zone, whose tab shows already, is heard from its button (zones are built once and never redrawn). A step
    /// whose part is not on the page for a moment, or a room the stock cannot build, is left out rather than holding the player. The tour
    /// ends at once when the base page closes or the run ends. It is drawn on the fifth pass's page only (V5HudKit.On).
    /// The same plate and highlight carry the guide to a first purchase, once a run on a later visit (BuyGuide holds its rules and words,
    /// V2Campaign.TickBuyGuide starts it): the room's zone, then the lamp row, or the study button and the study window's buy button with
    /// the cheapest item the stock pays for chosen. It holds the page's keys as the tour does, and Esc skips it.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // A step whose part stays off the page this long (unscaled seconds) is left out.
        const float TourMissingSeconds = .5f;

        V2RectSpotlight tourView;
        BaseTourStep tourStep = BaseTourStep.None;
        // tourHolding: the page is held for the first step (every click taken, nothing shown); tourShown: the step's box is up;
        // tourPassed: the step is done and its green flash is playing.
        bool tourHolding, tourShown, tourPassed, tourControlClicked;
        float tourMissing;
        UnityAction tourControlListener;
        static readonly Vector3[] tourCorners = new Vector3[4];

        // The guide to a first purchase (BuyGuide): its step, what it leads to and in which room, the purchases made before it began, when
        // it began (unscaled seconds), and the frame in which Esc skipped it (that Esc does nothing else).
        BuyGuideStep guideStep = BuyGuideStep.None;
        BuyGuideAim guideAim;
        CampaignRoom guideRoom;
        int guidePurchases, guideSkipFrame = -1;
        float guideBegunAt;

        /// <summary>The base tour or the guide to a first purchase is on, or the tour holds the page for its first step: only the
        /// highlighted part takes a click, and the keys that would change the page do nothing.</summary>
        public bool BaseTourRunning => tourHolding || tourStep is not (BaseTourStep.None or BaseTourStep.Done) || GuideRunning
            || guideSkipFrame == Time.frameCount;

        bool GuideRunning => guideStep is not (BuyGuideStep.None or BuyGuideStep.Done);

        /// <summary>For automated checks: the guide's step now (None before it, Done after it).</summary>
        public BuyGuideStep BuyGuideStepNow => guideStep;

        /// <summary>The guide to a first purchase can start now: the fifth pass's base page open with nothing over it (no study window, no
        /// question, no weapon card) and neither the tour nor the guide on.</summary>
        public bool BuyGuideCanStart => CanTour && !BaseTourRunning && !StudyOpen && !ConfirmOpen && gm && gm.RunPhase == ArenaUiState.Base;

        /// <summary>For automated checks: the tour's step now (None before the tour, Done after it).</summary>
        public BaseTourStep BaseTourStepNow => tourStep;

        bool CanTour => V5HudKit.On && b5Built && BaseOpen && campaign && campaign.Running;

        /// <summary>Holds the open base page for the tour: every click is taken and nothing is shown until StartBaseTour (while the base
        /// system is still talking, the page's orange row would otherwise spend the alloy the tour builds the Armament Bay with).</summary>
        public void HoldBaseForTour()
        {
            if (tourHolding || BaseTourRunning || !CanTour) return;
            TourView.Seal();
            tourHolding = true;
            KeepTourOnTop();
        }

        /// <summary>Starts the tour on the open base page at its first step. False when it cannot run: the old page (V5HudKit off), the
        /// base page closed, or a tour already on.</summary>
        public bool StartBaseTour()
        {
            if (tourStep is not (BaseTourStep.None or BaseTourStep.Done)) return false;
            if (!CanTour)
            {
                EndBaseTour();
                return false;
            }
            tourHolding = false;
            var control = b5Zones[(int)CampaignRoom.Control].plate.Button;
            tourControlListener ??= () => tourControlClicked = true;
            control.onClick.RemoveListener(tourControlListener);
            control.onClick.AddListener(tourControlListener);
            BeginTourStep(BaseTourStep.Control);
            return tourStep != BaseTourStep.Done;
        }

        /// <summary>Ends the tour, or the guide, at once: its dim, its plate and the backing that takes the clicks go, and the page's keys
        /// work again.</summary>
        public void EndBaseTour()
        {
            if (tourView) tourView.Hide();
            var control = b5Built ? b5Zones[(int)CampaignRoom.Control] : null;
            if (tourControlListener != null && control != null && control.plate) control.plate.Button.onClick.RemoveListener(tourControlListener);
            if (tourStep != BaseTourStep.None) tourStep = BaseTourStep.Done;
            if (guideStep != BuyGuideStep.None) guideStep = BuyGuideStep.Done;
            tourHolding = tourShown = tourPassed = false;
        }

        /// <summary>Starts the guide to a first purchase on the open base page, leading to aim in room (V2Campaign.TickBuyGuide picks
        /// both). False when it cannot run: the old page, the base page closed or covered, the tour or the guide already on, or nothing on
        /// the page to show.</summary>
        public bool StartBuyGuide(BuyGuideAim aim, CampaignRoom room)
        {
            if (aim == BuyGuideAim.None || !BuyGuideCanStart) return false;
            guideAim = aim;
            guideRoom = room;
            guidePurchases = campaign.PurchaseCount;
            guideBegunAt = Time.unscaledTime;
            BeginGuideStep(BuyGuideStep.Zone);
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
                var audio = V3WorldAudio.Instance;
                if (audio) audio.Play(ArenaWorldCue.LevelUp, gm && gm.player ? gm.player.Position : Vector3.zero, 1.3f, .75f);
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
            view.Show(() => TourTarget(step), BaseTour.Text(step), BaseTour.Hint, campaign.rules ? campaign.rules.spotlightDim : .75f);
            tourShown = true;
        }

        /// <summary>Goes on to the first step from step on that has something to show (BaseTour.FirstToRun); past the last one the tour
        /// ends. The box of the step before stays up, still taking every click, until the new step's box slides over from it.</summary>
        void BeginTourStep(BaseTourStep step)
        {
            tourControlClicked = false;
            tourShown = tourPassed = false;
            tourMissing = 0f;
            tourStep = BaseTour.FirstToRun(step, TourState());
            if (tourStep == BaseTourStep.Done) EndBaseTour();
        }

        BaseTourState TourState()
        {
            var c = campaign;
            return new BaseTourState
            {
                tab = tab,
                armamentLevel = c.Room(CampaignRoom.Armament).level,
                energyLevel = c.Room(CampaignRoom.Energy).level,
                armamentBuildable = TourCanBuild(c, CampaignRoom.Armament),
                energyBuildable = TourCanBuild(c, CampaignRoom.Energy),
                studyOpen = StudyOpen,
                baseOpen = BaseOpen,
                controlClicked = tourControlClicked
            };
        }

        // ───── The guide to a first purchase ─────

        /// <summary>Every frame while the guide runs (TickBaseTour). Esc skips it. Otherwise, as in the tour, a done step's flash plays out
        /// and the next step begins, the step's box goes up once its part is on the page (or the step is left out when the part stays away),
        /// and the step is done once what it asks for has happened. Before the buy step's box goes up, the cheapest item the stock pays for
        /// is chosen, so the buy button buys it.</summary>
        void TickBuyGuide()
        {
            var keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame && Time.unscaledTime - guideBegunAt > .2f)
            {
                guideSkipFrame = Time.frameCount; // this Esc neither closes the study window nor leaves the base
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
                var audio = V3WorldAudio.Instance;
                if (audio) audio.Play(ArenaWorldCue.LevelUp, gm && gm.player ? gm.player.Position : Vector3.zero, 1.3f, .75f);
                return;
            }
            if (!tourShown && guideStep == BuyGuideStep.Buy) ChooseCheapestStudyTile();
            if (GuideTarget(guideStep) == null)
            {
                tourMissing += Time.unscaledDeltaTime;
                if (tourMissing >= TourMissingSeconds) BeginGuideStep(BuyGuide.After(guideStep));
                return;
            }
            tourMissing = 0f;
            if (tourShown) return;
            var step = guideStep;
            view.Show(() => GuideTarget(step), BuyGuide.Text(step, guideAim, guideRoom), BuyGuide.Hint, campaign.rules ? campaign.rules.spotlightDim : .75f);
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

        BuyGuideState GuideState() => new()
        {
            aim = guideAim,
            room = guideRoom,
            tab = tab,
            studyOpen = StudyOpen,
            baseOpen = BaseOpen,
            bought = campaign && campaign.PurchaseCount > guidePurchases
        };

        /// <summary>Where a guide step's part is on the screen now, or null while it is not on the page: the room's zone, the launch bay's
        /// lamp row, the room's study button, the study window's buy button while it can buy.</summary>
        Rect? GuideTarget(BuyGuideStep step) => step switch
        {
            BuyGuideStep.Zone => TourZone(guideRoom),
            BuyGuideStep.Lamp => TourRow(CampaignRoom.Ship, B5Deed.Lamp),
            BuyGuideStep.OpenStudy => TourRow(guideRoom, B5Deed.Study),
            BuyGuideStep.Buy => StudyOpen && s5Built && s5Go && s5Go.gameObject.activeInHierarchy && s5Go.Button.interactable ? TourRect(s5Go.Rect) : null,
            _ => null
        };

        /// <summary>Chooses the cheapest item of the open study tab that the stock pays for now (the buy button buys the chosen item).</summary>
        void ChooseCheapestStudyTile()
        {
            var shown = CurrentStudyTab;
            if (shown == null || shown.locked) return;
            int best = -1, bestCost = int.MaxValue;
            for (int i = 0; i < shown.entries.Count; i++)
            {
                var entry = shown.entries[i];
                if (entry.state != StudyState.Available || entry.price is not { } price) continue;
                int cost = price.alloy + price.energy + price.dark;
                if (cost < bestCost)
                {
                    best = i;
                    bestCost = cost;
                }
            }
            if (best < 0 || best == studySelected) return;
            studySelected = best;
            studyEntryId = shown.entries[best].id;
            StyleTiles();
            ScrollToSelected();
        }

        /// <summary>The room's next level can be bought now, as its button would: Central Control works, the room is whole and the stock
        /// pays for it.</summary>
        static bool TourCanBuild(V2Campaign c, CampaignRoom room) =>
            c.NextLevelCost(room) is { } cost && c.ControlWorks && !c.Room(room).damaged && c.CanAfford(cost);

        // ───── Where each step's part is ─────

        /// <summary>Where a step's part is on the screen now (canvas units from the bottom-left corner), or null while it is not on the
        /// page: a zone is its plate with the hexagon beside it, a room's build or study is its row of the room's tab, the study window's
        /// is its close button, the way out is the orange go button.</summary>
        Rect? TourTarget(BaseTourStep step) => step switch
        {
            BaseTourStep.Control => TourZone(CampaignRoom.Control),
            BaseTourStep.ArmamentZone => TourZone(CampaignRoom.Armament),
            BaseTourStep.EnergyZone => TourZone(CampaignRoom.Energy),
            BaseTourStep.ShipZone => TourZone(CampaignRoom.Ship),
            BaseTourStep.BuildArmament => TourRow(CampaignRoom.Armament, B5Deed.Build),
            BaseTourStep.OpenStudy => TourRow(CampaignRoom.Armament, B5Deed.Study),
            BaseTourStep.BuildEnergy => TourRow(CampaignRoom.Energy, B5Deed.Build),
            BaseTourStep.CloseStudy => StudyOpen && s5Built && s5Close ? TourRect(s5Close.Rect) : null,
            BaseTourStep.Leave => TourGoWay(),
            _ => null
        };

        /// <summary>A zone's plate and the hexagon left of it (a click on either picks the zone); not while the study window covers them.</summary>
        Rect? TourZone(CampaignRoom room)
        {
            var zone = StudyOpen ? null : b5Zones[(int)room];
            if (zone == null || !zone.plate) return null;
            return TourRect(zone.plate.Rect, zone.frame ? zone.frame.rectTransform.parent as RectTransform : null);
        }

        /// <summary>The shown row of the room's tab that does this (B5DeedOf), with its button live.</summary>
        Rect? TourRow(CampaignRoom room, B5Deed deed)
        {
            if (StudyOpen || tab != room) return null;
            foreach (var row in b5Rows)
                if (row.deed == deed && row.act != null && row.root && row.root.gameObject.activeInHierarchy) return TourRect(row.root);
            return null;
        }

        /// <summary>The orange go button among the ways out (on Day 0's arrival the only one: out into the night).</summary>
        Rect? TourGoWay()
        {
            if (StudyOpen) return null;
            foreach (var way in b5Ways)
                if (way.go && way.act != null && way.plate && way.plate.gameObject.activeInHierarchy) return TourRect(way.plate.Rect);
            return null;
        }

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

        /// <summary>The tour's layer stands right over the base page, the study window and the purchase flash; the subtitles, the CG
        /// screens and a yes / no question stay over it. The study window is put back over the base page each time it opens, so the
        /// layer's place is checked every frame.</summary>
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
