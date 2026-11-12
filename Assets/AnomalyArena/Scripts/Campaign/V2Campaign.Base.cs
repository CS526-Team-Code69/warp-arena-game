using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    
        /// <summary>What a room sells besides armor levels, weapon damage and new weapons: one random upgrade of this kind, applied at once.</summary>
    public enum CampaignUpgrade { Stat, Skill, Infusion, Tame, Twist }

    /// <summary>
    /// The base (V2 doc section 5): HP that grows with the durability level, a shield from central control that refills each night, four
    /// sides that each stand for one room (D7-A: central control can break too; a broken central control is repaired by hand, the
    /// other rooms through it), the rooms' levels and the ship's repair state. Raiders (since 2026-11-06 every monster of a day wave) walk
    /// to the base and strike its nearest side, unless the player comes close; only those attacks hurt it, never an element or a status
    /// (the doc: the base has no attributes).
    /// </summary>
    public sealed partial class V2Campaign
    {
        public sealed class RoomState { public int level; public bool damaged; }

        /// <summary>Side order: north, east, south, west. North faces the ship pad, south is the door.</summary>
        public static readonly CampaignRoom[] SideRoom = { CampaignRoom.Ship, CampaignRoom.Armament, CampaignRoom.Control, CampaignRoom.Energy };
        public static readonly Vector3[] SideNormal = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        public const float BaseHalf = 5f;         // the base footprint is 10 x 10 cells around the map centre
        public static readonly Vector3 BaseCentre = Vector3.zero;
        public static readonly Vector3 ShipCentre = new(0f, 0f, 13f);
        public const float ShipHalfLength = 7f, ShipHalfWidth = 3f;
        public Vector3 DoorPoint => BaseCentre + Vector3.back * (BaseHalf + 1.4f);

        readonly RoomState[] rooms = { new(), new(), new(), new() };
        public RoomState Room(CampaignRoom room) => rooms[(int)room];
        public int Durability { get; private set; } = 1;
        public int ShieldLevel { get; private set; }
        public float BaseHp { get; private set; }
        public float BaseShield { get; private set; }
        // Since 2026-11-10 the base's levels and shield follow the V2 doc's base table (CampaignRules.Rooms.cs).
        public float BaseMaxHp => rules.BaseHpAt(Durability);
        public float BaseMaxShield => BaseMaxHp * rules.ShieldCapShare(ShieldLevel);
        /// <summary>The base's max HP at a level, and the highest base and shield levels (for the base panel's texts).</summary>
        public float BaseMaxHpAt(int level) => rules.BaseHpAt(level);
        public int MaxDurability => rules.MaxBaseLevel;
        public int MaxShieldLevel => rules.MaxShieldLevel;
        readonly float[] sideDamageToday = new float[4];
        readonly float[] sideFlash = new float[4];
        bool engineFixed, armorFixed;
        int fuel;
        public bool EngineFixed => engineFixed;
        public bool ArmorFixed => armorFixed;
        // anti-matter fuel tracker connected to rocket base UI. - H.
        public int Fuel => fuel;

        // One antimatter equals one fuel cell.
        public int FuelUnits => 100;
        public int DarkPerFuel => 1;
        public bool FuelFull => fuel >= FuelUnits;
        public bool CanSynthesiseFuel => Running && !BaseDestroyed && !FuelFull && Dark > 0;
   
        float shieldTopUpAt;

        Transform worldRoot, baseRoot, shipRoot;
        Renderer[] baseRenderers;
        readonly Renderer[] sidePanels = new Renderer[4];
        Material[] baseIntactMaterials;
        GameObject shipWreckView, shipIntactView;
        Renderer engineBlock, armorPlates, fuelTank;
        // The whitebox (2026-11-10): the whitebox table's looks (Whitebox.Build) instead of grey boxes, only while the art is off. The
        // base with its door, a room marker per side (the door marks central control), a broken panel per damaged side; the ship's wreck
        // with the repaired ship's engine and fuel tank in their places, and the repaired ship. The illustrated branches are unchanged.
        WhiteboxLook baseWhitebox, wreckWhitebox, wreckEngine, wreckFuel, shipWhitebox;
        readonly WhiteboxLook[] sideMarks = new WhiteboxLook[4];
        readonly GameObject[] damagedSides = new GameObject[4];
        const float WhiteboxMarkInset = 2f;     // cells from the base's middle to a room marker: on the 5 x 5 roof by its side
        const float DamagedPanelShift = 2.5f;   // cells along the side a broken panel stands off its middle, clear of the south door
        static readonly Color BrokenPartGrey = new(.3f, .3f, .32f), EmptyTankColour = new(.2f, .1f, .25f);
        Light baseLight;
        V2ShipView shipView;
        // Per-side damage (D3-A): which sides show the damaged texture set and their burn marks; whether the model was cut into sides.
        readonly bool[] sideShowsDamage = new bool[4];
        readonly List<GameObject>[] sideScorches = { new(), new(), new(), new() };
        bool sidesSplit;
        readonly List<Mesh> splitMeshes = new();
        static Material scorchMaterial;
        static readonly Mesh[] scorchMeshes = new Mesh[4];

        // ───── Walls of the base and the ship (2026-11-10) ─────
        // They follow what is seen.
        // Before, the base's wall was its whole 10 x 10 footprint, whose corners stand up to 2 cells clear of the rounded model, and the
        // ship's a 14 x 6 box, wider than the whitebox hull (3.6) and narrower than the illustrated wreck's wings (8.6).
        // The whitebox (since 2026-11-10 the whitebox table's looks): the same rules as a model's below, over the look's own polyhedra
        //   (AddModelWalls reads their meshes), so the walls follow what is seen. The old boxes, left for the illustrated art without its
        //   models: every block that stands at the height of a shot or a body is a wall of its own size, and the map's walls
        //   (WorldMap.SetExtraWalls) are the same rectangles.
        // The illustrated art: shots, area hits and sight stop at prisms over the model's outline round the shots' height (BaseFootprint;
        //   the ship cut into slabs along its length), on the Wall layer. Bodies and the map's walls, which are rectangles, stop at each
        //   outline's bounding box, on Unity's Ignore Raycast layer (Combatant.BodyOnlyWallLayer), which no shot or sight casts against:
        //   a body on ground the map counts as wall could not walk off it (Enemy.KeepOffEdges), so the two must agree. The corners of the
        //   base's box are the one place left where a body (never a shot) meets a wall it cannot see.
        // The raiders' stand points stay a body and a little out from the 10 x 10 footprint (RaidSpotFor), outside every one of these.
        const float WallHeight = 4f, WhiteboxCoreInset = .3f;
        const int WallCorners = 32, ShipWallSlabs = 7;
        // Where a model is cut for its outline: from a body's knees to above its head, round the shots' height (Query.CastHeight 0.8).
        // The ship stands raised, its nose and tail overhanging from about 1.5 m: cut only at the shots' height, its walls would be 4.5 m
        // shorter than what is seen from above. Rubble lower than the first cut is walked and shot over.
        static readonly float[] WallSliceHeights = { .4f, Query.CastHeight, 1.4f, 2f, 2.6f };
        // The map's walls of the base (the first is the one the raiders' flow field leads to) and of each ship view.
        readonly List<Rect> baseRects = new(), shipRects = new(), shipIntactRects = new();
        readonly List<Mesh> wallMeshes = new();
        GameObject shipWreckWalls, shipIntactWalls;

        /// <summary>The ship tab's live picture (D1-C), drawn only while ShowShipView(true).</summary>
        public Texture ShipViewTexture => shipView ? shipView.Texture : null;
        public void ShowShipView(bool on) { if (shipView) shipView.Live = on; }
        /// <summary>For automated checks: whether a side shows its damage and how many burn marks lie there.</summary>
        public bool SideShowsDamage(int side) => sideShowsDamage[side];
        public int ScorchCount(int side) => sideScorches[side].Count;
        public bool SidesSplit => sidesSplit;
        readonly Dictionary<Enemy, (int side, Vector3 spot)> raidSpots = new();

        void ResetBase()
        {
            foreach (var room in rooms) { room.level = 0; room.damaged = false; }
            rooms[(int)CampaignRoom.Control].level = 1; // the base deployed itself with its central control (the V2 doc's story)
            Durability = 1; ShieldLevel = 0; BaseHp = rules.BaseHpAt(1); BaseShield = 0f;
            System.Array.Clear(sideDamageToday, 0, 4);
            engineFixed = armorFixed = false; fuel = 0; shieldTopUpAt = 0f;
            newWeaponsBought = 0;
            System.Array.Clear(upgradesBought, 0, upgradesBought.Length);
            raidSpots.Clear();
            ResetRooms(); // the rooms' own effects, the shared weapon Mk and the study's purchases (V2Campaign.Rooms.cs)
        }

        // ───── World objects ─────

        void BuildWorld()
        {
            if (worldRoot) Destroy(worldRoot.gameObject);
            worldRoot = new GameObject("V2 Campaign World").transform;
            foreach (var mesh in wallMeshes) if (mesh) Destroy(mesh); // the last run's wall prisms
            wallMeshes.Clear();
            baseRects.Clear(); shipRects.Clear(); shipIntactRects.Clear();
            BuildBase();
            BuildShip();
            ApplyMapWalls();
            BuildDarkness();
        }

        /// <summary>The map's walls of the base and of the ship view shown (the base's first: the raiders' flow field leads to it).</summary>
        void ApplyMapWalls()
        {
            var map = WorldMap.Active;
            if (!map) return;
            var walls = new List<Rect>(baseRects);
            walls.AddRange(shipIntactWalls && shipIntactWalls.activeSelf ? shipIntactRects : shipRects);
            map.SetExtraWalls(walls);
        }

        void BuildBase()
        {
            baseRoot = new GameObject("Base").transform;
            baseRoot.SetParent(worldRoot, false);
            baseRoot.position = BaseCentre;
            System.Array.Clear(sideShowsDamage, 0, 4);
            foreach (var list in sideScorches) list.Clear();
            sidesSplit = false;
            foreach (var mesh in splitMeshes) if (mesh) Destroy(mesh); // the last run's cut copies of the base mesh
            splitMeshes.Clear();
            baseWhitebox = null;
            System.Array.Clear(sideMarks, 0, 4);
            System.Array.Clear(damagedSides, 0, 4);
            if (art && art.basePrefab && Art.Enabled)
            {
                var model = Instantiate(art.basePrefab, baseRoot);
                model.name = "BaseModel";
                // The model's door faces +Z in its own space; the base's door is on the south side.
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                FitFootprint(model.transform, BaseHalf * 2f);
                StripColliders(model);
                AddModelWalls(baseRoot, "Walls", model, 1, baseRects, new Vector2(BaseHalf * 2f, BaseHalf * 2f));
                baseRenderers = model.GetComponentsInChildren<Renderer>();
                baseIntactMaterials = baseRenderers.Length > 0 ? baseRenderers[0].sharedMaterials : null;
                sidesSplit = SplitBaseSides(model);
            }
            else if (!Art.Enabled)
            {
                // The table's base, its door to the south (the table's front is +z). Its walls are made from what is seen by the same
                // rules as a model's (AddModelWalls): shots stop at its outline, bodies and the map at the outline's box. The table's base is
                // the full 10 x 10 footprint, so the wall is no larger than what is seen.
                var body = Whitebox.Build("base.body", baseRoot, "BaseWhitebox");
                body.localRotation = Quaternion.Euler(0f, 180f, 0f);
                baseWhitebox = body.GetComponent<WhiteboxLook>();
                AddModelWalls(baseRoot, "Walls", body.gameObject, 1, baseRects, new Vector2(BaseHalf * 2f, BaseHalf * 2f));
                baseRenderers = null;
            }
            else
            {
                float core = BaseHalf * 2f - WhiteboxCoreInset * 2f;
                var coreBox = Box(baseRoot, "Core", new Vector3(0f, 1.6f, 0f), new Vector3(core, 3.2f, core), new Color(.36f, .42f, .5f));
                Box(baseRoot, "Roof", new Vector3(0f, 3.5f, 0f), new Vector3(5f, .6f, 5f), new Color(.55f, .62f, .7f));
                var door = Box(baseRoot, "Door", new Vector3(0f, 1.1f, -BaseHalf), new Vector3(2.4f, 2.2f, .3f), new Color(.15f, .85f, 1f));
                Solid(coreBox, baseRects); // the first: the raiders' target
                Solid(door, baseRects);
                baseRenderers = new[] { coreBox };
            }
            // One coloured marker per side shows that side's room and whether it is broken: a panel on the whitebox, a light strip on
            // the ground along the model.
            bool onModel = baseRenderers != null && baseRenderers.Length > 0 && baseRenderers[0] && baseRenderers[0].name != "Core";
            if (baseWhitebox) BuildSideMarks();
            else for (int side = 0; side < 4; side++)
            {
                Vector3 n = SideNormal[side];
                Vector3 size = onModel ? side % 2 == 0 ? new Vector3(7f, .08f, .3f) : new Vector3(.3f, .08f, 7f)
                    : side % 2 == 0 ? new Vector3(6f, .5f, .35f) : new Vector3(.35f, .5f, 6f);
                Vector3 at = onModel ? n * (BaseHalf + .45f) + Vector3.up * .05f : n * (BaseHalf + .05f) + Vector3.up * 3.4f;
                sidePanels[side] = Box(baseRoot, "Side " + SideRoom[side], at, size, RoomColour(SideRoom[side]));
                sidePanels[side].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var lightGo = new GameObject("BaseLight");
            lightGo.transform.SetParent(baseRoot, false);
            lightGo.transform.localPosition = new Vector3(0f, 6f, 0f);
            baseLight = lightGo.AddComponent<Light>();
            baseLight.type = LightType.Point; baseLight.range = rules.baseLightRadius * 1.15f; baseLight.intensity = 0f;
            baseLight.color = new Color(.75f, .9f, 1f);
            BuildBaseFloodlights();
        }

        // ───── The base's night light ─────

        readonly List<Light> baseFloodlights = new();
        LightWaveRing baseLightEdge;

        /// <summary>Four floodlights at the base's corners and a ring on the ground at the edge of the base's light, lit at night: the
        /// player sees where the light ends, and the shadows waiting just outside it.</summary>
        void BuildBaseFloodlights()
        {
            baseFloodlights.Clear();
            for (int i = 0; i < 4; i++)
            {
                var post = new GameObject("Floodlight").transform;
                post.SetParent(baseRoot, false);
                post.localPosition = new Vector3(i % 2 == 0 ? -1f : 1f, 0f, i < 2 ? -1f : 1f) * (BaseHalf + 1.1f);
                if (art && art.lampPrefab && Art.Enabled)
                {
                    var model = Instantiate(art.lampPrefab, post);
                    StripColliders(model);
                    FitHeight(model.transform, 2.6f);
                }
                else if (!Art.Enabled)
                {
                    var look = Whitebox.Build("lamp", post, "FloodlightWhitebox");
                    if (look) look.localScale = Vector3.one * 1.6f;
                }
                else
                {
                    Box(post, "Post", new Vector3(0f, 1f, 0f), new Vector3(.22f, 2f, .22f), new Color(.3f, .3f, .32f));
                    Box(post, "Head", new Vector3(0f, 2.2f, 0f), new Vector3(.6f, .45f, .6f), new Color(.8f, .93f, 1f)).shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                var lampGo = new GameObject("FloodlightLight");
                lampGo.transform.SetParent(post, false);
                lampGo.transform.localPosition = new Vector3(0f, 3f, 0f);
                var light = lampGo.AddComponent<Light>();
                light.type = LightType.Point; light.range = rules.baseLightRadius * .6f; light.intensity = 0f; light.color = new Color(1f, .93f, .8f);
                baseFloodlights.Add(light);
            }
            baseLightEdge = LightWaveRing.Create(baseRoot, "BaseLightEdge");
            baseLightEdge.sparkleRate = 0f;
        }

        /// <summary>The base's lights go on for the night and off for the day.</summary>
        void SetBaseLights(bool on)
        {
            // Low lights: they warm the ground by the base and its posts; the reach of the base's light shows by the darkness cut away
            // and the thin line at its edge (LightEdgeWidth), not by brightness.
            if (baseLight) baseLight.intensity = on ? 1.2f : 0f;
            foreach (var light in baseFloodlights) if (light) light.intensity = on ? 1.4f : 0f;
            if (!baseLightEdge) return;
            if (on) baseLightEdge.Draw(BaseCentre, rules.baseLightRadius, LightEdgeWidth, new Color(1f, .93f, .78f, LightEdgeAlpha), LightEdgeGlow);
            else baseLightEdge.Hide();
        }

        void BuildShip()
        {
            shipRoot = new GameObject("Ship").transform;
            shipRoot.SetParent(worldRoot, false);
            shipRoot.position = ShipCentre;
            shipWreckWalls = shipIntactWalls = null;
            wreckWhitebox = wreckEngine = wreckFuel = shipWhitebox = null;
            engineBlock = armorPlates = fuelTank = null;
            var oldShip = new Vector2(ShipHalfLength * 2f, ShipHalfWidth * 2f); // the box a model's walls fall back to (unreadable mesh)
            if (art && Art.Enabled && art.shipWreckPrefab)
            {
                shipWreckView = Instantiate(art.shipWreckPrefab, shipRoot);
                shipWreckView.name = "ShipWreck";
                FitFootprint(shipWreckView.transform, ShipHalfLength * 2f, true);
                StripColliders(shipWreckView);
                shipWreckWalls = AddModelWalls(shipRoot, "WreckWalls", shipWreckView, ShipWallSlabs, shipRects, oldShip);
                if (art.shipIntactPrefab)
                {
                    shipIntactView = Instantiate(art.shipIntactPrefab, shipRoot);
                    shipIntactView.name = "ShipIntact";
                    FitFootprint(shipIntactView.transform, ShipHalfLength * 2f, true);
                    StripColliders(shipIntactView);
                    // Read while it is still active (an inactive model's meshes are not found); RefreshShip shows one view and its walls.
                    shipIntactWalls = AddModelWalls(shipRoot, "IntactWalls", shipIntactView, ShipWallSlabs, shipIntactRects, oldShip);
                    shipIntactView.SetActive(false);
                    shipIntactWalls.SetActive(false);
                }
            }
            else if (!Art.Enabled) BuildShipWhitebox(oldShip);
            else
            {
                Solid(Box(shipRoot, "Hull", new Vector3(0f, 1.3f, 0f), new Vector3(10f, 2.2f, 3.6f), new Color(.82f, .84f, .86f)), shipRects);
                Solid(Box(shipRoot, "Nose", new Vector3(5.8f, 1.2f, 0f), new Vector3(1.8f, 1.6f, 2.4f), new Color(.9f, .55f, .2f)), shipRects);
            }
            // Part markers, with a model and on the old boxes: they show what is repaired (grey = broken, coloured = fixed). The whitebox
            // looks carry their own engine and fuel tank (BuildShipWhitebox).
            if (!wreckWhitebox) BuildPartMarkers();
            RefreshShip();
            shipView = V2ShipView.Create(worldRoot, ShipCentre);
        }

        void BuildPartMarkers()
        {
            engineBlock = Box(shipRoot, "Engine", new Vector3(-6.2f, 1.4f, 0f), new Vector3(1.4f, 1.8f, 2.6f), new Color(.3f, .3f, .32f));
            armorPlates = Box(shipRoot, "ArmorPlates", new Vector3(0f, 2.75f, 0f), new Vector3(7f, .3f, 3f), new Color(.3f, .3f, .32f));
            fuelTank = Box(shipRoot, "FuelTank", new Vector3(-2.5f, 0.6f, -2.4f), new Vector3(3f, 1f, 1f), new Color(.2f, .1f, .25f));
            if (Art.Enabled && art && art.shipWreckPrefab)
            {
                // Over a model the markers become small glowing tabs instead of whole blocks (no walls: the model's own are round them).
                engineBlock.transform.localScale = new Vector3(.5f, .5f, .5f);
                armorPlates.transform.localScale = new Vector3(2.5f, .15f, .5f);
                fuelTank.transform.localScale = new Vector3(1.6f, .35f, .35f);
            }
            else
            {
                // On the old boxes (no model) the engine and the fuel tank are blocks of the ship at the height of a shot or a body (the
                // armor plates lie on the hull's top, above both).
                Solid(engineBlock, shipRects);
                Solid(fuelTank, shipRects);
            }
        }

        /// <summary>Whitebox: the table's wreck, with the repaired ship's engine and fuel tank in their places (so their repair shows before
        /// the armor is fixed), and the table's repaired ship; both nose east (the table's front is +z), each with its walls made from what
        /// is seen by the same rules as the models' (AddModelWalls: the ship cut into slabs along its length). RefreshShip shows one and
        /// its walls, as it does the models.</summary>
        void BuildShipWhitebox(Vector2 fallback)
        {
            var wreck = ShipWhiteboxView("ShipWreckWhitebox");
            wreckWhitebox = Whitebox.Build("wreck", wreck).GetComponent<WhiteboxLook>();
            wreckEngine = Whitebox.Build("ship.engine", wreck).GetComponent<WhiteboxLook>();
            wreckFuel = Whitebox.Build("ship.fuel", wreck).GetComponent<WhiteboxLook>();
            shipWreckView = wreck.gameObject;
            // Read while active, as the models' (an inactive object's meshes are not found).
            shipWreckWalls = AddModelWalls(shipRoot, "WreckWalls", shipWreckView, ShipWallSlabs, shipRects, fallback);
            var intact = ShipWhiteboxView("ShipIntactWhitebox");
            shipWhitebox = Whitebox.Build("ship", intact).GetComponent<WhiteboxLook>();
            shipIntactView = intact.gameObject;
            shipIntactWalls = AddModelWalls(shipRoot, "IntactWalls", shipIntactView, ShipWallSlabs, shipIntactRects, fallback);
            shipIntactView.SetActive(false);
            shipIntactWalls.SetActive(false);
        }

        /// <summary>A holder for one whitebox ship look, turned so the table's front (+z) points east, the way the ship lies.</summary>
        Transform ShipWhiteboxView(string name)
        {
            var view = new GameObject(name).transform;
            view.SetParent(shipRoot, false);
            view.localRotation = Quaternion.LookRotation(Vector3.right);
            return view;
        }

        /// <summary>Whitebox: the room marker of the ship (north), armament (east) and energy (west) rooms on the roof by their side;
        /// central control, on the south, is marked by the door itself. TickBaseVisuals colours them (PaintSideMark).</summary>
        void BuildSideMarks()
        {
            for (int side = 0; side < 4; side++)
            {
                string id = SideRoom[side] switch
                {
                    CampaignRoom.Ship => "base.ship", CampaignRoom.Armament => "base.armament", CampaignRoom.Energy => "base.energy", _ => null
                };
                if (id == null) continue;
                var mark = Whitebox.Build(id, baseRoot, "Side " + SideRoom[side]);
                mark.localPosition = SideNormal[side] * WhiteboxMarkInset;
                sideMarks[side] = mark.GetComponent<WhiteboxLook>();
            }
        }

        /// <summary>Whitebox: a side's marker (the door for central control) in its table colour once built, grey before, red while broken,
        /// flashing white when struck, as the old panels were.</summary>
        void PaintSideMark(int side, RoomState room)
        {
            var mark = sideMarks[side];
            var look = mark ? mark : baseWhitebox;
            if (!look) return;
            Color own = mark ? mark.TableColour(0) : Whitebox.Colour("door");
            Color shown = room.damaged ? new Color(1f, .2f, .15f) : room.level <= 0 ? new Color(.25f, .25f, .27f) : own;
            shown = Color.Lerp(shown, Color.white, sideFlash[side]);
            if (mark) for (int i = 0; i < mark.PartCount; i++) mark.SetColour(i, shown);
            else baseWhitebox.SetColour("door", shown);
        }

        /// <summary>Whitebox: the table's broken panel on a side that shows its damage (the wall turned red, split into leaning plates),
        /// off its middle so the south door stays in sight; the smoke stays as it was.</summary>
        void ShowDamagedSide(int side, bool on)
        {
            if (!baseWhitebox) return;
            if (on && !damagedSides[side])
            {
                var panel = Whitebox.Build("baseDamaged.side", baseRoot, "Damaged " + SideRoom[side]);
                panel.localRotation = Quaternion.LookRotation(SideNormal[side]);
                panel.localPosition = Tangent(side) * DamagedPanelShift;
                damagedSides[side] = panel.gameObject;
            }
            if (damagedSides[side]) damagedSides[side].SetActive(on);
        }

        /// <summary>Whitebox: the engine lit once repaired, the armor (the repaired ship's wings) once fixed, the fuel tank filling from
        /// dark to the table's colour; the fuel tank shown is the one the ending lights.</summary>
        void PaintShipWhitebox(float fill)
        {
            Color engine = engineFixed ? Whitebox.Colour("control") : BrokenPartGrey;
            Color tank = Color.Lerp(EmptyTankColour, Whitebox.Colour("core"), fill);
            foreach (var look in new[] { wreckEngine, wreckFuel, shipWhitebox })
            {
                if (!look) continue;
                look.SetColour("control", engine);
                look.SetColour("core", tank);
            }
            if (shipWhitebox) shipWhitebox.SetColour("roof", armorFixed ? Whitebox.Colour("roof") : BrokenPartGrey);
            var tankLook = armorFixed ? shipWhitebox : wreckFuel;
            fuelTank = null;
            if (tankLook)
                for (int i = 0; i < tankLook.PartCount; i++)
                    if (tankLook.PartColourKey(i) == "core") { fuelTank = tankLook.PartRenderer(i); break; }
        }

        /// <summary>D3-A, one model with two texture sets and a damage mask per side: the base mesh is cut into four sub-meshes by the side
        /// each triangle faces (seen from above, the quarter of the footprint its centre lies in; the roof splits along the diagonals), so a
        /// side takes the damaged material on its own. Needs the mesh readable (the installer turns Read/Write on for the base model);
        /// otherwise the whole base swaps as before.</summary>
        bool SplitBaseSides(GameObject model)
        {
            var filters = model.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) return false;
            foreach (var f in filters)
                if (!f.sharedMesh || !f.sharedMesh.isReadable || f.sharedMesh.subMeshCount != 1 || !f.TryGetComponent<Renderer>(out _)) return false;
            foreach (var f in filters)
            {
                var source = f.sharedMesh;
                Matrix4x4 toBase = baseRoot.worldToLocalMatrix * f.transform.localToWorldMatrix;
                var vertices = source.vertices;
                var triangles = source.triangles;
                var parts = new List<int>[4];
                for (int s = 0; s < 4; s++) parts[s] = new List<int>(triangles.Length / 4 + 3);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    Vector3 c = toBase.MultiplyPoint3x4((vertices[triangles[t]] + vertices[triangles[t + 1]] + vertices[triangles[t + 2]]) / 3f);
                    int side = Mathf.Abs(c.x) > Mathf.Abs(c.z) ? c.x > 0f ? 1 : 3 : c.z > 0f ? 0 : 2;
                    parts[side].Add(triangles[t]); parts[side].Add(triangles[t + 1]); parts[side].Add(triangles[t + 2]);
                }
                var mesh = Instantiate(source);
                mesh.name = source.name + " (four sides)";
                mesh.subMeshCount = 4;
                for (int s = 0; s < 4; s++) mesh.SetTriangles(parts[s], s, false);
                f.sharedMesh = mesh;
                splitMeshes.Add(mesh);
                var r = f.GetComponent<Renderer>();
                var intact = r.sharedMaterial;
                r.sharedMaterials = new[] { intact, intact, intact, intact };
            }
            return true;
        }

        /// <summary>A side shows its damage while its room is broken, and every side does below half HP: its part of the model takes the
        /// damaged texture set and, with the illustrated art, burn marks lie on the ground along it.</summary>
        void RefreshSideDamage()
        {
            bool hull = BaseHp < BaseMaxHp * .5f;
            bool changed = false;
            for (int side = 0; side < 4; side++)
            {
                bool show = rooms[(int)SideRoom[side]].damaged || hull;
                if (show == sideShowsDamage[side]) continue;
                sideShowsDamage[side] = show;
                changed = true;
                SetScorches(side, show);
                ShowDamagedSide(side, show);
            }
            if (!changed || baseRenderers == null || !art || !art.baseDamagedMaterial || baseIntactMaterials == null || baseIntactMaterials.Length == 0) return;
            var intact = baseIntactMaterials[0];
            bool any = sideShowsDamage[0] || sideShowsDamage[1] || sideShowsDamage[2] || sideShowsDamage[3];
            foreach (var r in baseRenderers)
            {
                if (!r) continue;
                var materials = r.sharedMaterials;
                if (sidesSplit && materials.Length == 4)
                {
                    for (int s = 0; s < 4; s++) materials[s] = sideShowsDamage[s] ? art.baseDamagedMaterial : intact;
                    r.sharedMaterials = materials;
                }
                else r.sharedMaterial = any ? art.baseDamagedMaterial : intact;
            }
        }

        /// <summary>Burn marks along one side (the game's own scorch texture), or none. Only with the illustrated art: the whitebox shows
        /// a broken side by its red marker and the smoke.</summary>
        void SetScorches(int side, bool on)
        {
            var list = sideScorches[side];
            if (!on)
            {
                foreach (var go in list) if (go) Destroy(go);
                list.Clear();
                return;
            }
            if (list.Count > 0 || !Art.Enabled || !baseRoot) return;
            var material = ScorchMaterial();
            if (!material) return;
            var rng = new System.Random(side * 7919 + Day * 31 + 5);
            const int marks = 4;
            for (int i = 0; i < marks; i++)
            {
                float along = -BaseHalf + 1.2f + i * (BaseHalf * 2f - 2.4f) / (marks - 1) + (float)(rng.NextDouble() - .5) * 1.4f;
                Vector3 at = BaseCentre + SideNormal[side] * (BaseHalf + .5f + (float)rng.NextDouble() * .9f) + Tangent(side) * along;
                var go = new GameObject("Burn mark");
                go.transform.SetParent(baseRoot, true);
                go.transform.SetPositionAndRotation(new Vector3(at.x, .05f + i * .004f, at.z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                go.transform.localScale = Vector3.one * (2.9f + (float)rng.NextDouble() * 1.7f);
                go.AddComponent<MeshFilter>().sharedMesh = ScorchMesh(rng.Next(4));
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                list.Add(go);
            }
        }

        /// <summary>The scorch texture the explosions leave (Resources/V5/Fx/scorch.png, four marks in a 2 x 2 sheet) on the effects
        /// shader, alpha blended dark.</summary>
        static Material ScorchMaterial()
        {
            if (scorchMaterial) return scorchMaterial;
            var shader = Resources.Load<Shader>("V5/V5Particle");
            var texture = Resources.Load<Texture2D>("V5/Fx/scorch");
            if (!shader || !texture) return null;
            scorchMaterial = new Material(shader) { name = "Base burn marks" };
            scorchMaterial.SetTexture("_BaseMap", texture);
            scorchMaterial.SetColor("_BaseColor", new Color(.05f, .04f, .035f, .8f));
            scorchMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            scorchMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            scorchMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            return scorchMaterial;
        }

        /// <summary>A flat unit quad showing one of the four marks of the sheet, with white vertex colours (the shader multiplies them).</summary>
        static Mesh ScorchMesh(int tile)
        {
            tile = Mathf.Clamp(tile, 0, 3);
            if (scorchMeshes[tile]) return scorchMeshes[tile];
            float u = tile % 2 * .5f, v = tile / 2 * .5f;
            var mesh = new Mesh { name = "Burn mark " + tile };
            mesh.vertices = new[] { new Vector3(-.5f, 0f, -.5f), new Vector3(.5f, 0f, -.5f), new Vector3(-.5f, 0f, .5f), new Vector3(.5f, 0f, .5f) };
            mesh.uv = new[] { new Vector2(u, v), new Vector2(u + .5f, v), new Vector2(u, v + .5f), new Vector2(u + .5f, v + .5f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return scorchMeshes[tile] = mesh;
        }

        static void AddWallCollider(GameObject go, Vector3 size, Vector3 centre)
        {
            go.layer = GameManager.WallLayer;
            var box = go.AddComponent<BoxCollider>();
            box.size = size; box.center = centre;
        }

        /// <summary>A whitebox block made a wall of its own size (for shots, bodies and the map alike): a unit box collider on the scaled
        /// cube, and its rectangle on the map. The blocks are never turned, so the rectangle is the block's.</summary>
        static void Solid(Renderer block, List<Rect> rects)
        {
            var go = block.gameObject;
            go.layer = GameManager.WallLayer;
            go.AddComponent<BoxCollider>(); // the primitive's own collider goes in Box; a new one is the cube's size
            Vector3 at = go.transform.position, size = go.transform.lossyScale;
            rects.Add(new Rect(at.x - size.x * .5f, at.z - size.z * .5f, size.x, size.z));
        }

        /// <summary>The walls of an illustrated model, in a group under parent: per slab of its outline (BaseFootprint; parent's frame,
        /// never turned) a prism that shots, area hits and sight stop at, on the Wall layer, and the outline's bounding box that bodies stop
        /// at, on the body-only layer, which also goes on the map. A model whose mesh cannot be read (its Read/Write import setting is off)
        /// gets the box of fallback's size round parent instead, as before 2026-11-10.</summary>
        GameObject AddModelWalls(Transform parent, string name, GameObject model, int slabs, List<Rect> rects, Vector2 fallback)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            Vector3 origin = parent.position;
            var outlines = BaseFootprint.Outlines(model, parent, WallSliceHeights, slabs, WallCorners);
            if (outlines.Count == 0)
            {
                Debug.LogWarning($"[V2Campaign] {model.name}: its mesh cannot be read (Read/Write off), so its wall is the old {fallback.x} x {fallback.y} box");
                AddWallCollider(group, new Vector3(fallback.x, WallHeight, fallback.y), new Vector3(0f, WallHeight * .5f, 0f));
                rects.Add(new Rect(origin.x - fallback.x * .5f, origin.z - fallback.y * .5f, fallback.x, fallback.y));
                return group;
            }
            foreach (var outline in outlines)
            {
                var shots = new GameObject("ShotWall");
                shots.transform.SetParent(group.transform, false);
                shots.layer = GameManager.WallLayer;
                var prism = BaseFootprint.Prism(outline, 0f, WallHeight);
                wallMeshes.Add(prism);
                var hull = shots.AddComponent<MeshCollider>();
                hull.sharedMesh = prism;
                hull.convex = true;

                var box = BaseFootprint.Bounds(outline);
                var bodies = new GameObject("BodyWall");
                bodies.transform.SetParent(group.transform, false);
                bodies.layer = Combatant.BodyOnlyWallLayer;
                var block = bodies.AddComponent<BoxCollider>();
                block.size = new Vector3(box.width, WallHeight, box.height);
                block.center = new Vector3(box.center.x, WallHeight * .5f, box.center.y);
                rects.Add(new Rect(origin.x + box.xMin, origin.z + box.yMin, box.width, box.height));
            }
            return group;
        }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        /// <summary>Scales a model so its longest horizontal side equals size and centres its footprint on the parent; alongX first turns
        /// a model whose long side runs north-south a quarter turn.</summary>
        static void FitFootprint(Transform model, float size, bool alongX = false)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (alongX && bounds.size.z > bounds.size.x * 1.05f)
            {
                model.Rotate(0f, 90f, 0f, Space.World);
                bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            }
            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longest > .01f) model.localScale *= size / longest;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var parent = model.parent ? model.parent.position : Vector3.zero;
            model.position += new Vector3(parent.x - bounds.center.x, parent.y - bounds.min.y, parent.z - bounds.center.z);
        }

        Renderer Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            if (gm && gm.litMaterial) r.sharedMaterial = gm.Mat(colour);
            return r;
        }

        public static Color RoomColour(CampaignRoom room) => room switch
        {
            CampaignRoom.Control => new Color(.2f, .85f, 1f),
            CampaignRoom.Armament => new Color(1f, .58f, .18f),
            CampaignRoom.Energy => new Color(.72f, .4f, 1f),
            _ => new Color(.95f, .95f, .9f)
        };

        void RefreshShip()
        {
            if (!shipRoot) return;
            if (shipIntactView) { shipIntactView.SetActive(armorFixed); if (shipWreckView) shipWreckView.SetActive(!armorFixed); }
            // The walls follow the view shown, and so do the map's (the intact ship is narrower than the wreck).
            if (shipIntactWalls && shipIntactWalls.activeSelf != armorFixed)
            {
                shipIntactWalls.SetActive(armorFixed);
                if (shipWreckWalls) shipWreckWalls.SetActive(!armorFixed);
                ApplyMapWalls();
            }
            float f = rules ? fuel / (float)FuelUnits : 0f;
            if (wreckWhitebox) { PaintShipWhitebox(f); return; }
            SetColour(engineBlock, engineFixed ? new Color(.45f, .8f, 1f) : new Color(.3f, .3f, .32f));
            SetColour(armorPlates, armorFixed ? new Color(.92f, .92f, .95f) : new Color(.3f, .3f, .32f));
            SetColour(fuelTank, Color.Lerp(new Color(.2f, .1f, .25f), new Color(.78f, .3f, 1f), f));
        }

        void SetColour(Renderer r, Color c)
        {
            if (r && gm && gm.litMaterial) r.sharedMaterial = gm.Mat(c);
        }

        void TickBaseVisuals(float dt)
        {
            TickRooms(); // the rooms' passive effects, every frame of a run (V2Campaign.Rooms.cs)
            for (int side = 0; side < 4; side++)
            {
                sideFlash[side] = Mathf.Max(0f, sideFlash[side] - dt * 3f);
                var room = rooms[(int)SideRoom[side]];
                Color baseColour = room.damaged ? new Color(1f, .2f, .15f) : room.level <= 0 ? new Color(.25f, .25f, .27f) : RoomColour(SideRoom[side]);
                if (sidePanels[side]) Art.Tint(sidePanels[side], Color.Lerp(baseColour, Color.white, sideFlash[side]));
                else if (baseWhitebox) PaintSideMark(side, room);
                // A broken side smokes.
                if (room.damaged && Random.value < dt * 3f)
                    Fx.Pop(BaseCentre + SideNormal[side] * (BaseHalf + .3f) + Vector3.up * Random.Range(1f, 3.5f) + Tangent(side) * Random.Range(-3f, 3f), new Color(.25f, .25f, .25f), Random.Range(.8f, 1.4f));
            }
            RefreshSideDamage();
        }

        static Vector3 Tangent(int side) => side % 2 == 0 ? Vector3.right : Vector3.forward;

        // ───── Raids ─────

        /// <summary>Since 2026-11-06 every monster born by day raids the base (no share, not only the melee swarm); Enemy.RaidAggroRadius
        /// turns one on the player when the player comes close. Bosses, void trial summons and monsters that cannot walk (the Spire) never
        /// raid.</summary>
        void OnEnemySpawned(Enemy enemy)
        {
            if (!Running || !enemy || !enemy.definition) return;
            enemy.Raider = false;
            if (Stage is not (CampaignStage.Day or CampaignStage.Lull)) return;
            if (enemy.IsBoss || enemy.IsTrialSummon || enemy.Immovable) return;
            enemy.Raider = true;
        }

        /// <summary>Where a raider stands to strike: next to the base side nearest to it when it was first asked, spread along that side.</summary>
        Vector3? RaidSpotFor(Enemy enemy)
        {
            if (!Running || BaseDestroyed || Stage is not (CampaignStage.Day or CampaignStage.Lull)) return null;
            if (raidSpots.TryGetValue(enemy, out var known)) return known.spot;
            Vector3 d = Query.Flat(enemy.Position - BaseCentre);
            int side = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? d.x > 0f ? 1 : 3 : d.z > 0f ? 0 : 2;
            float along = Mathf.Clamp(Vector3.Dot(d, Tangent(side)) + Random.Range(-2f, 2f), -BaseHalf + 1f, BaseHalf - 1f);
            Vector3 spot = BaseCentre + SideNormal[side] * (BaseHalf + enemy.Radius + .35f) + Tangent(side) * along;
            raidSpots[enemy] = (side, spot);
            return spot;
        }

        void OnRaidStrike(Enemy enemy, float damage)
        {
            int side = raidSpots.TryGetValue(enemy, out var known) ? known.side : 2;
            Vector3 at = raidSpots.TryGetValue(enemy, out known) ? known.spot - SideNormal[side] * .4f + Vector3.up : BaseCentre;
            // Ship Launch Bay level 2 (the repaired armor's energy shield): raids hurt the base less.
            DamageBase(side, damage * rules.raidDamage * RaidDamageFactor, at);
        }

        /// <summary>A hit on one side of the base: the shield takes it first, then the HP; the side's room breaks once that side has
        /// taken sideBreakShare of the max HP today. HP at zero loses the run.</summary>
        public void DamageBase(int side, float amount, Vector3 at)
        {
            if (!Running || BaseDestroyed || amount <= 0f) return;
            float toShield = Mathf.Min(BaseShield, amount);
            BaseShield -= toShield;
            float toHp = amount - toShield;
            BaseHp -= toHp;
            CampaignEvents.RaiseBaseDamaged(amount, toHp, SideRoom[side]);
            sideFlash[side] = 1f;
            gm.hud?.DamageNumber(at, amount, toHp > 0f ? new Color(1f, .55f, .2f) : new Color(.5f, .9f, 1f), false);
            Fx.Pop(at, toHp > 0f ? new Color(1f, .5f, .2f) : new Color(.5f, .9f, 1f), .9f);
            if (toHp > 0f)
            {
                sideDamageToday[side] += toHp;
                var room = rooms[(int)SideRoom[side]];
                if (!room.damaged && room.level > 0 && sideDamageToday[side] >= BaseMaxHp * rules.sideBreakShare)
                {
                    room.damaged = true;
                    SayRoomOffline(SideRoom[side]);
                }
            }
            if (BaseHp <= 0f)
            {
                BaseHp = 0f; BaseDestroyed = true;
                V3WorldAudio.Instance?.Play(ArenaWorldCue.BossDeath, BaseCentre);
                gm.Shake(.6f);
            }
        }

        /// <summary>Dusk: the base repairs part of its hull by itself; with central control working the shield refills; side damage
        /// counts from zero again tomorrow.</summary>
        void NightlyBaseUpkeep()
        {
            BaseHp = Mathf.Min(BaseMaxHp, BaseHp + BaseMaxHp * rules.nightSelfRepair);
            if (ShieldLevel > 0 && !rooms[(int)CampaignRoom.Control].damaged) { BaseShield = BaseMaxShield; Say(Line.S06); }
            System.Array.Clear(sideDamageToday, 0, 4);
        }

        // ───── Purchases (the base panel calls these) ─────

        public bool ControlWorks => !rooms[(int)CampaignRoom.Control].damaged;

        /// <summary>The price of a room's next level (since 2026-11-10 the V2 doc's table: CampaignRules.RoomLevelPrice), or null at the top.</summary>
        public CampaignCost? NextLevelCost(CampaignRoom room) => rules.RoomLevelPrice(room, rooms[(int)room].level + 1);

        /// <summary>Build (level 0 to 1) or upgrade a room. Needs central control working and the room intact.</summary>
        public bool TryUpgradeRoom(CampaignRoom room)
        {
            var cost = NextLevelCost(room);
            if (cost == null || !ControlWorks || rooms[(int)room].damaged || !TrySpend(cost.Value)) return false;
            rooms[(int)room].level++;
            OnRoomLevelBought(room);
            Bought("room_upgrade", $"{room} level {rooms[(int)room].level}", room, cost.Value);
            ui.Flash(ArenaText.Pair($"{RoomName(room)}升到 {rooms[(int)room].level} 级", $"{RoomName(room)} reached level {rooms[(int)room].level}"));
            return true;
        }

        /// <summary>Since 2026-11-10 every repair of a broken room costs the same (CampaignRules.roomRepairAlloy), central control too.</summary>
        public int RepairCost(CampaignRoom room) => rules.roomRepairAlloy;

        /// <summary>Repair a broken room: central control by hand, every other room only through a working central control (D7-A).</summary>
        public bool TryRepairRoom(CampaignRoom room)
        {
            var state = rooms[(int)room];
            if (!state.damaged || room != CampaignRoom.Control && !ControlWorks) return false;
            var repair = new CampaignCost(RepairCost(room));
            if (!TrySpend(repair)) return false;
            state.damaged = false;
            Bought("repair", room.ToString(), room, repair);
            Say(Line.S05);
            return true;
        }

        /// <summary>The player's next level at the Armament Bay (since 2026-11-06 bought with currency; it gives what an XP level gives in
        /// the classic run: max HP and max energy). Since 2026-11-10 these are the five armor levels (CampaignRules.armorAlloy): armor
        /// level n is the player's level n + 1. Null at the top.</summary>
        public CampaignCost? PlayerLevelCost => gm && gm.progression && !gm.progression.AtMaxLevel ? rules.ArmorPrice(ArmorLevel) : null;
        /// <summary>Armor levels bought (0 to CampaignRules.MaxArmorLevel).</summary>
        public int ArmorLevel => gm && gm.progression ? Mathf.Max(0, gm.progression.Level - 1) : 0;

        /// <summary>Armament Bay level 1: the armor upgrade, one player level (max HP and max energy), at once.</summary>
        public bool TryBuyPlayerLevel()
        {
            var cost = PlayerLevelCost;
            if (cost == null || !PlayerLevelsForSale || !TrySpend(cost.Value)) return false;
            gm.progression.BuyLevel();
            Bought("player_level", $"level {gm.progression.Level}, armor {ArmorLevel}", CampaignRoom.Armament, cost.Value);
            ui.Flash(ArenaText.Pair($"装甲升到 {ArmorLevel} 级：最大生命 +{Mathf.RoundToInt(gm.progression.LastLevelHpGain)}、最大能量 +{Mathf.RoundToInt(gm.progression.LastLevelEnergyGain)}",
                $"Armor level {ArmorLevel}: max HP +{Mathf.RoundToInt(gm.progression.LastLevelHpGain)}, max energy +{Mathf.RoundToInt(gm.progression.LastLevelEnergyGain)}"));
            return true;
        }

        // ----- Room upgrades: since 2026-11-06 the V2 doc's rooms sell what the classic run dropped or drew, and a purchase takes effect
        // the moment it is made (no build phase, no picks waiting). Each kind counts its own purchases for its rising price. -----

        int newWeaponsBought;
        readonly int[] upgradesBought = new int[5];

        public bool WeaponDamageForSale => Running && Works(CampaignRoom.Armament, 1);
        public bool NewWeaponForSale => Running && Works(CampaignRoom.Armament, 2);
        /// <summary>The next Mk's price (since 2026-11-10 CampaignRules.weaponMkAlloy, after the shared Mk or, with sharedWeaponMk off, the
        /// Mk of the weapon that would rise); a price no alloy pays at the top Mk.</summary>
        public CampaignCost WeaponDamageCost => rules.MkPrice(NextMkFrom) ?? new CampaignCost(int.MaxValue);
        public CampaignCost NewWeaponCost => CampaignRules.Rising(rules.newWeaponCost, rules.newWeaponCostPerBuy, newWeaponsBought);
        /// <summary>Every hand and auto slot holds a weapon and no hand holds the starting pistol (which a new hand weapon replaces): a new
        /// weapon has nowhere to go.</summary>
        public bool WeaponSlotsFull => gm && gm.runWeapons && !System.Array.Exists(gm.runWeapons.Hand, w => w == null || w.form == WeaponForm.Pistol)
            && !System.Array.Exists(gm.runWeapons.Auto, w => w == null);
        /// <summary>Some hand or auto slot holds an anomaly weapon (anything but the starting pistol): only those take weapon damage boosts.</summary>
        public bool HoldsAnomalyWeapon
        {
            get
            {
                var weapons = gm ? gm.runWeapons : null;
                if (!weapons) return false;
                foreach (var w in weapons.Hand) if (w != null && w.IsAnomalous) return true;
                foreach (var w in weapons.Auto) if (w != null && w.IsAnomalous) return true;
                return false;
            }
        }
        /// <summary>Both hands hold weapons, one of them the starting pistol: a newly researched hand weapon takes the pistol's place.</summary>
        public bool NewWeaponReplacesPistol => gm && gm.runWeapons && PistolHandIfFull(gm.runWeapons) >= 0;
        /// <summary>The hand holding the starting pistol when no hand is free, else -1.</summary>
        static int PistolHandIfFull(RunWeapons weapons)
        {
            if (System.Array.Exists(weapons.Hand, w => w == null)) return -1;
            return System.Array.FindIndex(weapons.Hand, w => w != null && w.form == WeaponForm.Pistol);
        }

        public static CampaignRoom UpgradeRoom(CampaignUpgrade kind) => kind is CampaignUpgrade.Tame or CampaignUpgrade.Twist ? CampaignRoom.Armament : CampaignRoom.Energy;
        public static int UpgradeLevel(CampaignUpgrade kind) => kind switch
        {
            CampaignUpgrade.Stat => 1,
            CampaignUpgrade.Skill or CampaignUpgrade.Tame => 2,
            _ => 3 // Infusion (Energy Lab 3), Twist (Armament Bay 3)
        };
        public bool UpgradeForSale(CampaignUpgrade kind) => Running && Works(UpgradeRoom(kind), UpgradeLevel(kind));
        public CampaignCost UpgradeCost(CampaignUpgrade kind)
        {
            int bought = upgradesBought[(int)kind];
            return kind switch
            {
                CampaignUpgrade.Stat => CampaignRules.Rising(rules.statCost, rules.statCostPerBuy, bought),
                CampaignUpgrade.Skill => CampaignRules.Rising(rules.skillCost, rules.skillCostPerBuy, bought),
                CampaignUpgrade.Infusion => CampaignRules.Rising(rules.infusionCost, rules.infusionCostPerBuy, bought),
                CampaignUpgrade.Tame => CampaignRules.Rising(rules.tameCost, rules.tameCostPerBuy, bought),
                _ => CampaignRules.Rising(rules.twistCost, rules.twistCostPerBuy, bought)
            };
        }
        public static string UpgradeName(CampaignUpgrade kind) => kind switch
        {
            CampaignUpgrade.Stat => ArenaText.Pair("升级体质", "Upgrade the body"),
            CampaignUpgrade.Skill => ArenaText.Pair("解锁异星技能", "Unlock an alien skill"),
            CampaignUpgrade.Infusion => ArenaText.Pair("武器附魔", "Infuse a weapon"),
            CampaignUpgrade.Tame => ArenaText.Pair("驯化武器", "Tame a weapon"),
            _ => ArenaText.Pair("武器变招", "Twist a weapon")
        };

        /// <summary>Armament Bay level 1: the next weapon Mk (since 2026-11-10 bought with CampaignRules.weaponMkAlloy). With the shared Mk
        /// (CampaignRules.sharedWeaponMk, the rule chosen on 2026-11-10) it is the bay's level for every anomaly weapon, held now or taken
        /// later, so it can be bought before any is held; with it off the weapon in hand rises, or else the first other anomaly weapon held.
        /// The starting pistol never takes a Mk. Alloy buys up to Mk 10; Mk 11 is the void alloy's (SetTeamMk).</summary>
        public bool TryBuyWeaponDamage()
        {
            if (!WeaponDamageForSale || !gm || !gm.runWeapons) return false;
            var target = rules.sharedWeaponMk ? null : MkTarget;
            if (!rules.sharedWeaponMk && target == null)
            {
                V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
                ui.Flash(ArenaText.Pair("小手枪不能升级：先在武装区研究一把异常武器", "The pistol takes no Mk: research an anomaly weapon at the Armament Bay first"));
                return false;
            }
            int before = NextMkFrom;
            var price = rules.MkPrice(before);
            if (price == null)
            {
                V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
                ui.Flash(ArenaText.Pair($"武器型号已经是合金能升到的最高级（Mk {before}）", $"The Mk is already the highest alloy buys (Mk {before})"));
                return false;
            }
            if (!TrySpend(price.Value)) return false;
            float damageBefore = rules.MkDamage(before), damageAfter = rules.MkDamage(before + 1);
            if (target == null) SetTeamMk(before + 1);
            else target.mk = before + 1;
            Bought("weapon_mk", target == null ? $"shared Mk{before + 1}" : $"{target.form} Mk{before + 1}", CampaignRoom.Armament, price.Value);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            ui.Flash(target == null
                ? ArenaText.Pair($"全部异常武器升到 Mk {before + 1}：伤害 ×{damageBefore:0.##} → ×{damageAfter:0.##}", $"Every anomaly weapon is now Mk {before + 1}: damage ×{damageBefore:0.##} → ×{damageAfter:0.##}")
                : ArenaText.Pair($"{target.FormName} 升到 Mk {before + 1}：伤害 ×{damageBefore:0.##} → ×{damageAfter:0.##}", $"{target.FormName} is now Mk {before + 1}: damage ×{damageBefore:0.##} → ×{damageAfter:0.##}"));
            return true;
        }

        /// <summary>Armament Bay level 2: research a new weapon, always an anomaly weapon. It goes straight into a free hand or auto slot; a
        /// hand weapon with both hands taken replaces the starting pistol (since 2026-11-06). With nowhere to put it, or no weapon left that
        /// fits, nothing is spent. Once bought, its weapon card shows; closing it returns to the base panel.</summary>
        public bool TryBuyNewWeapon()
        {
            var weapons = gm ? gm.runWeapons : null;
            if (!NewWeaponForSale || !weapons || !gm.waves) return false;
            if (WeaponSlotsFull) { ui.Flash(ArenaText.Pair("武器栏已满：没有空位放新武器", "Every weapon slot is taken: no room for a new weapon")); return false; }
            var cost = NewWeaponCost;
            if (!CanAfford(cost)) { TrySpend(cost); return false; }
            RunWeapon got = null;
            string message = "";
            armoryMode = ArmoryMode.NewWeaponsOnly;
            try
            {
                for (int attempt = 0; attempt < 3 && got == null; attempt++)
                {
                    weapons.RollArmory(gm.waves.WaveIndex + 2);
                    var offers = weapons.Offers;
                    for (int i = 0; i < offers.Length; i++)
                    {
                        var weapon = offers[i];
                        if (weapon == null || weapon.upgradeOf != null || !weapon.IsAnomalous) continue;
                        int replace = weapon.IsAuto ? -1 : PistolHandIfFull(weapons);
                        if (replace < 0 && !System.Array.Exists(weapon.IsAuto ? weapons.Auto : weapons.Hand, w => w == null)) continue;
                        var pistol = replace >= 0 ? weapons.Hand[replace] : null;
                        if (!TrySpend(cost)) return false;
                        if (!weapons.TakeOffer(i, replace)) { Refund(cost); return false; }
                        got = weapon;
                        newWeaponsBought++;
                        Bought("new_weapon", $"{weapon.form} Mk{weapon.mk} {(weapon.IsAuto ? "auto" : "hand")}{(pistol != null ? ", replaces Pistol" : "")}", CampaignRoom.Armament, cost);
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
                        message = weapon.IsAuto
                            ? ArenaText.Pair($"研究出新的异常武器：{weapon.FormName}（{weapon.MkLabel}，自动武器栏）", $"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel}, auto slot)")
                            : pistol != null
                                ? ArenaText.Pair($"研究出新的异常武器：{weapon.FormName}（{weapon.MkLabel}），换下了{pistol.FormName}", $"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel}); it replaces the {pistol.FormName}")
                                : ArenaText.Pair($"研究出新的异常武器：{weapon.FormName}（{weapon.MkLabel}，手持武器栏）", $"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel}, hand slot)");
                        break;
                    }
                }
            }
            finally
            {
                armoryMode = ArmoryMode.Rooms;
                weapons.Offers = new RunWeapon[RunWeapons.ArmoryOfferCount];
            }
            if (got == null)
            {
                ui.Flash(ArenaText.Pair("暂时没有可研究的新武器", "No new weapon can be researched right now"));
                return false;
            }
            AnnounceWeapon(got, message);
            return true;
        }

        /// <summary>A weapon just bought at the base shows its weapon card, as a weapon taken in the classic run does (the card says how it
        /// attacks and its trait: the anomaly the button promised); closing it returns to the base panel on the same tab to go on buying,
        /// and only then the message says where the weapon went. Without the base panel open (a purchase made by an automated check) only
        /// the message shows.</summary>
        void AnnounceWeapon(RunWeapon weapon, string message)
        {
            if (weapon == null || !ui.BaseOpen || Stage != CampaignStage.Base) { ui.Flash(message, 3.5f); return; }
            ui.ClearFlash();
            // A weapon bought in the study window (since 2026-11-10) brings the window back on the same tab after the card.
            bool study = ui.StudyOpen;
            ui.HideStudy();
            ui.HideBase();
            gm.CampaignShowWeaponCard(weapon, () =>
            {
                if (!Running || Stage != CampaignStage.Base) return;
                gm.CampaignSetPhase(ArenaUiState.Base);
                ui.ResumeBase();
                if (study) ui.ResumeStudy();
                ui.Flash(message, 3.5f);
            });
        }

        /// <summary>One upgrade of a kind, at once: the body (a stat) and alien skills at the Energy Lab levels 1 and 2, weapon Infusion at
        /// its level 3; Tame and Twist at the Armament Bay levels 2 and 3. GameManager.CampaignApplyUpgrade draws and applies one random
        /// upgrade while only this kind is allowed. Nothing to give (no weapon can take it, a new skill with every slot taken): nothing is
        /// spent. The money is taken only once something was given.</summary>
        public bool TryBuyUpgrade(CampaignUpgrade kind)
        {
            if (!UpgradeForSale(kind) || !gm || !gm.progression) return false;
            var cost = UpgradeCost(kind);
            if (!CanAfford(cost)) { TrySpend(cost); return false; }
            int bankedBefore = gm.progression.BankedDrafts;
            string got = null;
            activeUpgrade = kind;
            try
            {
                // A skill draw can come up with only a new skill while every skill slot is taken (it cannot be learned without a choice):
                // a second or third draw usually brings a level for a learned skill instead.
                for (int attempt = 0; attempt < 3 && string.IsNullOrEmpty(got); attempt++)
                {
                    got = gm.CampaignApplyUpgrade();
                    // A pick the draw could not use must not wait for a later purchase, which would then give it away for free.
                    while (gm.progression.BankedDrafts > bankedBefore) gm.progression.ConsumeDraft();
                }
            }
            finally { activeUpgrade = null; }
            if (string.IsNullOrEmpty(got))
            {
                ui.Flash(ArenaText.Pair($"{UpgradeName(kind)}：暂时没有可升级的项目", $"{UpgradeName(kind)}: nothing can be upgraded right now"));
                return false;
            }
            // Affordable a moment ago and nothing since touched the stock, so this always succeeds.
            if (!TrySpend(cost)) return false;
            upgradesBought[(int)kind]++;
            Bought(UpgradeItem(kind), got, UpgradeRoom(kind), cost);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            ui.Flash(ArenaText.Pair($"{UpgradeName(kind)}：{got}", $"{UpgradeName(kind)}: {got}"));
            return true;
        }

        // ----- The rocket launcher (since 2026-11-06 the run starts with the pistol alone; the rocket launcher, an anomaly weapon, is bought) -----

        public bool RocketForSale => Running && Works(CampaignRoom.Armament, 2) && gm && gm.runWeapons && !gm.runWeapons.OwnsForm(WeaponForm.RocketLauncher);
        public CampaignCost RocketCost => rules.rocketLauncherCost;
        /// <summary>The hand the rocket launcher goes into: a free hand first; with both hands holding weapons, the starting pistol's (as a
        /// researched hand weapon takes it, TryBuyNewWeapon), else the one not in use (switch weapons first to keep it).</summary>
        int RocketHand
        {
            get
            {
                var weapons = gm ? gm.runWeapons : null;
                if (!weapons) return 1;
                int free = System.Array.FindIndex(weapons.Hand, w => w == null);
                if (free >= 0) return free;
                int pistol = PistolHandIfFull(weapons);
                return pistol >= 0 ? pistol : 1 - weapons.ActiveIndex;
            }
        }
        /// <summary>The weapon the rocket launcher would replace, or null when it goes into a free hand.</summary>
        public RunWeapon RocketReplaces => gm && gm.runWeapons ? gm.runWeapons.Hand[RocketHand] : null;
        public bool TryBuyRocketLauncher()
        {
            var weapons = gm ? gm.runWeapons : null;
            if (!RocketForSale || !weapons || !TrySpend(RocketCost)) return false;
            int hand = RocketHand;
            var old = weapons.GiveToHand(hand, WeaponForm.RocketLauncher);
            var rocket = weapons.Hand[hand];
            Bought("new_weapon", old != null ? $"RocketLauncher Mk1 hand, replaces {old.form}" : "RocketLauncher Mk1 hand", CampaignRoom.Armament, RocketCost);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            AnnounceWeapon(rocket, old != null ? ArenaText.Pair($"解锁火箭筒（异常武器，Mk 1）：换下了{old.FormName}", $"Rocket launcher unlocked (an anomaly weapon, Mk 1): it replaces the {old.FormName}")
                : ArenaText.Pair("解锁火箭筒（异常武器，Mk 1）：放进了空着的手", "Rocket launcher unlocked (an anomaly weapon, Mk 1): it goes into the free hand"));
            return true;
        }

        /// <summary>A purchase was paid and given: the analytics record (CampaignEvents.Purchased, one v2_purchase row).</summary>
        void Bought(string item, string detail, CampaignRoom room, CampaignCost cost) => CampaignEvents.RaisePurchased(item, detail, room, cost, Day);
        static string UpgradeItem(CampaignUpgrade kind) => kind switch
        {
            CampaignUpgrade.Stat => "constitution",
            CampaignUpgrade.Skill => "unlock_skill",
            CampaignUpgrade.Infusion => "weapon_infusion",
            CampaignUpgrade.Tame => "weapon_tame",
            _ => "weapon_twist"
        };

        /// <summary>Gives a price back after a purchase that failed once paid: straight into the stock, neither income nor tonight's haul.</summary>
        void Refund(CampaignCost cost) { alloy += cost.alloy; energy += cost.energy; dark += cost.dark; }

        // Kept only so Editor/V2CampaignCheck.cs (its Shop step) still compiles; delete these three with its calls once it uses
        // TryBuyWeaponDamage, TryBuyNewWeapon and TryBuyUpgrade.
        public int ResearchQueued => 0;
        public bool TryBuyWeaponResearch() => TryBuyWeaponDamage();
        public bool TryBuyCardPick(CampaignRoom room) => TryBuyUpgrade(room == CampaignRoom.Energy ? CampaignUpgrade.Stat : CampaignUpgrade.Tame);

        /// <summary>The base's next level (since 2026-11-10 CampaignRules.baseLevelAlloy), or null at the top.</summary>
        public CampaignCost? DurabilityCost => rules.BaseLevelPrice(Durability);
        public bool TryUpgradeDurability()
        {
            var cost = DurabilityCost;
            if (cost == null || !ControlWorks || !TrySpend(cost.Value)) return false;
            float share = BaseHp / BaseMaxHp;
            Durability++;
            Bought("room_upgrade", $"durability {Durability}", CampaignRoom.Control, cost.Value);
            BaseHp = Mathf.Max(BaseHp, share * BaseMaxHp);
            BaseShield = Mathf.Min(BaseShield, BaseMaxShield);
            return true;
        }

        /// <summary>The shield's next level (since 2026-11-10 three levels, CampaignRules.shieldEnergy), or null at the top.</summary>
        public CampaignCost? ShieldCost => rules.ShieldPrice(ShieldLevel);
        public bool TryUpgradeShield()
        {
            var cost = ShieldCost;
            if (cost == null || !ControlWorks || !TrySpend(cost.Value)) return false;
            ShieldLevel++;
            Bought("shield", $"level {ShieldLevel}", CampaignRoom.Control, cost.Value);
            BaseShield = BaseMaxShield; // a new shield level comes charged
            return true;
        }

        public int HullRepairCost => rules.HullRepairCost(1f - BaseHp / BaseMaxHp, Durability);
        public bool TryRepairHull()
        {
            if (BaseHp >= BaseMaxHp - .5f || !ControlWorks) return false;
            var hull = new CampaignCost(HullRepairCost);
            if (!TrySpend(hull)) return false;
            BaseHp = BaseMaxHp;
            Bought("repair", "hull", CampaignRoom.Control, hull);
            CampaignAudio.Play(CampaignSfx.RoomRepair);
            return true;
        }

        bool ShipWorks(int level) => rooms[(int)CampaignRoom.Ship].level >= level && !rooms[(int)CampaignRoom.Ship].damaged;
        public bool CanRepairEngine => !engineFixed && ShipWorks(1);
        public bool CanRepairArmor => !armorFixed && ShipWorks(2);
            
        public bool TryRepairEngine()
        {
            if (!CanRepairEngine || !TrySpend(rules.engineRepair)) return false;
            engineFixed = true; RefreshShip(); Say(Line.S08);
            Bought("ship_part", "engine", CampaignRoom.Ship, rules.engineRepair);
            return true;
        }

        public bool TryRepairArmor()
        {
            if (!CanRepairArmor || !TrySpend(rules.armorRepair)) return false;
            armorFixed = true; RefreshShip(); Say(Line.S09);
            Bought("ship_part", "armor", CampaignRoom.Ship, rules.armorRepair);
            return true;
        }

        public bool TrySynthesiseFuel()
        {
            if (!CanSynthesiseFuel) return false;

            int remainingCapacity = FuelUnits - fuel;
            int amount = Mathf.Min(Dark, remainingCapacity);

            if (amount <= 0) return false;

            // The existing Dark inventory holds the nighttime resource.
            var cost = new CampaignCost(0, 0, amount);

            if (!TrySpend(cost)) return false;

            fuel += amount;
            RefreshShip();

            Bought(
                "fuel",
                $"Deposited {amount}; tank {fuel}/{FuelUnits}",
                CampaignRoom.Ship,
                cost);

            return true;
        }

        public bool CanIgnite => FuelFull && engineFixed && armorFixed;

        public bool TryCraftLamp()
        {
            if (!LampsCraftable || !TrySpend(new CampaignCost(rules.lampCost))) return false;
            lampStock++;
            Bought("lamp", $"stock {lampStock}", CampaignRoom.Ship, new CampaignCost(rules.lampCost));
            return true;
        }

        public bool CanTopUpShield => ShieldLevel > 0 && BaseShield < BaseMaxShield - .5f && Time.time >= shieldTopUpAt && ControlWorks;
        public bool TryTopUpShield()
        {
            if (!CanTopUpShield || !TrySpend(new CampaignCost(0, rules.ShieldTopUpCost(Day)))) return false;
            BaseShield = Mathf.Min(BaseMaxShield, BaseShield + BaseMaxShield * rules.shieldTopUpShare);
            shieldTopUpAt = Time.time + rules.shieldTopUpCooldown;
            Bought("shield", "top_up", CampaignRoom.Control, new CampaignCost(0, rules.ShieldTopUpCost(Day)));
            Fx.Ring(BaseCentre, BaseHalf + 2f, new Color(.5f, .9f, 1f), .5f);
            return true;
        }

        public static string RoomName(CampaignRoom room) => room switch
        {
            CampaignRoom.Control => ArenaText.Pair("总控区", "Central Control"),
            CampaignRoom.Armament => ArenaText.Pair("武装区", "Armament Bay"),
            CampaignRoom.Energy => ArenaText.Pair("能量研究区", "Energy Lab"),
            _ => ArenaText.Pair("飞船发射区", "Ship Launch Bay")
        };

        // ───── Day interaction at the base door ─────

        void TickDayInteraction(float dt)
        {
            var p = gm.player;
            if (!p || !p.IsAlive) { prompt = ""; return; }
            bool atDoor = Query.Flat(p.Position - DoorPoint).magnitude < 3.2f;
            if (atDoor && ShieldLevel > 0)
            {
                prompt = CanTopUpShield ? ArenaText.Pair($"按 {ArenaKeys.InteractKey} 紧急补充护盾（能量 {rules.ShieldTopUpCost(Day)}）", $"{ArenaKeys.InteractKey}: emergency shield top-up ({rules.ShieldTopUpCost(Day)} energy)")
                    : ArenaText.Pair("护盾补充冷却中", "Shield top-up cooling down");
                if (EPressed && !TryTopUpShield()) ui.Flash(ArenaText.Pair("能量不够，或护盾已满", "Not enough energy, or the shield is full"));
            }
            else prompt = "";
        }
    }
}
