using System.Collections.Generic;
using UnityEngine;
using Prim = AnomalyArena.Whitebox.Prim;

namespace AnomalyArena
{
    /// <summary>
    /// The memory (V2 doc section 2), played as Day 0 passes into night: the alien invasion, the great conscription, the grandmother who
    /// raised Felix, the cabin before the jump, the turbulence in the wormhole, the fall, Felix alone by the wreck, the ship deploying the
    /// base by itself, and the base's lamps coming on beside the wreck at night. One line each, in Felix's words; the five that were
    /// recorded keep their voices (C01 to C05).
    /// </summary>
    public sealed partial class V2Cutscene
    {
        /// <summary>The memory's nine shots, M1 to M9, under fifty seconds in all.</summary>
        public static readonly CutsceneShot[] Memory =
        {
            new("M1", 5.5f, StageInvasion, CutsceneBeat.Say(V2Campaign.Line.C06, .6f)),
            new("M2", 5f, StageConscription, CutsceneBeat.Say(V2Campaign.Line.C07, .5f)),
            new("M3", 5f, StageGrandmother, CutsceneBeat.Say(V2Campaign.Line.C08, .6f)),
            new("M4", 5f, StageCabin, CutsceneBeat.Say(V2Campaign.Line.C01, .4f)),
            new("M5", 5.5f, StageWormhole, CutsceneBeat.Sound(CampaignSfx.WormholeTurbulence, 0f), CutsceneBeat.Say(V2Campaign.Line.C02, .4f)),
            new("M6", 5f, StageFall, CutsceneBeat.Sound(CampaignSfx.ShipCrash, 1f), CutsceneBeat.Say(V2Campaign.Line.C09, .8f)),
            new("M7", 5.5f, StageAlone, CutsceneBeat.Say(V2Campaign.Line.C03, .4f)),
            new("M8", 6.5f, StageDeploy, CutsceneBeat.Sound(CampaignSfx.BaseDeploy, 0f), CutsceneBeat.Say(V2Campaign.Line.C04, .3f)),
            new("M9", 5.5f, StageNightBase, CutsceneBeat.Say(V2Campaign.Line.C05, 1.8f)),
        };

        static readonly Color CitySky = new(.07f, .09f, .18f), CityGround = new(.11f, .11f, .13f), TowerWhite = new(.84f, .84f, .8f), TowerGrey = new(.68f, .7f, .74f);
        static readonly Color Beam = new(.75f, .95f, 1f);
        static readonly Color HallSky = new(.1f, .11f, .14f), HallWall = new(.22f, .24f, .29f), HallFloor = new(.17f, .19f, .23f), StripDim = new(.08f, .3f, .38f);
        static readonly Color MorningSky = new(.8f, .81f, .84f), Pavement = new(.52f, .5f, .47f), Street = new(.6f, .61f, .64f);
        static readonly Color HouseWall = new(.74f, .75f, .77f), HouseRoof = new(.6f, .61f, .64f), Doorway = new(.08f, .08f, .1f), Wood = new(.5f, .42f, .34f);
        static readonly Color Grandmother = new(.8f, .8f, .84f), KitBag = new(.33f, .34f, .38f);
        static readonly Color CabinSky = new(.015f, .02f, .05f), Locker = new(.86f, .86f, .82f), PaleStar = new(.65f, .8f, 1f);
        static readonly Color WormholeSky = new(.02f, .01f, .035f), FrameDark = new(.3f, .08f, .5f), FrameBright = new(.6f, .3f, .95f), Turbulence = new(.8f, .7f, 1f);
        static readonly Color NightGround = new(.1f, .115f, .16f), NightTint = new(.09f, .11f, .17f), LampDim = new(.3f, .28f, .22f);

        // ───── M1: the alien invasion of a human city at night, far and high ─────

