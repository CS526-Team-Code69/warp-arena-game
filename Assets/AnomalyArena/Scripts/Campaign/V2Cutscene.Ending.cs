using System.Collections.Generic;
using UnityEngine;
using Prim = AnomalyArena.Whitebox.Prim;

namespace AnomalyArena
{
    /// <summary>
    /// The ending (V2 doc section 2), played once the dark matter core is taken: Felix seats the core in the engine's empty ring and the
    /// base system starts the ignition again, the ship lifts off over the base while Felix promises to send someone back for his unit, in
    /// orbit the jump to Earth is locked and the survivor's record is closed, and the ship flies into the jump gate home, the picture
    /// turning white. The run is won when the white is full.
    /// </summary>
    public sealed partial class V2Cutscene
    {
        /// <summary>The ending's four shots, E11 to E14, 30.5 seconds as written.</summary>
        public static readonly CutsceneShot[] Ending =
        {
            new("E11", 8.5f, CutsceneTime.Present, CutsceneCut.Black, Move(Crop(.5f, .5f, 1f), Crop(.55f, .46f, .7f)), StageCoreSeat,
                CutsceneBeat.Sound(CampaignSfx.CoreInsert, .6f), CutsceneBeat.Say(V2Campaign.Line.E04, 1.2f), CutsceneBeat.Say(V2Campaign.Line.E05, 3f),
                CutsceneBeat.Sound(CampaignSfx.EngineSpinUp, 7.8f)),
            new("E12", 7.5f, CutsceneTime.Present, CutsceneCut.Hard, Move(Crop(.5f, .62f, .85f), Crop(.5f, .38f, .85f)), StageLiftOff,
                CutsceneBeat.Sound(CampaignSfx.IgnitionSuccess, 0f), CutsceneBeat.Say(V2Campaign.Line.E06, 1.4f)),
            new("E13", 10f, CutsceneTime.Present, CutsceneCut.Dissolve, Move(Crop(.66f, .55f, .8f), Crop(.4f, .45f, .8f)), StageOrbit,
                CutsceneBeat.Sound(CampaignSfx.CoordinatesLocked, .3f), CutsceneBeat.Say(V2Campaign.Line.E07, .6f), CutsceneBeat.Say(V2Campaign.Line.E08, 6.4f),
                CutsceneBeat.Sound(CampaignSfx.RecordChime, 9f)),
            new("E14", 4.5f, CutsceneTime.Present, CutsceneCut.Hard, Hold(Crop(.5f, .5f, .95f)), StageWarp,
                CutsceneBeat.Sound(CampaignSfx.WarpJump, 1f), CutsceneBeat.WhiteOut(1f, 3.5f)),
        };

        static readonly Color GateHot = new(.86f, .66f, 1f), EarthBlue = new(.62f, .82f, 1f);

        // ───── E11: Felix seats the core in the engine's empty ring ─────

