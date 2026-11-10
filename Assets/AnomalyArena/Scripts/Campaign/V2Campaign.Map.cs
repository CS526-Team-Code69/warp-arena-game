using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The V2 campaign's own map (since 2026-11-06): CampaignRules.mapSize cells, with the base in the middle, terrain laid out afresh each
    /// run from a random seed (CampaignTerrain: ridges, broken walls, rock forests, outcrops, boulders and a few quicksand pools) and one
    /// black hole far out in a corner, which the day's minerals lead toward (the afternoon's ore trail reads HoleCentre; the hole has no
    /// marker of its own). The ground the campaign needs stays open: round the base and the ship, the crash site, the Day 0 walk to the
    /// base and its glowing corner, the first night's dark matter, the black hole and a band from the base to it. Every open cell is
    /// checked to be reachable on foot from the base door. The black hole's side quest is in V2Campaign.Hole.cs; the arena side in
    /// WorldMap.Campaign.cs.
    /// </summary>
    public sealed partial class V2Campaign
    {
        // Open ground round the campaign's places, in cells (level geometry, not balance numbers).
        const float BaseClearRadius = 22f, ShipClearRadius = 12f, CrashClearRadius = 10f, RouteHalfWidth = 7f, CornerClearRadius = 10f;
        const float NightClusterClearRadius = 7f, HoleClearance = 9f, HoleBandHalfWidth = 4f;
        // Day 0's glowing corner (V2Campaign.TutorialCorner, read by V2Campaign.Story.cs) keeps its ground open, and so does the place the
        // first version of the tutorial put it: this far east of the crash site, south at 55% of the map's half depth.
        const float TutorialCornerEastOfCrash = 20f, FirstCornerDepthShare = .55f;
        const int MinMapCells = 40;

        /// <summary>The campaign map's size in cells, at least MinMapCells each way.</summary>
        Vector2Int CampaignMapSize => new(Mathf.Max(MinMapCells, rules.mapSize.x), Mathf.Max(MinMapCells, rules.mapSize.y));
        /// <summary>The seed this run's terrain was laid out from (CampaignTerrain.Generate gives the same terrain for the same seed and
        /// plan); 0 before the campaign's map is in place. For automated checks and bug reports.</summary>
        public int MapSeed { get; private set; }

        /// <summary>Called once from Begin, before the world props are built: switch the arena to the campaign's map (larger, with terrain)
        /// and place the campaign's black hole.</summary>
        internal void SetupCampaignMap()
        {
            var map = WorldMap.Active;
            if (!map || !rules) return;
            if (map.CampaignMapApplied) map.RestoreLayout(); // a second campaign in the same scene starts from the saved map
            var size = CampaignMapSize;
            int seed = MapSeed = Random.Range(1, int.MaxValue);
            var hole = map.PrimaryHole;
            var plan = TerrainPlan(size, seed, hole ? hole.GravityRadius : 6.5f, map.HasWallTemplates);
            var layout = CampaignTerrain.Build(map.ClassicLayout, plan, out string report);
            map.ApplyCampaignLayout(layout, new Vector3(plan.hole.x, 0f, plan.hole.y), rules.holeFeedsToOpen);
            Debug.Log($"[Campaign] {report}; black hole at ({plan.hole.x:0}, {plan.hole.y:0}), {(plan.hole - new Vector2(BaseCentre.x, BaseCentre.z)).magnitude:0} cells from the base");
            BeginHoleQuest(map);
            BuildHoleGuide(map); // the purple shards and the hum that lead to the hole (V2Campaign.Guide.cs)
        }

        /// <summary>What the campaign's map must keep: the black hole in a far corner and the open ground round the places the run needs.
        /// The crash site and the tutorial's corner are read before the map is in place, so CrashSite must follow MapHalfSize (the rules'
        /// size), not the arena's current bounds.</summary>
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
            // Day 0: the walk right from the crash site to the base door, the sprint down to the glowing corner, the first night's cluster.
            plan.keep.Add(CampaignTerrain.Keep.Band(crash, home, RouteHalfWidth));
            plan.keep.Add(CampaignTerrain.Keep.Circle(corner, CornerClearRadius));
            plan.keep.Add(CampaignTerrain.Keep.Band(corner, new Vector2(corner.x, crash.y), RouteHalfWidth * .8f));
            plan.keep.Add(CampaignTerrain.Keep.Circle(firstCorner, CornerClearRadius));
            plan.keep.Add(CampaignTerrain.Keep.Band(firstCorner, new Vector2(firstCorner.x, crash.y), RouteHalfWidth * .8f));
            plan.keep.Add(CampaignTerrain.Keep.Circle(home + new Vector2(18f, -9f), NightClusterClearRadius));
            // The black hole: open ground round its pull ring, and a band from the base to it along which the minerals lead.
            plan.keep.Add(CampaignTerrain.Keep.Circle(holeAt, holePull + HoleClearance));
            plan.keep.Add(CampaignTerrain.Keep.Band(home, holeAt, HoleBandHalfWidth));
            // The base and the ship pad stand as walls once BuildWorld adds them (SetExtraWalls): the check walks round them.
            plan.fixedWalls.Add(new Rect(BaseCentre.x - BaseHalf, BaseCentre.z - BaseHalf, BaseHalf * 2f, BaseHalf * 2f));
            plan.fixedWalls.Add(new Rect(ShipCentre.x - ShipHalfLength, ShipCentre.z - ShipHalfWidth, ShipHalfLength * 2f, ShipHalfWidth * 2f));
            return plan;
        }

        /// <summary>One of the three far corners (never the south-west one, beside the Day 0 corner), a few cells either way: far from the
        /// base, so the minerals have days of ground to lead the player across.</summary>
        static Vector2 HoleSpot(System.Random rng, float hw, float hh)
        {
            float insetX = Mathf.Clamp(hw * .24f, 12f, 24f), insetZ = Mathf.Clamp(hh * .3f, 10f, 22f);
            int pick = rng.Next(3);
            float sx = pick == 2 ? -1f : 1f, sz = pick == 1 ? -1f : 1f;
            float jitterX = ((float)rng.NextDouble() - .5f) * 6f, jitterZ = ((float)rng.NextDouble() - .5f) * 6f;
            return new Vector2(sx * (hw - insetX) + jitterX, sz * (hh - insetZ) + jitterZ);
        }

        /// <summary>Called when the campaign ends or its object goes away: give the classic run its own map back. The black hole's shards
        /// and hum go first. While the scene itself is closing nothing is rebuilt (everything goes with it); only the side quest lets go
        /// of its hooks.</summary>
        internal void RestoreClassicMap()
        {
            EndHoleQuest();
            ClearHoleGuide();
            var map = WorldMap.Active;
            if (!map || !map.CampaignMapApplied || !gameObject.scene.isLoaded) return;
            map.RestoreLayout();
        }

        /// <summary>Where the campaign's black hole stands (null while there is none: before the campaign map, or once the hidden boss is
        /// beaten and the hole has turned into ground). At night it is hidden but keeps its place, so this still answers. Mineral trails
        /// lead toward it.</summary>
        internal Vector3? HoleCentre
        {
            get
            {
                var hole = WorldMap.Active ? WorldMap.Active.CampaignHole : null;
                return hole && hole.Phase != HolePhase.Gone ? (Vector3?)hole.Centre : null;
            }
        }

        /// <summary>Half the campaign map's size in cells (x, z): the area minerals and terrain may use. Before the map is in place it is
        /// the rules' size, so it reads the same before and after SetupCampaignMap.</summary>
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

        /// <summary>The side quest has opened the hidden portal (the black hole has been fed enough) and it stands open: false before, and
        /// again once the hidden boss is beaten.</summary>
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
