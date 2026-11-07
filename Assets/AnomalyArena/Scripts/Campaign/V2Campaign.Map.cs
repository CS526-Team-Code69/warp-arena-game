using UnityEngine;

namespace AnomalyArena
{

    public sealed partial class V2Campaign
    {

        const float BaseClearRadius = 22f, ShipClearRadius = 12f, CrashClearRadius = 10f, RouteHalfWidth = 7f, CornerClearRadius = 10f;
        const float NightClusterClearRadius = 7f, HoleClearance = 9f, HoleBandHalfWidth = 4f;

        const float TutorialCornerEastOfCrash = 20f, FirstCornerDepthShare = .55f;
        const int MinMapCells = 40;

        Vector2Int CampaignMapSize => new(Mathf.Max(MinMapCells, rules.mapSize.x), Mathf.Max(MinMapCells, rules.mapSize.y));

        public int MapSeed { get; private set; }

        internal void SetupCampaignMap()
        {
            var map = WorldMap.Active;
            if (!map || !rules) return;
            if (map.CampaignMapApplied) map.RestoreLayout();                                                                 
            var size = CampaignMapSize;
            int seed = MapSeed = Random.Range(1, int.MaxValue);
            var hole = map.PrimaryHole;
            var plan = TerrainPlan(size, seed, hole ? hole.GravityRadius : 6.5f, map.HasWallTemplates);
            var layout = CampaignTerrain.Build(map.ClassicLayout, plan, out string report);
            map.ApplyCampaignLayout(layout, new Vector3(plan.hole.x, 0f, plan.hole.y), rules.holeFeedsToOpen);
            Debug.Log($"[Campaign] {report}; black hole at ({plan.hole.x:0}, {plan.hole.y:0}), {(plan.hole - new Vector2(BaseCentre.x, BaseCentre.z)).magnitude:0} cells from the base");
            BeginHoleQuest(map);
            BuildHoleGuide(map);                                                                             
        }

        CampaignTerrain.Plan TerrainPlan(Vector2Int size, int seed, float holePull, bool walls)
        {
            float hw = size.x * .5f, hh = size.y * .5f;
            var rng = new System.Random(seed);
            Vector2 holeAt = HoleSpot(rng, hw, hh);
            Vector2 home = new(BaseCentre.x, BaseCentre.z), ship = new(ShipCentre.x, ShipCentre.z);
            Vector2 crash = new(CrashSite.x, CrashSite.z);
            Vector2 corner = new(TutorialCorner.x, TutorialCorner.z);
            Vector2 firstCorner = new(crash.x + TutorialCornerEastOfCrash, -hh * FirstCornerDepthShare);
            var plan = new CampaignTerrain.Plan
            {
                size = size, seed = seed, start = new Vector2(DoorPoint.x, DoorPoint.z), hole = holeAt, holePull = holePull + .5f,
                walls = walls, sand = true,
            };
            plan.keep.Add(CampaignTerrain.Keep.Circle(home, BaseClearRadius));
            plan.keep.Add(CampaignTerrain.Keep.Circle(ship, ShipClearRadius));
            plan.keep.Add(CampaignTerrain.Keep.Circle(crash, CrashClearRadius));

            plan.keep.Add(CampaignTerrain.Keep.Band(crash, home, RouteHalfWidth));
            plan.keep.Add(CampaignTerrain.Keep.Circle(corner, CornerClearRadius));
            plan.keep.Add(CampaignTerrain.Keep.Band(corner, new Vector2(corner.x, crash.y), RouteHalfWidth * .8f));
            plan.keep.Add(CampaignTerrain.Keep.Circle(firstCorner, CornerClearRadius));
            plan.keep.Add(CampaignTerrain.Keep.Band(firstCorner, new Vector2(firstCorner.x, crash.y), RouteHalfWidth * .8f));
            plan.keep.Add(CampaignTerrain.Keep.Circle(home + new Vector2(18f, -9f), NightClusterClearRadius));

            plan.keep.Add(CampaignTerrain.Keep.Circle(holeAt, holePull + HoleClearance));
            plan.keep.Add(CampaignTerrain.Keep.Band(home, holeAt, HoleBandHalfWidth));

            plan.fixedWalls.Add(new Rect(BaseCentre.x - BaseHalf, BaseCentre.z - BaseHalf, BaseHalf * 2f, BaseHalf * 2f));
            plan.fixedWalls.Add(new Rect(ShipCentre.x - ShipHalfLength, ShipCentre.z - ShipHalfWidth, ShipHalfLength * 2f, ShipHalfWidth * 2f));
            return plan;
        }

        static Vector2 HoleSpot(System.Random rng, float hw, float hh)
        {
            float insetX = Mathf.Clamp(hw * .24f, 12f, 24f), insetZ = Mathf.Clamp(hh * .3f, 10f, 22f);
            int pick = rng.Next(3);
            float sx = pick == 2 ? -1f : 1f, sz = pick == 1 ? -1f : 1f;
            float jitterX = ((float)rng.NextDouble() - .5f) * 6f, jitterZ = ((float)rng.NextDouble() - .5f) * 6f;
            return new Vector2(sx * (hw - insetX) + jitterX, sz * (hh - insetZ) + jitterZ);
        }

        internal void RestoreClassicMap()
        {
            EndHoleQuest();
            ClearHoleGuide();
            var map = WorldMap.Active;
            if (!map || !map.CampaignMapApplied || !gameObject.scene.isLoaded) return;
            map.RestoreLayout();
        }

        internal Vector3? HoleCentre
        {
            get
            {
                var hole = WorldMap.Active ? WorldMap.Active.CampaignHole : null;
                return hole && hole.Phase != HolePhase.Gone ? (Vector3?)hole.Centre : null;
            }
        }

        internal Vector2 MapHalfSize
        {
            get
            {
                var map = WorldMap.Active;
                if (map && map.CampaignMapApplied && map.layout) return new Vector2(map.layout.width * .5f, map.layout.height * .5f);
                if (!rules) return new Vector2(50f, 35f);
                var size = CampaignMapSize;
                return new Vector2(size.x * .5f, size.y * .5f);
            }
        }

        internal bool HiddenPortalOpen
        {
            get
            {
                var hole = WorldMap.Active ? WorldMap.Active.CampaignHole : null;
                return hole && hole.Phase is HolePhase.Collapsing or HolePhase.Portal;
            }
        }
    }
}