        static void StageCoreSeat(V2Cutscene s)
        {
            s.Sky(DawnSky);
            s.Ground(Sand);
            BaseWithLamps(s, new Vector3(16f, 0f, 14f));
            // The ship at its pad, its full tank lit and its engine still dark; on the left side of the engine the empty ring that the
            // cockpit's diagram showed (L13).
            var ship = s.Look("ship", Vector3.zero);
            ship.SetColour("control", BrokenGrey);
            ship.SetColour("core", FullTank);
            Vector3 ringAt = new(-.74f, 1.5f, -6.2f);
            var ring = Outline(s, ringAt, .8f, .8f, .08f, RingDim);
            ring.localRotation = Quaternion.Euler(0f, 90f, 0f); // its face toward Felix, along the engine's side
            var bars = ring.GetComponentsInChildren<MeshRenderer>();
            // Felix before the ring, the core at his chest: it slides into the ring in the first 0.6 seconds (the CoreInsert sound lands
            // with it).
            s.Felix(new Vector3(-2.1f, 0f, -5.95f), 90f);
            Vector3 chest = new(-1.45f, 1.45f, -5.95f);
            var core = s.Solid(Prim.Octa, new Vector3(.5f, .66f, .5f), chest, Table("core")).transform;
            var motes = new Spout();
            var runners = new Spout();
            var jet = new Spout();
            bool seated = false;
            s.Every(t =>
            {
                float k = Smooth(t / .6f);
                core.localPosition = Vector3.Lerp(chest, ringAt, k);
                core.localRotation = Quaternion.Euler(0f, 70f * t, 0f);
                for (int n = motes.Take(10f, s.Step); n > 0; n--)
                    s.Emit(core.localPosition + Jitter(.3f, .35f, .3f), new Vector3(J(-.15f, .15f), J(.5f, 1f), J(-.15f, .15f)), Violet, J(.08f, .14f), J(1f, 1.5f));
                if (k < 1f) return;
                // Seated: the ring turns bright violet, the engine comes back to cyan, and violet cubes run along the hull to the nose.
                if (!seated)
                {
                    seated = true;
                    foreach (var bar in bars) Paint(bar, Violet);
                    ship.SetColour("control", Table("control"));
                }

                for (int n = runners.Take(14f, s.Step); n > 0; n--)
                    s.Emit(new Vector3(J(-1.9f, -1.85f), J(1f, 2.2f), J(-5f, -4.4f)), new Vector3(0f, 0f, J(7f, 9f)), Violet, J(.12f, .2f), J(1.2f, 1.4f));
                // The engine warms up at the end of the shot (EngineSpinUp, 7.8 s), its first cyan cubes out of the nozzle.
                if (t >= 7.8f)
                    for (int n = jet.Take(30f, s.Step); n > 0; n--)
                        s.Emit(new Vector3(0f, 1.5f, -7.6f) + Jitter(.3f, .3f, .1f), new Vector3(J(-.4f, .4f), J(-.3f, .3f), -J(3f, 6f)), ExhaustCyan, J(.25f, .4f), J(.4f, .7f));
            });
            // From behind his right shoulder, slowly in to the ring.
            s.CameraMove(new Vector3(-4.3f, 2.5f, -8.4f), ringAt, new Vector3(-3f, 2.15f, -7.6f), ringAt, 42f, 38f);
            s.LightFrom(140f);
            s.Subject(ring, Vector3.zero);
        }

        // ───── E12: the ship lifts off over the base ─────

        static void StageLiftOff(V2Cutscene s)
        {
            s.Sky(DawnSky);
            s.Ground(Sand);
            BaseWithLamps(s, Vector3.zero);
            // Nose up, already several ship-lengths over the base, climbing steadily on a column of cyan and white cubes.
            var ship = s.Look("ship", new Vector3(0f, 26f, 0f), new Vector3(-90f, 0f, 0f));
            ship.SetColour("core", FullTank);
            float seconds = s.Seconds;

            float Height(float t) => 26f + 8f * t + 1.6f * t * t;

            var column = new Spout();
            var sand = new Spout();
            s.Every(t =>
            {
                ship.transform.localPosition = new Vector3(0f, Height(t), 0f);
                Vector3 nozzle = s.OnStage(ship.transform, new Vector3(0f, 1.5f, -7.6f));
                for (int n = column.Take(80f, s.Step); n > 0; n--)
                    s.Emit(nozzle + Jitter(.4f, .1f, .4f), new Vector3(J(-.5f, .5f), -J(14f, 18f), J(-.5f, .5f)), UnityEngine.Random.value < .6f ? ExhaustCyan : Color.white,
                        J(.4f, .6f), J(1.1f, 1.5f));
                // The ring of blown sand round the base spreads outward.
                float ring = 7f + 10f * t / seconds;
                for (int n = sand.Take(45f, s.Step); n > 0; n--)
                {
                    float a = J(0f, Mathf.PI * 2f);
                    var direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    s.Emit(direction * ring + Vector3.up * .3f, direction * J(4f, 6f) + Vector3.up * J(.5f, 1.5f), Sand, J(.3f, .5f), J(.9f, 1.3f), 4f);
                }
            });
            // From the ground at a low angle, tilting up after the ship into the sky.
            Vector3 eye = new(-8f, 2.2f, -38f);
            s.CameraPath(t => new CameraPose(eye, new Vector3(0f, Mathf.Lerp(12f, Height(seconds) - 18f, Smooth(t / seconds)), 0f), 55f));
            s.LightFrom(140f);
            s.Subject(ship.transform, new Vector3(0f, 1.5f, 0f));
        }