        static void StageInvasion(V2Cutscene s)
        {
            s.Sky(CitySky);
            s.Ground(CityGround);
            var random = new System.Random(11);
            // Towers in off-white and pale grey, each with one cyan strip on the side facing the camera; taller toward the middle.
            var white = new List<Matrix4x4>();
            var grey = new List<Matrix4x4>();
            var strips = new List<Matrix4x4>();
            var tops = new List<Vector3>();
            for (int gx = -12; gx <= 12; gx++)
                for (int gz = 0; gz < 22; gz++)
                {
                    float x = gx * 7f + R(random, -1.5f, 1.5f), z = 30f + gz * 7f + R(random, -1.5f, 1.5f);
                    float w = R(random, 2.6f, 5f), d = R(random, 2.6f, 5f), tall = R(random, 0f, 1f);
                    float middle = 1f - Mathf.Min(1f, Mathf.Abs(x) / 90f);
                    float h = R(random, 4f, 10f) + tall * tall * 22f * (.4f + .6f * middle);
                    (random.NextDouble() < .5 ? white : grey).Add(Matrix4x4.TRS(new Vector3(x, h * .5f, z), Quaternion.identity, new Vector3(w, h, d)));
                    strips.Add(Matrix4x4.TRS(new Vector3(x + R(random, -w * .3f, w * .3f), h * .45f, z - d * .5f - .06f), Quaternion.identity,
                        new Vector3(.35f, h * .7f, .12f)));
                    tops.Add(new Vector3(x, h, z));
                }

            s.Batch(Prim.Box, white, TowerWhite, "Towers");
            s.Batch(Prim.Box, grey, TowerGrey, "Towers");
            s.Batch(Prim.Box, strips, Table("door"), "Tower strips");
            s.Box(new Vector3(1.8f, 700f, 1.8f), new Vector3(14f, 350f, 130f), TowerWhite); // the space elevator, out of the top of the frame
            // The invaders: black pyramids and octahedra hanging over the city, each with a pale-yellow spike; beams from some of them.
            var invaders = s.Node("Invaders", Vector3.zero);
            var beams = new List<(MeshRenderer beam, float rate, float phase)>();
            for (int i = 0; i < 30; i++)
            {
                float x = R(random, -110f, 110f), y = R(random, 46f, 82f), z = R(random, 110f, 240f), size = R(random, 4f, 11f);
                bool pyramid = random.NextDouble() < .5;
                s.Solid(pyramid ? Prim.Pyramid : Prim.Octa, pyramid ? new Vector3(size, size * .9f, size) : new Vector3(size, size * 1.3f, size), new Vector3(x, y, z),
                    Table("black"), new Vector3(pyramid ? 180f : 0f, R(random, 0f, 90f), 0f), invaders);
                s.Solid(Prim.Pyramid, new Vector3(size * .22f, size * .4f, size * .22f), new Vector3(x, y - size * .62f, z), Table("tyrantHorn"), new Vector3(180f, 0f, 0f),
                    invaders);
                if (i % 5 != 1) continue;
                float bottom = y - size * .8f, length = bottom + 12f; // down into the ground, so the beam still reaches it as they sink
                beams.Add((s.Box(new Vector3(.6f, length, .6f), new Vector3(x, bottom - length * .5f, z), Beam, default, invaders), R(random, 1.5f, 3f), R(random, 0f, 1f)));
            }

            // Burning towers: the five tallest near the middle.
            tops.Sort((a, b) => (Mathf.Abs(a.x) < 55f ? -a.y : 0f).CompareTo(Mathf.Abs(b.x) < 55f ? -b.y : 0f));
            var burning = tops.GetRange(0, 5);
            var fire = new Spout();
            var smoke = new Spout();
            // A handful of defence ships climbing toward the invaders: an off-white hull with a cyan tail.
            var ships = new List<(Transform ship, Vector3 from)>();
            for (int k = 0; k < 6; k++)
            {
                var from = new Vector3(-60f + k * 23f, 14f + k * 3f, 60f + k * 9f);
                var ship = s.Node("Defence ship", from, new Vector3(-32f, 10f - k * 4f, 0f));
                s.Box(new Vector3(2f, 1.2f, 5.2f), Vector3.zero, TowerWhite, default, ship);
                s.Box(new Vector3(1.2f, 1f, 1.1f), new Vector3(0f, 0f, -3.1f), Table("door"), default, ship);
                ships.Add((ship, from));
            }

            float seconds = s.Seconds;
            s.Every(t =>
            {
                invaders.localPosition = new Vector3(0f, -4f * t / seconds, 0f);
                foreach (var (beam, rate, phase) in beams) beam.enabled = Mathf.Repeat(t * rate + phase, 1f) < .7f;
                foreach (var (ship, from) in ships) ship.localPosition = from + ship.localRotation * Vector3.forward * (6f * t);
                for (int n = fire.Take(45f, s.Step); n > 0; n--)
                {
                    var top = burning[UnityEngine.Random.Range(0, burning.Count)];
                    s.Emit(top + new Vector3(J(-1.2f, 1.2f), .6f, J(-1.2f, 1.2f)), new Vector3(J(-.4f, .4f), J(2.5f, 4f), J(-.4f, .4f)), Table("fire"), J(1f, 1.6f), J(.8f, 1.2f));
                }

                for (int n = smoke.Take(25f, s.Step); n > 0; n--)
                {
                    var top = burning[UnityEngine.Random.Range(0, burning.Count)];
                    s.Emit(top + new Vector3(J(-1f, 1f), 3.5f, J(-1f, 1f)), new Vector3(J(-.5f, .5f), J(1.2f, 2f), J(-.5f, .5f)), Smoke, J(1.4f, 2.2f), J(1.8f, 2.6f));
                }
            });
            s.CameraMove(new Vector3(0f, 48f, -60f), new Vector3(0f, 20f, 80f), new Vector3(0f, 45f, -47f), new Vector3(0f, 20f, 82f), 42f);
            s.Subject(s.Node("City", new Vector3(0f, 15f, 100f)));
        }

        // ───── M2: the great conscription, ranks of soldiers in a hall, ships along the walls ─────

        static void StageConscription(V2Cutscene s)
        {
            s.Sky(HallSky);
            s.Box(new Vector3(96f, 1f, 330f), new Vector3(0f, -.5f, 135f), HallFloor);
            s.Box(new Vector3(2f, 40f, 330f), new Vector3(-48f, 20f, 135f), HallWall);
            s.Box(new Vector3(2f, 40f, 330f), new Vector3(48f, 20f, 135f), HallWall);
            s.Box(new Vector3(96f, 40f, 2f), new Vector3(0f, 20f, 290f), HallWall);
            // Every soldier is Felix's box, all facing the camera: blocks of four ranks with an aisle between blocks, a cyan strip in each.
            const int columns = 22, blocks = 10, ranks = 4;
            const float columnStep = 1.9f, rankStep = 2.6f, blockStep = 15.4f, firstRank = 20f, aisle = 11.6f;
            var facing = Quaternion.Euler(0f, 180f, 0f);
            var bodies = new List<Matrix4x4>();
            var marks = new List<Matrix4x4>();
            var movers = new List<Vector3>();
            var random = new System.Random(2);
            for (int b = 0; b < blocks; b++)
                for (int r = 0; r < ranks; r++)
                    for (int c = 0; c < columns; c++)
                    {
                        var at = new Vector3((c - (columns - 1) * .5f) * columnStep, 0f, firstRank + b * blockStep + r * rankStep);
                        // A few in the far ranks are figures of their own, so they can shift a little.
                        if (b >= 6 && movers.Count < 6 && random.NextDouble() < .03) { movers.Add(at); continue; }
                        bodies.Add(Matrix4x4.TRS(at + facing * new Vector3(0f, 1f, 0f), facing, new Vector3(1f, 2f, 1f)));
                        marks.Add(Matrix4x4.TRS(at + facing * new Vector3(0f, 1.55f, .6f), facing, new Vector3(.6f, .4f, .5f)));
                    }

            s.Batch(Prim.Box, bodies, Table("player"), "Soldiers");
            s.Batch(Prim.Wedge, marks, Table("white"), "Soldiers' front marks");
            var shifting = new List<(Transform soldier, Vector3 home, float start, float side)>();
            foreach (var at in movers)
                shifting.Add((s.Felix(at, 180f).transform, at, R(random, .6f, 3.4f), random.NextDouble() < .5 ? -.4f : .4f));
            var strips = new List<MeshRenderer>();
            for (int b = -1; b < blocks; b++)
                strips.Add(s.Box(new Vector3(44f, .06f, 1.2f), new Vector3(0f, .03f, firstRank + b * blockStep + aisle), StripDim));
            // The ships along both walls, one behind another, each in a bay marked by orange strips.
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 9; k++)
                {
                    float x = side * 36f, z = 20f + k * 28f;
                    s.Look("ship", new Vector3(x, 0f, z));
                    s.Box(new Vector3(.4f, .05f, 14f), new Vector3(x - 5f, .03f, z), Table("armament"));
                    s.Box(new Vector3(.4f, .05f, 14f), new Vector3(x + 5f, .03f, z), Table("armament"));
                }

