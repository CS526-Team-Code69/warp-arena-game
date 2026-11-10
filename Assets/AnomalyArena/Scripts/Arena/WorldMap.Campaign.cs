using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The V2 day campaign's map on the arena (since 2026-11-06). ApplyCampaignLayout swaps in the campaign's larger layout with its
    /// terrain (CampaignTerrain) when a campaign run starts: the bounds and everything that reads them (camera, spawns, regions, the night's
    /// darkness), the path grid and the raiders' flow field, the wall objects (a new group copied from the map's saved walls, which hide
    /// meanwhile), the quicksand, the floor's size, the invisible boundary walls, and the void trial's platform, which moves out as far as
    /// the floor grew so the trial still floats in the void. One black hole stands far out (the campaign's); the other is put away.
    /// RestoreLayout gives the classic map back. In between the campaign steers its black hole through the hooks below: when the
    /// first-sight cutscene may play, when E may take the hidden gate and the question asked before it, the side quest's frame, and the
    /// night, when the hole is hidden (SetCampaignHoleHidden). A classic run never calls any of this; every classic rule stays as it was.
    /// </summary>
    public sealed partial class WorldMap
    {
        MapLayout classicLayout, campaignLayout;
        BlackHole[] classicHoles;
        BlackHole campaignHole, pendingCampaignEntry;
        int classicFeedsToOpen, wallGroupIndex = -1;
        Transform classicWallGroup, campaignWallGroup;
        Vector3 classicFloorScale, classicTrialPosition;
        readonly List<(Transform part, Vector3 position, Vector3 scale)> classicBoundary = new();
        bool campaignHoleHidden, campaignAsking;
        float campaignAskedAt;

        /// <summary>The V2 campaign's map is in place (from ApplyCampaignLayout to RestoreLayout).</summary>
        public bool CampaignMapApplied { get; private set; }
        /// <summary>The campaign's one black hole while its map is in place (also while it is hidden at night), else null.</summary>
        public BlackHole CampaignHole => CampaignMapApplied ? campaignHole : null;
        /// <summary>The campaign's black hole is put away for the night: unseen, it pulls, swallows, counts and opens nothing.</summary>
        public bool CampaignHoleHidden => CampaignMapApplied && campaignHoleHidden;
        /// <summary>The map's own saved layout, also while the campaign's stands in for it.</summary>
        public MapLayout ClassicLayout => CampaignMapApplied ? classicLayout : layout;
        /// <summary>The map hole the campaign takes for its own (the first, the large one).</summary>
        public BlackHole PrimaryHole => CampaignMapApplied ? campaignHole : holes != null && holes.Length > 0 ? holes[0] : null;
        /// <summary>The map has a saved wall group to copy the campaign's terrain walls from.</summary>
        public bool HasWallTemplates => SavedWallGroup() is { childCount: > 0 };
        /// <summary>The first-sight cutscene has played (or found no room for its three fodder): never again in this run.</summary>
        public bool HoleIntroDone => holeIntroStage == HoleIntroStage.Done;
        /// <summary>The first-sight cutscene is over and play is handed back (or it could not play at all).</summary>
        public event Action HoleIntroFinished;
        /// <summary>The base, the ship pad and any other walls the campaign stands on the map (SetExtraWalls); MapGrid stamps them.</summary>
        internal List<Rect> ExtraWallRects => extraWalls;

        // Hooks the V2 campaign fills while its map is in place; RestoreLayout clears them.
        /// <summary>Called every frame (Update) while the campaign's map is in place.</summary>
        internal Action campaignTick;
        /// <summary>Whether the first-sight cutscene may start now (by day or in the afternoon, the hole out, the player free).</summary>
        internal Func<bool> campaignHoleIntroAllowed;
        /// <summary>Whether E may take the hidden gate now (by day or in the afternoon); the standing rules of CanEnterPortal still apply.</summary>
        internal Func<bool> campaignPortalAllowed;
        /// <summary>Asks before the hidden gate takes the player: the hole, then what a yes and a no do.</summary>
        internal Action<BlackHole, Action, Action> campaignConfirmPortal;

        bool CampaignPortalOpenNow => !campaignHoleHidden && !campaignAsking && !pendingCampaignEntry && campaignPortalAllowed != null && campaignPortalAllowed();

        Transform SavedWallGroup()
        {
            int index = biomes != null ? Array.IndexOf(biomes, biome) : -1;
            return index >= 0 && biomeWallGroups != null && index < biomeWallGroups.Length ? biomeWallGroups[index] : null;
        }

        /// <summary>
        /// V2 day campaign: the arena becomes the campaign's map. campaign is its layout (CampaignTerrain.Build; this map owns it from now
        /// and destroys it with RestoreLayout); the first map hole moves to holeAt, opens its hidden gate after feedsToOpen monsters and is
        /// the only hole in play; the other is put away. Called once as the campaign begins, before its base is built
        /// (V2Campaign.SetupCampaignMap).
        /// </summary>
        public void ApplyCampaignLayout(MapLayout campaign, Vector3 holeAt, int feedsToOpen)
        {
            if (!campaign) return;
            if (CampaignMapApplied) RestoreLayout();
            // A Tyrant fight's runtime copy goes first, so the layout saved here is the map's own.
            if (TryGetComponent(out TyrantTerrain terrain) && terrain.Changed) terrain.ResetTerrain();
            classicLayout = layout;
            campaignLayout = campaign;

            // The black holes: the first is the campaign's, far out; the rest wait out of play until RestoreLayout.
            classicHoles = holes;
            campaignHole = holes != null && holes.Length > 0 ? holes[0] : null;
            for (int i = 1; holes != null && i < holes.Length; i++) if (holes[i]) holes[i].gameObject.SetActive(false);
            if (campaignHole)
            {
                classicFeedsToOpen = campaignHole.feedsToOpen;
                campaignHole.feedsToOpen = Mathf.Max(1, feedsToOpen);
                campaignHole.transform.position = new Vector3(holeAt.x, 0f, holeAt.z);
                campaignHole.gameObject.SetActive(true);
                campaignHole.ResetHole();
            }
            holes = campaignHole ? new[] { campaignHole } : Array.Empty<BlackHole>();
            campaignHoleHidden = campaignAsking = false;
            pendingCampaignEntry = null;

            layout = campaign;
            CampaignMapApplied = true;
            Grid = new MapGrid(campaign.width, campaign.height);
            lastCell = -1;
            // The cutscene of the first black hole seen belongs to the campaign's hole now.
            holeIntroStage = HoleIntroStage.Idle;
            holeIntroFodder.Clear();
            holeIntroHole = null;

            // The terrain's wall objects: a group of copies of the saved walls stands in for the saved group, under the same index, so
            // the Tyrant's broken walls (TyrantTerrain) and the floor's shadows (V2BiomeArt) find it.
            wallGroupIndex = biomes != null ? Array.IndexOf(biomes, biome) : -1;
            classicWallGroup = SavedWallGroup();
            if (classicWallGroup && classicWallGroup.childCount > 0)
            {
                int templates = classicLayout && classicLayout.walls != null && classicLayout.walls.Length > 0
                    ? Mathf.Min(classicLayout.walls.Length, classicWallGroup.childCount) : classicWallGroup.childCount;
                campaignWallGroup = LayoutWalls.Build(classicWallGroup, templates, campaign, " (campaign)");
                classicWallGroup.gameObject.SetActive(false);
                if (campaignWallGroup) biomeWallGroups[wallGroupIndex] = campaignWallGroup;
            }

            ResizeFloor(campaign);
            MoveBoundary(campaign);
            MoveTrialPlatform(campaign);
            Quicksand.Build(this);
            Physics.SyncTransforms();
            HoleChanged();
        }

        /// <summary>The classic map comes back: its layout, walls, floor, boundary, trial platform and both black holes on fresh anchors.
        /// The campaign's hooks and its base and ship walls go. Safe to call when no campaign map is in place.</summary>
        public void RestoreLayout()
        {
            if (!CampaignMapApplied) return;
            if (TrialActive) AbortTrial(); // a run that ends in the void trial: the battlefield comes back first
            if (TryGetComponent(out TyrantTerrain terrain) && terrain.Changed) terrain.ResetTerrain();
            ClearCampaignHooks();
            CampaignMapApplied = false;
            pendingCampaignEntry = null;
            campaignHoleHidden = campaignAsking = false;
            layout = classicLayout;

            if (wallGroupIndex >= 0 && biomeWallGroups != null && wallGroupIndex < biomeWallGroups.Length && classicWallGroup) biomeWallGroups[wallGroupIndex] = classicWallGroup;
            if (campaignWallGroup)
            {
                campaignWallGroup.gameObject.SetActive(false);
                Destroy(campaignWallGroup.gameObject);
            }
            campaignWallGroup = null;
            if (classicWallGroup) classicWallGroup.gameObject.SetActive(true);

            if (floorRenderer) floorRenderer.transform.localScale = classicFloorScale;
            foreach (var (part, position, scale) in classicBoundary)
                if (part) { part.localPosition = position; part.localScale = scale; }
            classicBoundary.Clear();
            if (trialPlatform) trialPlatform.position = classicTrialPosition;

            if (campaignHole) campaignHole.feedsToOpen = classicFeedsToOpen;
            holes = classicHoles ?? holes;
            foreach (var hole in holes) if (hole) hole.gameObject.SetActive(true);
            PlaceHolesAtAnchors();
            campaignHole = null;
            if (campaignLayout && campaignLayout != classicLayout) Destroy(campaignLayout);
            campaignLayout = null;

            extraWalls.Clear(); // the base and the ship pad were the campaign's
            Grid = new MapGrid(layout ? layout.width : 100, layout ? layout.height : 70);
            lastCell = -1;
            holeIntroStage = HoleIntroStage.Idle;
            holeIntroFodder.Clear();
            holeIntroHole = null;
            Quicksand.Build(this);
            Physics.SyncTransforms();
            HoleChanged();
        }

        internal void ClearCampaignHooks()
        {
            campaignTick = null;
            campaignHoleIntroAllowed = campaignPortalAllowed = null;
            campaignConfirmPortal = null;
            HoleIntroFinished = null;
        }

        /// <summary>OnDestroy: the campaign's runtime layout goes with the map.</summary>
        void ForgetCampaignMap()
        {
            ClearCampaignHooks();
            if (campaignLayout && campaignLayout != classicLayout) Destroy(campaignLayout);
            campaignLayout = null;
        }

        /// <summary>The floor grows with the map, keeping the margin it has round the saved layout (the view never sees its edge).</summary>
        void ResizeFloor(MapLayout campaign)
        {
            if (!floorRenderer) return;
            var floor = floorRenderer.transform;
            classicFloorScale = floor.localScale;
            float marginX = classicLayout ? Mathf.Max(0f, classicFloorScale.x - classicLayout.width) : 0f;
            float marginZ = classicLayout ? Mathf.Max(0f, classicFloorScale.z - classicLayout.height) : 0f;
            floor.localScale = new Vector3(Mathf.Max(classicFloorScale.x, campaign.width + marginX), classicFloorScale.y, Mathf.Max(classicFloorScale.z, campaign.height + marginZ));
        }

        /// <summary>The scene's invisible walls round the map (the "Boundary" group beside the floor) move out to the campaign map's
        /// edge, each the same distance outside it as round the saved map, and grow along it.</summary>
        void MoveBoundary(MapLayout campaign)
        {
            classicBoundary.Clear();
            var boundary = floorRenderer && floorRenderer.transform.parent ? floorRenderer.transform.parent.Find("Boundary") : null;
            if (!boundary || !classicLayout) return;
            float growX = (campaign.width - classicLayout.width) * .5f, growZ = (campaign.height - classicLayout.height) * .5f;
            foreach (Transform part in boundary)
            {
                Vector3 p = part.localPosition, s = part.localScale;
                classicBoundary.Add((part, p, s));
                if (Mathf.Abs(p.z) >= Mathf.Abs(p.x)) // north or south: further out in z, longer in x
                {
                    part.localPosition = new Vector3(p.x, p.y, p.z + Mathf.Sign(p.z) * growZ);
                    part.localScale = new Vector3(s.x + growX * 2f, s.y, s.z);
                }
                else // east or west
                {
                    part.localPosition = new Vector3(p.x + Mathf.Sign(p.x) * growX, p.y, p.z);
                    part.localScale = new Vector3(s.x, s.y, s.z + growZ * 2f);
                }
            }
        }

        /// <summary>The void trial's platform lies off the map; it moves out as far as the map grew on its side.</summary>
        void MoveTrialPlatform(MapLayout campaign)
        {
            if (!trialPlatform) return;
            classicTrialPosition = trialPlatform.position;
            if (!classicLayout) return;
            Vector3 at = classicTrialPosition;
            float growX = Mathf.Max(0f, (campaign.width - classicLayout.width) * .5f), growZ = Mathf.Max(0f, (campaign.height - classicLayout.height) * .5f);
            if (Mathf.Abs(at.x) > classicLayout.width * .5f) at.x += Mathf.Sign(at.x) * growX;
            if (Mathf.Abs(at.z) > classicLayout.height * .5f) at.z += Mathf.Sign(at.z) * growZ;
            trialPlatform.position = at;
        }

        /// <summary>V2 campaign night: the campaign's black hole goes out of play (unseen, no pull, no swallowing, no count, no gate) and
        /// comes back at dawn with its count and its gate as they were. While the void trial runs nothing changes (the hole is the way
        /// back); the campaign asks again every frame, so it follows once the trial is over.</summary>
        public void SetCampaignHoleHidden(bool hidden)
        {
            if (!CampaignMapApplied || !campaignHole || campaignHoleHidden == hidden || TrialActive) return;
            campaignHoleHidden = hidden;
            if (hidden) { pendingCampaignEntry = null; campaignAsking = false; }
            holes = hidden ? Array.Empty<BlackHole>() : new[] { campaignHole };
            campaignHole.gameObject.SetActive(!hidden);
            HoleChanged();
        }

        /// <summary>After a won void trial: the campaign's hole closes even while it is put away for the night (RemoveHolesAfterTrial).</summary>
        void CloseCampaignHoleAfterTrial()
        {
            if (CampaignMapApplied && campaignHole && campaignHole.Phase != HolePhase.Gone) campaignHole.Close();
        }

        /// <summary>The first-sight cutscene's fodder on the campaign's map: born outside any wave (in the afternoon none runs), giving
        /// nothing, never raiding.</summary>
        Enemy SpawnCampaignIntroFodder(Vector3 at)
        {
            var body = GM.waves.SpawnCampaign(EnemyKind.Grunt, at);
            if (!body) return null;
            body.SuppressRewards = true;
            body.Raider = false;
            return body;
        }

        /// <summary>E on the hidden gate: the campaign asks first. Its yes enters on a later frame, once the question is closed and play goes
        /// on (TickCampaignWorld); its no leaves the gate open.</summary>
        void AskCampaignPortal(BlackHole hole)
        {
            campaignAsking = true;
            campaignAskedAt = Time.unscaledTime;
            campaignConfirmPortal(hole, () => { campaignAsking = false; pendingCampaignEntry = hole; }, () => campaignAsking = false);
        }

        /// <summary>Every frame while the campaign's map is in place: a confirmed entry, then the campaign's own frame (its side quest).</summary>
        void TickCampaignWorld()
        {
            bool playing = Time.timeScale > 0f && GM.RunPhase is ArenaUiState.WaveActive or ArenaUiState.Night;
            // A question closed without an answer (a stage change took it away) does not hold E back for ever.
            if (campaignAsking && playing && Time.unscaledTime - campaignAskedAt > .25f) campaignAsking = false;
            if (pendingCampaignEntry) TickCampaignEntry(playing);
            campaignTick?.Invoke();
        }

        void TickCampaignEntry(bool playing)
        {
            var hole = pendingCampaignEntry;
            var player = GM.player;
            if (!player || !player.IsAlive || TrialActive || TrialWon || campaignHoleHidden || hole.Phase != HolePhase.Portal) { pendingCampaignEntry = null; return; }
            if (!playing) return; // the question is still up
            pendingCampaignEntry = null;
            // Still on the gate and free to act, as CanEnterPortal asked when E was pressed.
            if (player.State != CharacterState.Normal || player.ActionsBlocked || Query.Flat(player.Position - hole.Centre).magnitude > 1.2f) return;
            enteredHole = hole;
            BeginTrial(player, hole.Centre, false);
        }

        /// <summary>
        /// V2 day campaign: the player fell in the void trial at a time the campaign catches deaths (the afternoon), so the run goes on and
        /// the trial ends without a win: the Nash Prophet, its summons and their warnings go, the battlefield comes back as it was and the
        /// body is laid where the player went in, as it is (the campaign's death rule wakes the player). No reward; the hidden gate stays
        /// open for another try. GameManager.CampaignAbortVoidTrial calls it and thaws its own side; RestoreLayout calls it when a run ends
        /// inside the trial.
        /// </summary>
        internal void AbortTrial()
        {
            if (!TrialActive) return;
            if (GM.waves.preparedEnemies != null)
                foreach (var enemy in GM.waves.preparedEnemies) if (enemy && enemy.CorpseActive && enemy.IsTrialSummon) enemy.DespawnCorpse(enemy.SpawnVersion);
            GM.waves.ClearTrialSummons(); // the Nash Prophet is a trial summon too: it leaves the living without a kill
            // Summon warnings still out on the trial's ground would hatch on the battlefield once the wave runs again.
            if (GM.waves.preparedMarkers != null && trialPlatform)
            {
                float reach = TrialGroundReach + 6f;
                foreach (var marker in GM.waves.preparedMarkers)
                    if (marker && marker.gameObject.activeSelf && Query.Flat(marker.transform.position - trialPlatform.position).magnitude <= reach)
                        marker.gameObject.SetActive(false);
            }
            if (hollow) { hollow.ClearBossMarks(); hollow.gameObject.SetActive(false); }
            HideBossHoles();
            HideTrialTerrain();
            foreach (var wall in trialWalls) if (wall) wall.gameObject.SetActive(false);
            trialWalls.Clear();
            foreach (var wall in temporaryWalls) if (wall) wall.BattlefieldFrozen = false;
            if (trialDecoy) trialDecoy.gameObject.SetActive(false);
            if (trialTemporaryHole) trialTemporaryHole.gameObject.SetActive(false);
            if (trialExtraTemporaryHoles != null) foreach (var hole in trialExtraTemporaryHoles) if (hole) hole.gameObject.SetActive(false);
            TrialActive = false;
            if (GM.waves.preparedBolts != null)
                foreach (var bolt in GM.waves.preparedBolts)
                    if (bolt && bolt.gameObject.activeSelf && !frozen.Contains(bolt.gameObject)) bolt.gameObject.SetActive(false);
            foreach (var go in frozen) if (go) go.SetActive(true);
            frozen.Clear();
            GM.waves.ResumeAfterTrial();
            var player = GM.player;
            if (player)
            {
                Vector3 at = new(returnPoint.x, 0f, returnPoint.z);
                var body = player.GetComponent<Rigidbody>();
                if (body)
                {
                    if (!body.isKinematic) body.linearVelocity = Vector3.zero;
                    body.position = at;
                }
                player.transform.position = at;
            }
            if (trialPlatform) trialPlatform.gameObject.SetActive(false);
            Physics.SyncTransforms();
            HoleChanged();
        }
    }
}