        // ───── E13: in orbit, the route home on the console ─────

        static void StageOrbit(V2Cutscene s)
        {
            s.Sky(SpaceSky);
            CockpitRoom(s);
            // Space through the window: white square stars above, and below them the planet, part of one huge sand-brown disc, sinking
            // slowly as the ship climbs.
            var random = new System.Random(13);
            var stars = new List<Matrix4x4>();
            for (int i = 0; i < 80; i++)
                stars.Add(Matrix4x4.TRS(new Vector3(R(random, -80f, 70f), R(random, 6f, 45f), R(random, 140f, 170f)), Quaternion.identity, Vector3.one * .5f));
            s.Batch(Prim.Box, stars, Color.white, "Stars");
            Vector3 planetAt = new(-20f, -155f, 260f);
            var planet = s.Solid(Prim.Disc, new Vector3(320f, 2f, 320f), planetAt, Sand, new Vector3(-75f, 0f, 0f)).transform;
            // Over the console, the route home: a line of small cyan cubes in a low arc from a small sand-coloured disc (this planet) to a
            // pale-blue cube (Earth). The cubes light one after another from the planet's end; then the Earth pulses.
            const float front = 2.85f, planetEnd = -1f, earthEnd = 1.6f;
            s.Solid(Prim.Disc, new Vector3(.3f, .04f, .3f), new Vector3(planetEnd - .3f, 2.55f, front), Sand, new Vector3(90f, 0f, 0f));
            var earth = s.Box(Vector3.one * .18f, new Vector3(earthEnd + .3f, 2.55f, front), EarthBlue).transform;
            const int steps = 12;
            var route = new MeshRenderer[steps];
            for (int i = 0; i < steps; i++)
            {
                float u = i / (steps - 1f);
                route[i] = s.Box(Vector3.one * .08f, new Vector3(Mathf.Lerp(planetEnd, earthEnd, u), 2.55f + .3f * Mathf.Sin(Mathf.PI * u), front), StripDim);
            }

            // Felix in the pilot's seat on the left, his box sunk to a sitting height before a seat back.
            s.Box(new Vector3(1.1f, 1.2f, .25f), new Vector3(-1.3f, 1.6f, .25f), ConsoleGrey);
            var felix = s.Felix(new Vector3(-1.3f, .55f, .9f), 15f);
            s.Every(t =>
            {
                planet.localPosition = planetAt + Vector3.down * (.6f * t);
                for (int i = 0; i < steps; i++) Paint(route[i], t >= .4f + i * .45f ? Table("door") : StripDim);
                earth.localScale = Vector3.one * (.18f * (t >= .4f + steps * .45f ? 1f + .25f * Mathf.Sin(t * 5f) : 1f));
            });
            // Over his shoulder, slowly in toward the window and the route.
            s.CameraMove(new Vector3(.6f, 3.1f, -3.4f), new Vector3(-.2f, 2.7f, 4f), new Vector3(.3f, 2.85f, -2.2f), new Vector3(.1f, 2.75f, 4f), 48f, 44f);
            s.LightFrom(135f);
            s.Subject(felix.transform, new Vector3(0f, 1.5f, 0f));
        }

        // ───── E14: into the jump gate, home ─────