            s.Every(t =>
            {
                // The strips light one after another down the hall.
                for (int i = 0; i < strips.Count; i++) Paint(strips[i], Mathf.Repeat(t * 1.5f - i * .14f, 1f) < .22f ? Table("door") : StripDim);
                foreach (var (soldier, home, start, side) in shifting)
                    soldier.localPosition = home + new Vector3(side * Smooth(Ramp(t, start, .5f)), 0f, 0f);
            });
            s.CameraMove(new Vector3(0f, 30f, -10f), new Vector3(0f, 0f, 75f), new Vector3(0f, 28f, 6f), new Vector3(0f, 0f, 92f), 50f);
            s.Subject(s.Node("Ranks", new Vector3(0f, 1f, 80f)));
        }

        // ───── M3: the grandmother sends Felix off at the door of their home ─────

        static void StageGrandmother(V2Cutscene s)
        {
            s.Sky(MorningSky);
            s.Ground(Pavement);
            s.Box(new Vector3(40f, .04f, 6f), new Vector3(21f, .02f, 4.5f), Street);
            // The house behind her, hollow, so a table and a chair show through its dark doorway.
            Vector3 house = new(-4.2f, 0f, 5f);
            s.Box(new Vector3(7f, 4.2f, .3f), house + new Vector3(0f, 2.1f, 4f), HouseWall);
            s.Box(new Vector3(.3f, 4.2f, 4.3f), house + new Vector3(-3.5f, 2.1f, 2f), HouseWall);
            s.Box(new Vector3(.3f, 4.2f, 4.3f), house + new Vector3(3.5f, 2.1f, 2f), HouseWall);
            s.Box(new Vector3(2.6f, 4.2f, .3f), house + new Vector3(-2.2f, 2.1f, 0f), HouseWall);
            s.Box(new Vector3(2.6f, 4.2f, .3f), house + new Vector3(2.2f, 2.1f, 0f), HouseWall);
            s.Box(new Vector3(1.8f, 1.4f, .3f), house + new Vector3(0f, 3.5f, 0f), HouseWall);
            s.Box(new Vector3(7.4f, .4f, 4.6f), house + new Vector3(0f, 4.4f, 2f), HouseRoof);
            s.Box(new Vector3(6.8f, .05f, 3.8f), house + new Vector3(0f, .03f, 2f), Doorway);
            s.Box(new Vector3(6.8f, 4f, .1f), house + new Vector3(0f, 2f, 3.8f), Doorway);
            s.Box(new Vector3(1.1f, .1f, .8f), house + new Vector3(.2f, .8f, 1.6f), Wood);
            s.Box(new Vector3(.12f, .8f, .12f), house + new Vector3(.2f, .4f, 1.6f), Wood);
            s.Box(new Vector3(.5f, .5f, .5f), house + new Vector3(-.6f, .25f, 1.7f), Wood);
            s.Box(new Vector3(.5f, .7f, .1f), house + new Vector3(-.6f, .85f, 1.95f), Wood);
            // Felix on the right facing her, she on the left facing him: two thirds of his height, light grey, a white wedge for her
            // front and a dark cube on top for her bun. Each leans from the front edge of the feet; at the end her top meets his front.
            const float height = 1.35f, herLean = 5f, hisLean = 2.5f;
            float gap = height * (Mathf.Sin(herLean * Mathf.Deg2Rad) + Mathf.Sin(hisLean * Mathf.Deg2Rad));
            var hisFeet = s.Node("Felix's feet", new Vector3(.35f, 0f, 0f));
            var felix = s.Felix(new Vector3(.5f, 0f, 0f), -90f, hisFeet);
            var herFeet = s.Node("Grandmother's feet", new Vector3(.35f - gap, 0f, 0f));
            var her = s.Node("Grandmother", new Vector3(-.375f, 0f, 0f), new Vector3(0f, 90f, 0f), herFeet);
            s.Box(new Vector3(.75f, height, .75f), new Vector3(0f, height * .5f, 0f), Grandmother, default, her);
            s.Solid(Prim.Wedge, new Vector3(.3f, .22f, .26f), new Vector3(0f, 1.08f, .48f), Table("white"), default, her);
            s.Box(new Vector3(.26f, .22f, .26f), new Vector3(0f, height + .11f, -.05f), Table("greyDark"), default, her);
            s.Box(new Vector3(.9f, .55f, .6f), new Vector3(1.6f, .275f, .4f), KitBag);
            s.Every(t =>
            {
                float k = Smooth(t / 3.5f);
                herFeet.localRotation = Quaternion.Euler(0f, 0f, -herLean * k);
                hisFeet.localRotation = Quaternion.Euler(0f, 0f, hisLean * k);
            });
            s.CameraMove(new Vector3(.2f, 1.9f, -9f), new Vector3(-.5f, 1f, 1f), new Vector3(.1f, 1.85f, -7.4f), new Vector3(-.45f, 1f, 1f), 30f);
            s.Subject(felix.transform, new Vector3(0f, 1f, 0f));
        }

