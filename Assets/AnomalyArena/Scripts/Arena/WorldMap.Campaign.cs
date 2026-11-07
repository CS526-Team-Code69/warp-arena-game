using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

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

        public bool CampaignMapApplied { get; private set; }

        public BlackHole CampaignHole => CampaignMapApplied ? campaignHole : null;

        public bool CampaignHoleHidden => CampaignMapApplied && campaignHoleHidden;

        public MapLayout ClassicLayout => CampaignMapApplied ? classicLayout : layout;

        public BlackHole PrimaryHole => CampaignMapApplied ? campaignHole : holes != null && holes.Length > 0 ? holes[0] : null;

        public bool HasWallTemplates => SavedWallGroup() is { childCount: > 0 };

        public bool HoleIntroDone => holeIntroStage == HoleIntroStage.Done;

        public event Action HoleIntroFinished;

        internal List<Rect> ExtraWallRects => extraWalls;

        internal Action campaignTick;

        internal Func<bool> campaignHoleIntroAllowed;

        internal Func<bool> campaignPortalAllowed;

        internal Action<BlackHole, Action, Action> campaignConfirmPortal;

        bool CampaignPortalOpenNow => !campaignHoleHidden && !campaignAsking && !pendingCampaignEntry && campaignPortalAllowed != null && campaignPortalAllowed();

        Transform SavedWallGroup()
        {
            int index = biomes != null ? Array.IndexOf(biomes, biome) : -1;
            return index >= 0 && biomeWallGroups != null && index < biomeWallGroups.Length ? biomeWallGroups[index] : null;
        }

        public void ApplyCampaignLayout(MapLayout campaign, Vector3 holeAt, int feedsToOpen)
        {
            if (!campaign) return;
            if (CampaignMapApplied) RestoreLayout();

            if (TryGetComponent(out TyrantTerrain terrain) && terrain.Changed) terrain.ResetTerrain();
            classicLayout = layout;
            campaignLayout = campaign;

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

            holeIntroStage = HoleIntroStage.Idle;
            holeIntroFodder.Clear();
            holeIntroHole = null;

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

        public void RestoreLayout()
        {
            if (!CampaignMapApplied) return;
            if (TrialActive) AbortTrial();                                                                       
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

            extraWalls.Clear();                                                 
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

        void ForgetCampaignMap()
        {
            ClearCampaignHooks();
            if (campaignLayout && campaignLayout != classicLayout) Destroy(campaignLayout);
            campaignLayout = null;
        }

        void ResizeFloor(MapLayout campaign)
        {
            if (!floorRenderer) return;
            var floor = floorRenderer.transform;
            classicFloorScale = floor.localScale;
            float marginX = classicLayout ? Mathf.Max(0f, classicFloorScale.x - classicLayout.width) : 0f;
            float marginZ = classicLayout ? Mathf.Max(0f, classicFloorScale.z - classicLayout.height) : 0f;
            floor.localScale = new Vector3(Mathf.Max(classicFloorScale.x, campaign.width + marginX), classicFloorScale.y, Mathf.Max(classicFloorScale.z, campaign.height + marginZ));
        }

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
                if (Mathf.Abs(p.z) >= Mathf.Abs(p.x))                                                 
                {
                    part.localPosition = new Vector3(p.x, p.y, p.z + Mathf.Sign(p.z) * growZ);
                    part.localScale = new Vector3(s.x + growX * 2f, s.y, s.z);
                }
                else                
                {
                    part.localPosition = new Vector3(p.x + Mathf.Sign(p.x) * growX, p.y, p.z);
                    part.localScale = new Vector3(s.x, s.y, s.z + growZ * 2f);
                }
            }
        }

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

        public void SetCampaignHoleHidden(bool hidden)
        {
            if (!CampaignMapApplied || !campaignHole || campaignHoleHidden == hidden || TrialActive) return;
            campaignHoleHidden = hidden;
            if (hidden) { pendingCampaignEntry = null; campaignAsking = false; }
            holes = hidden ? Array.Empty<BlackHole>() : new[] { campaignHole };
            campaignHole.gameObject.SetActive(!hidden);
            HoleChanged();
        }

        void CloseCampaignHoleAfterTrial()
        {
            if (CampaignMapApplied && campaignHole && campaignHole.Phase != HolePhase.Gone) campaignHole.Close();
        }

        Enemy SpawnCampaignIntroFodder(Vector3 at)
        {
            var body = GM.waves.SpawnCampaign(EnemyKind.Grunt, at);
            if (!body) return null;
            body.SuppressRewards = true;
            body.Raider = false;
            return body;
        }

        void AskCampaignPortal(BlackHole hole)
        {
            campaignAsking = true;
            campaignAskedAt = Time.unscaledTime;
            campaignConfirmPortal(hole, () => { campaignAsking = false; pendingCampaignEntry = hole; }, () => campaignAsking = false);
        }

        void TickCampaignWorld()
        {
            bool playing = Time.timeScale > 0f && GM.RunPhase is ArenaUiState.WaveActive or ArenaUiState.Night;

            if (campaignAsking && playing && Time.unscaledTime - campaignAskedAt > .25f) campaignAsking = false;
            if (pendingCampaignEntry) TickCampaignEntry(playing);
            campaignTick?.Invoke();
        }

        void TickCampaignEntry(bool playing)
        {
            var hole = pendingCampaignEntry;
            var player = GM.player;
            if (!player || !player.IsAlive || TrialActive || TrialWon || campaignHoleHidden || hole.Phase != HolePhase.Portal) { pendingCampaignEntry = null; return; }
            if (!playing) return;                            
            pendingCampaignEntry = null;

            if (player.State != CharacterState.Normal || player.ActionsBlocked || Query.Flat(player.Position - hole.Centre).magnitude > 1.2f) return;
            enteredHole = hole;
            BeginTrial(player, hole.Centre, false);
        }

        internal void AbortTrial()
        {
            if (!TrialActive) return;
            if (GM.waves.preparedEnemies != null)
                foreach (var enemy in GM.waves.preparedEnemies) if (enemy && enemy.CorpseActive && enemy.IsTrialSummon) enemy.DespawnCorpse(enemy.SpawnVersion);
            GM.waves.ClearTrialSummons();                                                                               

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