        static void StageWarp(V2Cutscene s)
        {
            s.Sky(SpaceSky);
            Vector3 eye = new(0f, 3.5f, -12f), gate = new(13f, 3.5f, 70f), start = new(-3f, -.5f, 6f);
            // The planet: part of one huge flat sand-brown disc filling the lower left.
            s.Solid(Prim.Disc, new Vector3(300f, 2f, 300f), new Vector3(-120f, -110f, 240f), Sand, new Vector3(-80f, 0f, 0f));
            // Small white square stars, which stretch into lines away from the gate as the ship goes in.
            var random = new System.Random(8);
            Vector3 toGate = (gate - eye).normalized;
            var stars = new Transform[70];
            for (int i = 0; i < stars.Length; i++)
            {
                var p = new Vector3(R(random, -160f, 200f), R(random, -60f, 120f), R(random, 280f, 340f));
                Vector3 outward = p - eye - toGate * Vector3.Dot(p - eye, toGate);
                stars[i] = s.Box(Vector3.one * .9f, p, Color.white, Quaternion.LookRotation(outward.normalized).eulerAngles).transform;
            }

            // The gate: nested square frames on their corners, turning against each other, growing and brightening round a white square.
            const int rings = 6;
            var frames = new Transform[rings];
            var bars = new MeshRenderer[rings][];
            for (int i = 0; i < rings; i++)
            {
                frames[i] = s.Frame(gate + new Vector3(0f, 0f, i * .5f), 5f + i * 3.2f, .5f + .08f * i, i % 2 == 0 ? FrameBright : FrameDark, i * 11f);
                bars[i] = frames[i].GetComponentsInChildren<MeshRenderer>();
            }

            s.Box(new Vector3(2.6f, 2.6f, .4f), gate + new Vector3(0f, 0f, 3f), Color.white, new Vector3(0f, 0f, 45f));
            var ship = s.Look("ship", start);
            ship.SetColour("core", FullTank);
            var exhaust = new Spout();
            float seconds = s.Seconds;
            s.Every(t =>
            {
                float k = Mathf.Clamp01(t / seconds);
                for (int i = 0; i < rings; i++)
                {
                    frames[i].localRotation = Quaternion.Euler(0f, 0f, 45f + i * 11f + t * 12f * (i % 2 == 0 ? -1f : 1f));
                    frames[i].localScale = Vector3.one * (1f + .5f * k);
                    var colour = Color.Lerp(i % 2 == 0 ? FrameBright : FrameDark, GateHot, .8f * k);
                    foreach (var bar in bars[i]) Paint(bar, colour);
                }

                // The ship speeds up into the gate and shrinks to a point; it is gone once there.
                float travel = Mathf.Min(1f, k * k * k * 1.05f);
                Vector3 p = Vector3.Lerp(start, gate, travel), heading = (gate - start).normalized;
                ship.transform.localPosition = p;
                ship.transform.localRotation = Quaternion.LookRotation(heading);
                ship.Visible = travel < 1f;
                if (travel < 1f)
                    for (int n = exhaust.Take(50f, s.Step); n > 0; n--)
                        s.Emit(s.OnStage(ship.transform, new Vector3(0f, 1.5f, -7.6f)) + Jitter(.3f, .3f, .3f), -heading * J(6f, 10f) + Jitter(.5f, .5f, .5f),
                            UnityEngine.Random.value < .6f ? ExhaustCyan : Color.white, J(.3f, .45f), J(.4f, .6f));
                float stretch = 1f + 18f * Mathf.Pow(Ramp(t, 2f, 2.5f), 2f);
                foreach (var star in stars) star.localScale = new Vector3(.9f, .9f, .9f * stretch);
            });
            s.CameraPath(t => new CameraPose(eye, new Vector3(3f, 1.5f, 40f), 50f));
            s.LightFrom(140f);
            s.Subject(frames[0], Vector3.zero);
        }
    }
}