        // ───── M4: Felix in the ship's cabin, the galaxy beyond the window ─────

        static void StageCabin(V2Cutscene s)
        {
            s.Sky(CabinSky);
            s.Box(new Vector3(16f, .2f, 16f), new Vector3(0f, -.1f, 0f), SlateFloor);
            // The window wall at z = 5, its opening from x -0.2 to 6.2 and y 0.9 to 4.1; lockers with orange strips on its left part.
            s.Box(new Vector3(7.8f, 5f, .3f), new Vector3(-4.1f, 2.5f, 5f), Slate);
            s.Box(new Vector3(1.6f, 5f, .3f), new Vector3(7f, 2.5f, 5f), Slate);
            s.Box(new Vector3(6.4f, .9f, .3f), new Vector3(3f, .45f, 5f), Slate);
            s.Box(new Vector3(6.4f, .9f, .3f), new Vector3(3f, 4.55f, 5f), Slate);
            s.Box(new Vector3(.3f, 5f, 16f), new Vector3(-8f, 2.5f, -2f), Slate);
            s.Box(new Vector3(.3f, 5f, 16f), new Vector3(7.8f, 2.5f, -2f), Slate);
            s.Box(new Vector3(16.3f, .3f, 16f), new Vector3(0f, 5.15f, -2f), SlateCeiling);
            for (int i = 0; i < 4; i++)
            {
                float x = -6.6f + i * 1.25f;
                s.Box(new Vector3(1f, 2.3f, .7f), new Vector3(x, 1.15f, 4.45f), Locker);
                s.Box(new Vector3(.16f, 1.6f, .05f), new Vector3(x, 1.3f, 4.08f), Table("armament"));
            }

            // Space through the window: white square stars, and a galaxy straight through the opening from where the camera ends up.
            Vector3 eye = new(.8f, 2.05f, -5.3f), opening = new(3f, 2.5f, 5f);
            Vector3 through = (opening - eye).normalized;
            var random = new System.Random(4);
            var stars = new List<Matrix4x4>();
            for (int i = 0; i < 70; i++)
                stars.Add(Matrix4x4.TRS(eye + through * R(random, 110f, 150f) + new Vector3(R(random, -45f, 45f), R(random, -25f, 25f), 0f), Quaternion.identity,
                    Vector3.one * .45f));
            var drift = s.Node("Stars", Vector3.zero);
            s.Batch(Prim.Box, stars, Color.white, "Stars", drift);
            // A loose pinwheel of three arms of small white and pale-blue cubes, its disc tipped toward the window, turning slowly.
            var galaxy = s.Node("Galaxy", eye + through * 80f, new Vector3(-62f, 10f, 0f));
            var spin = s.Node("Spin", Vector3.zero, default, galaxy);
            var whites = new List<Matrix4x4>();
            var blues = new List<Matrix4x4>();
            for (int i = 0; i < 260; i++)
            {
                float r = 1.5f + i / 260f * 16f + R(random, -1.2f, 1.2f);
                float a = i % 3 * Mathf.PI * 2f / 3f + r * .3f + R(random, -.25f, .25f), size = R(random, .3f, .6f) * (1.3f - r / 24f);
                var placement = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * r, R(random, -.3f, .3f), Mathf.Sin(a) * r),
                    Quaternion.Euler(R(random, 0f, 90f), R(random, 0f, 90f), 0f), Vector3.one * size);
                (random.NextDouble() < .5 ? whites : blues).Add(placement);
            }

            s.Batch(Prim.Box, whites, Color.white, "Galaxy", spin);
            s.Batch(Prim.Box, blues, PaleStar, "Galaxy", spin);
            s.Box(Vector3.one * 1.3f, Vector3.zero, Color.white, default, spin);
            // Felix turns until his wedge points at the window, then tilts back a little as if looking up at the galaxy.
            var felix = s.Felix(new Vector3(-1.6f, 0f, 1.4f), 5f);
            s.Every(t =>
            {
                float yaw = 5f + 47f * Smooth(t / 3.2f), back = -7f * Smooth(Ramp(t, 3.2f, 1.3f));
                felix.transform.localRotation = Quaternion.Euler(back, yaw, 0f);
                spin.localRotation = Quaternion.Euler(0f, 4f * t, 0f);
                drift.localPosition = new Vector3(-1.2f * t, .3f * t, 0f);
            });
            s.CameraMove(new Vector3(.4f, 2f, -6.6f), new Vector3(2f, 2.15f, 5f), eye, new Vector3(2.2f, 2.2f, 5f), 50f);
            s.Subject(felix.transform, new Vector3(0f, 1f, 0f));
        }

        // ───── M5: the fleet in the wormhole, torn by alien turbulence ─────

        static void StageWormhole(V2Cutscene s)
        {
            s.Sky(WormholeSky);
            // A tunnel of square frames on their corners, each turned against the next, streaming toward the camera; a black square at its end.
            Vector3 axis = new(5f, 1f, 0f);
            const int count = 18;
            const float spacing = 16f, speed = 22f, size = 34f, nearest = 8f;
            var tunnel = new Transform[count];
            for (int i = 0; i < count; i++)
                tunnel[i] = s.Frame(new Vector3(axis.x, axis.y, nearest + i * spacing), size, .9f, i % 2 == 0 ? FrameBright : FrameDark, i * 9f);
            s.Box(new Vector3(5f, 5f, 1f), new Vector3(axis.x, axis.y, 300f), Table("black"), new Vector3(0f, 0f, 45f));
            // The fleet flying away into it: the near ship large in the lower left, the rest ahead and smaller toward the middle.
            Vector3 nearAt = new(-4.6f, -1.7f, 21f);
            var near = s.Look("ship", nearAt, new Vector3(4f, -4f, 0f));
            var alarm = s.Box(Vector3.one * .5f, new Vector3(.8f, 2.9f, -2f), Table("damaged"), default, near.transform);
            Vector3[] ahead =
            {
                new(-1f, 1f, 38f), new(6f, -2f, 50f), new(1.5f, 3f, 62f), new(9f, 2.5f, 78f), new(3f, -1f, 94f), new(7.5f, 4f, 112f), new(4f, 1.5f, 130f),
                new(-2.5f, -1.5f, 70f)
            };
            var fleet = new Transform[ahead.Length];
            for (int i = 0; i < ahead.Length; i++) fleet[i] = s.Look("ship", ahead[i], new Vector3(0f, 5f * Mathf.Sin(i), 0f)).transform;
            // The turbulence: three jagged zigzags of thin white and pale violet bars cutting across the fleet, flickering.
            var random = new System.Random(5);
            var zigzags = new List<(Transform[] bars, Vector3[] points)>();
            for (int z = 0; z < 3; z++)
            {
                var points = new Vector3[8];
                float x0 = R(random, -16f, -4f), y0 = R(random, 8f, 14f), z0 = R(random, 28f, 70f);
                for (int k = 0; k < points.Length; k++)
                    points[k] = new Vector3(x0 + k * 4.5f + R(random, -1f, 1f), y0 - k * 2.8f + (k % 2 == 1 ? 2.4f : -2.4f), z0 + k * 1.5f);
                var bars = new Transform[points.Length - 1];
                for (int k = 0; k < bars.Length; k++) bars[k] = s.Bar(points[k], points[k + 1], .35f, k % 2 == 1 ? Table("white") : Turbulence);
                zigzags.Add((bars, points));
            }

            var shed = new Spout();
            var jagged = new Vector3[8];
            float flickerAt = 0f;
            s.Every(t =>
            {
                for (int i = 0; i < count; i++)
                {
                    tunnel[i].localPosition = new Vector3(axis.x, axis.y, nearest + Mathf.Repeat(i * spacing - t * speed, count * spacing));
                    tunnel[i].localRotation = Quaternion.Euler(0f, 0f, 45f + i * 9f + t * 6f * (i % 2 == 0 ? -1f : 1f));
                }

                // The near ship shakes and banks; a red cube on it blinks.
                near.transform.localPosition = nearAt + Jitter(.05f, .05f, 0f);
                near.transform.localRotation = Quaternion.Euler(4f + J(-.4f, .4f), -4f, 10f * Mathf.Sin(t * 2.3f) + J(-1.2f, 1.2f));
                alarm.enabled = Mathf.Repeat(t * 2.5f, 1f) < .5f;
                // The others drift apart; one is thrown off its line, one is knocked sideways, one sheds small orange cubes.
                float apart = 1f + .06f * t;
                for (int i = 0; i < fleet.Length; i++)
                {
                    Vector3 p = ahead[i];
                    p.x = axis.x + (p.x - axis.x) * apart;
                    p.y = axis.y + (p.y - axis.y) * apart;
                    float yaw = 5f * Mathf.Sin(i), roll = 0f;
                    if (i == 2) { p.x += 2.2f * t; roll = 40f * t; }
                    if (i == 5) { p.x -= 1.4f * t; yaw -= 25f * Smooth(t / 2f); }
                    fleet[i].localPosition = p;
                    fleet[i].localRotation = Quaternion.Euler(0f, yaw, roll);
                }

                for (int n = shed.Take(14f, s.Step); n > 0; n--)
                    s.Emit(s.OnStage(fleet[4], new Vector3(J(-1f, 1f), 1.5f, J(-3f, 3f))), new Vector3(J(-1f, 1f), J(-.5f, 1f), -J(4f, 9f)), Table("fire"), J(.4f, .7f),
                        J(.8f, 1.3f));
                if (t >= flickerAt)
                {
                    flickerAt = t + J(.06f, .16f);
                    foreach (var (bars, points) in zigzags)
                    {
                        bool on = UnityEngine.Random.value < .7f;
                        for (int k = 0; k < points.Length; k++) jagged[k] = points[k] + Jitter(.6f, .6f, .3f);
                        for (int k = 0; k < bars.Length; k++)
                        {
                            bars[k].gameObject.SetActive(on);
                            if (on) Lay(bars[k], jagged[k], jagged[k + 1], .35f);
                        }
                    }
                }

                s.Shake(.06f + .05f * Mathf.Max(0f, Mathf.Sin(t * 3.1f)));
            });
            s.CameraPath(t => new CameraPose(new Vector3(0f, 3.2f, 0f), new Vector3(2.2f, .8f, 60f), 52f));
            s.LightFrom(150f);
            s.Subject(near.transform, new Vector3(0f, 1.5f, 0f));
        }

        // ───── M6: the ship burning, falling toward the planet, alone ─────

        static void StageFall(V2Cutscene s)
        {
            s.Sky(new Color(.04f, .05f, .1f));
            s.Ground(Sand, 9000f);
            var random = new System.Random(6);
            var rocks = new List<Matrix4x4>();
            for (int i = 0; i < 260; i++)
            {
                float w = R(random, 3f, 10f), h = R(random, 1.5f, 5f);
                rocks.Add(Matrix4x4.TRS(new Vector3(R(random, -260f, 260f), h * .5f, R(random, 60f, 900f)), Quaternion.Euler(0f, R(random, 0f, 90f), 0f),
                    new Vector3(w, h, w * R(random, .6f, 1.4f))));
            }

            s.Batch(Prim.Box, rocks, Rock, "Rocks");
            // High above the desert; the ship falls along the diagonal of the picture, from upper left to lower right and away, and the
            // camera tilts down after it (the horizon starts a quarter of the way down and leaves at the top).
            Vector3 eye = new(0f, 120f, 0f);
            Vector3 f = Downward(16f), up = Vector3.Cross(f, Vector3.right);
            Vector3 from = eye + f * 34f - Vector3.right * 10f + up * 4.2f, to = eye + f * 44f + Vector3.right * 8f - up * 9.5f;
            float seconds = s.Seconds;

            Vector3 FallAt(float t)
            {
                float k = Mathf.Clamp01(t / seconds);
                return Vector3.LerpUnclamped(from, to, k * (.55f + .45f * k));
            }

            // The ship as it is in the game, burning: its engine dark, one wing gone (the table's wings are one piece: a half stays), a red
            // cube blinking on the hull.
            var ship = s.Look("ship", from);
            ship.ShowParts("roof", false);
            s.Solid(Prim.Wedge, new Vector3(2.6f, .5f, 3f), new Vector3(-3.1f, 1.6f, -1f), Table("roof"), default, ship.transform);
            ship.SetColour("control", BrokenGrey);
            var alarm = s.Box(Vector3.one * .5f, new Vector3(.6f, 2.85f, 1f), Table("damaged"), default, ship.transform);
            var fire = new Spout();
            var smoke = new Spout();
            var bits = new Spout();
            s.Every(t =>
            {
                Vector3 p = FallAt(t), ahead = FallAt(t + .05f) - p;
                if (ahead.sqrMagnitude < 1e-8f) ahead = to - from;
                ship.transform.localPosition = p + Jitter(.06f, .06f, .06f);
                ship.transform.localRotation = Quaternion.LookRotation(ahead.normalized, (eye - p).normalized) * Quaternion.Euler(J(-1.5f, 1.5f), J(-1.5f, 1.5f), -38f);
                alarm.enabled = Mathf.Repeat(t * 3f, 1f) < .5f;
                Vector3 tail = s.OnStage(ship.transform, new Vector3(0f, 1.5f, -7.6f));
                for (int n = fire.Take(45f, s.Step); n > 0; n--)
                    s.Emit(tail + Jitter(.6f, .6f, .6f), Jitter(1.2f, 1.2f, 1.2f), Table("fire"), J(.7f, 1.2f), J(.6f, 1f), 0f, DullOrange);
                for (int n = smoke.Take(28f, s.Step); n > 0; n--)
                    s.Emit(tail + Jitter(.9f, .9f, .9f), Jitter(.6f, .6f, .6f) + Vector3.up * .5f, Smoke, J(1f, 1.6f), J(1.6f, 2.4f));
                for (int n = bits.Take(5f, s.Step); n > 0; n--)
                    s.Emit(s.OnStage(ship.transform, new Vector3(J(-1.8f, 1.8f), 2.2f, J(-4f, 4f))), Jitter(4f, 4f, 4f), Table("ship"), J(.25f, .45f), J(1f, 1.6f), 3f);
            });
            s.CameraPath(t => new CameraPose(eye, eye + Downward(12f + 12f * Smooth(t / seconds)) * 30f, 45f));
            s.Subject(ship.transform, new Vector3(0f, 1.5f, 0f));
        }

        /// <summary>Straight ahead (+z), pitched down by degrees.</summary>
        static Vector3 Downward(float degrees) => new(0f, -Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));

        // ───── M7: Felix alone by the wreck at dusk ─────

        static void StageAlone(V2Cutscene s)
        {
            s.Sky(DuskSky);
            s.Ground(SandDusk);
            // The wreck at the end of the long groove it ploughed, coming from the far right.
            Vector3 wreckAt = new(6.5f, 0f, 13f);
            const float wreckYaw = -112f;
            var wreck = Wreck(s, wreckAt, wreckYaw);
            Furrow(s, wreckAt, Quaternion.Euler(0f, wreckYaw + 180f, 0f) * Vector3.forward, 4f, 70f, 3.4f);
            // A low sun on the horizon ahead of him: one pale-yellow square.
            s.Box(new Vector3(26f, 26f, 1f), new Vector3(-70f, 10f, 560f), Table("lamp"));
            // His footprints from the wreck to where he stands; they fade one by one from the wreck.
            Vector3 felixAt = new(-2.6f, 0f, 2.6f);
            Vector3 first = wreckAt + new Vector3(-2.5f, 0f, -3f), last = felixAt + new Vector3(0f, 0f, 1f);
            Vector3 along = (last - first).normalized, side = Vector3.Cross(Vector3.up, along);
            float stepYaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
            const int prints = 12;
            var footprints = new Transform[prints];
            var printSize = new Vector3(.35f, .03f, .5f);
            for (int i = 0; i < prints; i++)
                footprints[i] = s.Box(printSize, Vector3.Lerp(first, last, i / (prints - 1f)) + side * (i % 2 == 0 ? -.25f : .25f) + Vector3.up * .015f, Footprint,
                    new Vector3(0f, stepYaw, 0f)).transform;
            var felix = s.Felix(felixAt, -42f);
            var smoke = new Spout();
            s.Every(t =>
            {
                // He turns slowly from left to right, searching, past the sun.
                float yaw = -42f + 22f * Smooth(t / 2.2f) + 28f * Smooth(Ramp(t, 3f, 2f));
                felix.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                for (int i = 0; i < prints; i++)
                {
                    float left = 1f - Ramp(t, .5f + i * .38f, .35f);
                    footprints[i].localScale = new Vector3(printSize.x * left, printSize.y, printSize.z * left);
                }

                for (int n = smoke.Take(5f, s.Step); n > 0; n--)
                    s.Emit(s.OnStage(wreck, new Vector3(J(-1f, 1f), 2.4f, J(-3f, 1f))), new Vector3(J(-.2f, .2f), J(1f, 1.6f), J(-.2f, .2f)), Smoke, J(.4f, .8f), J(2f, 3f));
            });
            s.CameraMove(new Vector3(-1.4f, 3.2f, -6f), new Vector3(1.6f, 1f, 14f), new Vector3(-2.4f, 3.8f, -9f), new Vector3(1.6f, 1f, 14f), 40f);
            s.Subject(felix.transform, new Vector3(0f, 1f, 0f));
        }

        // ───── M8: the ship builds the base by itself with its arms ─────

        static void StageDeploy(V2Cutscene s)
        {
            s.Sky(DuskSky);
            s.Ground(SandDusk);
            Vector3 baseAt = new(-5.5f, 0f, 1.5f), wreckAt = new(8f, 0f, -2.5f);
            Wreck(s, wreckAt, -105f);
            Furrow(s, wreckAt, new Vector3(1f, 0f, .25f).normalized, 5f, 90f, 3.6f);
            // The base half built: its body and its door stand; its roof slab, a lamp and a small grey box are still in the arms.
            var body = s.Look("base.body", baseAt, new Vector3(0f, 180f, 0f));
            body.ShowParts("roof", false);
            var roof = s.Box(new Vector3(5f, .6f, 5f), baseAt + new Vector3(0f, 7.7f, 0f), Table("roof")).transform;
            Vector3 lampFrom = baseAt + new Vector3(9.5f, 6f, -8.5f), lampTo = baseAt + new Vector3(6.1f, 0f, -6.1f);
            var lamp = s.Look("lamp", lampFrom, default, 1.6f).transform;
            var box = s.Box(Vector3.one * 1.2f, baseAt, Table("grey")).transform;
            Vector3 boxPivot = baseAt + new Vector3(6.8f, 0f, 4f);
            Vector3 top = wreckAt + new Vector3(0f, 2.4f, 0f);
            var arms = new[]
            {
                Arm(s, top + new Vector3(-1f, 0f, -2f), 8f), Arm(s, top + new Vector3(-.5f, 0f, -3.5f), 6.5f), Arm(s, top + new Vector3(-.6f, 0f, 1.5f), 6.5f)
            };
            bool roofSet = false, lampSet = false, boxSet = false;
            var weld = new Spout();
            s.Every(t =>
            {
                float k1 = Smooth((t - .6f) / 2.8f), k2 = Smooth((t - 1.6f) / 3f), k3 = Smooth((t - 2.6f) / 3f);
                roof.localPosition = baseAt + new Vector3(0f, 4.3f + (1f - k1) * 3.4f, 0f);
                lamp.localPosition = Vector3.Lerp(lampFrom, lampTo, k2) + Vector3.up * (Mathf.Sin(k2 * Mathf.PI) * 1.2f);
                float swing = (-70f + 70f * k3) * Mathf.Deg2Rad;
                box.localPosition = boxPivot + new Vector3(Mathf.Sin(swing) * 3.5f, .6f + (1f - k3) * 3.5f, Mathf.Cos(swing) * 3.5f - 3.5f);
                Reach(arms[0], roof.localPosition + Vector3.up * .3f);
                Reach(arms[1], lamp.localPosition + Vector3.up * 2.6f);
                Reach(arms[2], box.localPosition + Vector3.up * .6f);
                // Small orange sparks where the hands hold their parts, and a burst where each part comes to rest.
                for (int n = weld.Take(6f, s.Step); n > 0; n--) Sparks(s, arms[UnityEngine.Random.Range(0, arms.Length)].hand.localPosition, 2);
                if (!roofSet && k1 >= 1f)
                {
                    roofSet = true;
                    Sparks(s, roof.localPosition + new Vector3(2.5f, -.3f, -2.5f), 10);
                    Sparks(s, roof.localPosition + new Vector3(-2.5f, -.3f, -2.5f), 10);
                }

                if (!lampSet && k2 >= 1f) { lampSet = true; Sparks(s, lamp.localPosition, 10); }
                if (!boxSet && k3 >= 1f) { boxSet = true; Sparks(s, box.localPosition, 10); }
            });
            // A slow orbit to the right, high, as the map is seen in play.
            Vector3 target = new(1f, 2f, -.5f);
            float seconds = s.Seconds;
            s.CameraPath(t =>
            {
                float yaw = (20f + 20f * Smooth(t / seconds)) * Mathf.Deg2Rad, pitch = 40f * Mathf.Deg2Rad;
                var look = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), -Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
                return new CameraPose(target - look * 44f, target, 34f);
            });
            s.LightFrom(105f);
            s.Subject(body.transform, new Vector3(0f, 2f, 0f));
        }

        /// <summary>A robot arm of the ship: two thin dark-grey bars with a block at the shoulder, the elbow and the hand.</summary>
        sealed class RobotArm
        {
            public Vector3 shoulderAt;
            public float bone;
            public Transform upper, lower, shoulder, elbow, hand;
        }

        static RobotArm Arm(V2Cutscene s, Vector3 shoulderAt, float bone)
        {
            var arm = new RobotArm { shoulderAt = shoulderAt, bone = bone };
            arm.upper = s.Bar(shoulderAt, shoulderAt + Vector3.up, .45f, BrokenGrey);
            arm.lower = s.Bar(shoulderAt, shoulderAt + Vector3.up, .38f, BrokenGrey);
            arm.shoulder = s.Box(Vector3.one * .7f, shoulderAt, BrokenGrey).transform;
            arm.elbow = s.Box(Vector3.one * .55f, shoulderAt, BrokenGrey).transform;
            arm.hand = s.Box(new Vector3(.5f, .3f, .5f), shoulderAt, BrokenGrey).transform;
            return arm;
        }

        /// <summary>The arm's hand to a point (as near as its two bones reach), the elbow bent upward in the upright plane through the
        /// shoulder and the hand.</summary>
        static void Reach(RobotArm arm, Vector3 hand)
        {
            Vector3 d = hand - arm.shoulderAt;
            float reach = Mathf.Min(d.magnitude, arm.bone * 2f - 1e-3f);
            Vector3 along = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.down;
            float cos = Mathf.Clamp(reach * .5f / arm.bone, -1f, 1f);
            Vector3 bend = Vector3.up - along * Vector3.Dot(Vector3.up, along);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.forward;
            Vector3 elbow = arm.shoulderAt + along * (arm.bone * cos) + bend.normalized * (arm.bone * Mathf.Sqrt(1f - cos * cos));
            Vector3 end = arm.shoulderAt + along * reach;
            Lay(arm.upper, arm.shoulderAt, elbow, .45f);
            Lay(arm.lower, elbow, end, .38f);
            arm.elbow.localPosition = elbow;
            arm.hand.localPosition = end;
        }

        static void Sparks(V2Cutscene s, Vector3 at, int count)
        {
            for (int i = 0; i < count; i++)
                s.Emit(at + Jitter(.3f, .2f, .3f), new Vector3(J(-3f, 3f), J(1.5f, 4.5f), J(-3f, 3f)), Table("fire"), J(.15f, .3f), J(.4f, .8f), 9f);
        }

        // ───── M9: night; the base's lamps come on beside the wreck ─────

        static void StageNightBase(V2Cutscene s)
        {
            s.Sky(NightSky);
            s.Ground(NightGround);
            var looks = new List<WhiteboxLook> { s.Look("base", Vector3.zero, new Vector3(0f, 180f, 0f)) };
            looks.AddRange(Wreck(s, new Vector3(13f, 0f, -3f), -105f).GetComponentsInChildren<WhiteboxLook>());
            // What each part shows in the light, to darken toward the night and give back as the lamps come on.
            var lit = new List<Color[]>();
            foreach (var look in looks)
            {
                var colours = new Color[look.PartCount];
                for (int p = 0; p < colours.Length; p++) colours[p] = look.ShownColour(p);
                lit.Add(colours);
            }

            Vector3[] corners = { new(-6.1f, 0f, -6.1f), new(6.1f, 0f, -6.1f), new(6.1f, 0f, 6.1f), new(-6.1f, 0f, 6.1f) };
            var lamps = new WhiteboxLook[corners.Length];
            var patches = new Transform[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                lamps[i] = s.Look("lamp", corners[i], default, 1.6f);
                // A patch of the true sand under each lamp, each a little higher than the last so the overlaps never flicker.
                patches[i] = s.Solid(Prim.Disc, new Vector3(1f, .04f, 1f), corners[i] + Vector3.up * (.02f + i * .004f), Sand).transform;
            }

            var random = new System.Random(9);
            var stars = new Transform[60];
            for (int i = 0; i < stars.Length; i++)
            {
                float a = 28f * Mathf.Deg2Rad + R(random, -.9f, .9f), e = R(random, .02f, .16f);
                stars[i] = s.Box(Vector3.one * 3f, new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)) * 900f, Color.white).transform;
            }

            float shownNight = -1f;
            s.Every(t =>
            {
                float on = 0f;
                for (int i = 0; i < lamps.Length; i++)
                {
                    float k = Ramp(t, .8f + i * .9f, .25f);
                    on += k;
                    bool bright = k >= 1f || k > 0f && Mathf.Repeat(t * 14f, 1f) < .5f; // a flicker as it comes on
                    lamps[i].SetColour("lamp", bright ? Table("lamp") : LampDim);
                    float radius = 6f * EaseOut(k);
                    patches[i].gameObject.SetActive(radius > .05f);
                    patches[i].localScale = new Vector3(radius * 2f, .04f, radius * 2f);
                }

                float night = .75f * (1f - on / lamps.Length);
                if (!Mathf.Approximately(night, shownNight))
                {
                    shownNight = night;
                    for (int l = 0; l < looks.Count; l++)
                        for (int p = 0; p < lit[l].Length; p++) looks[l].SetColour(p, Color.Lerp(lit[l][p], NightTint, night));
                }

                for (int i = 0; i < stars.Length; i++) stars[i].localScale = Vector3.one * (3f * (.75f + .25f * Mathf.Sin(t * 3f + i * 1.7f)));
            });
            // Far and high; the camera pulls back and rises a little. The base stands a little below the middle, a quarter of the width.
            Vector3 centre = new(4f, 0f, -1f), flat = Quaternion.Euler(0f, 28f, 0f) * Vector3.forward;
            float seconds = s.Seconds;
            s.CameraPath(t =>
            {
                float k = Smooth(t / seconds), pitch = 12.5f * Mathf.Deg2Rad;
                Vector3 position = centre - flat * (75f + 22f * k) + Vector3.up * (18f + 7f * k);
                return new CameraPose(position, position + (flat * Mathf.Cos(pitch) + Vector3.down * Mathf.Sin(pitch)) * 50f, 40f);
            });
            s.LightFrom(110f);
            s.Subject(looks[0].transform, new Vector3(0f, 2f, 0f));
        }

        // ───── Shared pieces ─────

        /// <summary>The wreck as the whitebox shows it on the map: the table's broken hull and loose nose, with the ship's engine (dark,
        /// broken) and its fuel tank (dull, empty) in their places at the tail.</summary>
        static Transform Wreck(V2Cutscene s, Vector3 at, float yaw)
        {
            var wreck = s.Node("Wreck", at, new Vector3(0f, yaw, 0f));
            s.Look("wreck", Vector3.zero, default, 1f, wreck);
            s.Look("ship.engine", Vector3.zero, default, 1f, wreck).SetColour("control", BrokenGrey);
            s.Look("ship.fuel", Vector3.zero, default, 1f, wreck).SetColour("core", EmptyTank);
            return wreck;
        }

        /// <summary>The long shallow groove a crash ploughs: a strip of darker sand from near to far metres along a direction.</summary>
        static void Furrow(V2Cutscene s, Vector3 from, Vector3 direction, float near, float far, float width)
        {
            Vector3 a = from + direction * near, b = from + direction * far;
            s.Box(new Vector3(width, .04f, (b - a).magnitude), (a + b) * .5f + Vector3.up * .02f, Groove,
                new Vector3(0f, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0f));
        }
    }
}
