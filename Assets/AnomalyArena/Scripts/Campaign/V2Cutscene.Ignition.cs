using System.Collections.Generic;
using UnityEngine;
using Prim = AnomalyArena.Whitebox.Prim;

namespace AnomalyArena
{
    /// <summary>
    /// The failed ignition (V2 doc section 2), played when the full tank is lit: the engine fires and sputters out, Felix finds in the
    /// cockpit that the ignition needs an enormous source of energy, the wave of dark-matter energy spreads over the desert and the Tyrant
    /// senses it, and the Tyrant rises outside the base. The base system's lines S10, F01, F02 and Felix's F03 keep their voices.
    /// </summary>
    public sealed partial class V2Cutscene
    {
        /// <summary>The failed ignition's five shots, L1 to L5; the Tyrant fight starts after the last.</summary>
        public static readonly CutsceneShot[] Ignition =
        {
            new("L1", 5f, StageIgnition, CutsceneBeat.Say(V2Campaign.Line.S10, .4f)),
            new("L2", 5.5f, StageSputter, CutsceneBeat.Sound(CampaignSfx.IgnitionFail, 0f), CutsceneBeat.Say(V2Campaign.Line.F01, .5f)),
            new("L3", 6f, StageCockpit, CutsceneBeat.Say(V2Campaign.Line.F02, .3f), CutsceneBeat.Say(V2Campaign.Line.F05, 2.4f)),
            new("L4", 5.5f, StageSurge, CutsceneBeat.Say(V2Campaign.Line.F04, .8f)),
            new("L5", 5f, StageArrival, CutsceneBeat.Sound(CampaignSfx.TyrantArrive, 0f), CutsceneBeat.Say(V2Campaign.Line.F03, 2.4f)),
        };

        static readonly Color ConsoleGrey = new(.3f, .3f, .33f), ScreenBack = new(.07f, .09f, .13f), RingRed = new(1f, .35f, .2f), RingDim = new(.45f, .18f, .14f);

        // ───── L1 and L2: the ship beside the base at dawn, seen low from behind on its right ─────

        /// <summary>The ship whole and repaired on the sand beside the base: its engine in the left half of the picture, the base with its
        /// door in the right half. L1 and L2 are the same place moments apart.</summary>
        static WhiteboxLook LaunchPad(V2Cutscene s)
        {
            s.Sky(DawnSky);
            s.Ground(Sand);
            BaseWithLamps(s, new Vector3(12f, 0f, 18f));
            s.LightFrom(145f);
            return s.Look("ship", Vector3.zero);
        }

        /// <summary>The base as the whitebox shows it, its door toward -z, with a lamp at each corner (the base's floodlights).</summary>
        static WhiteboxLook BaseWithLamps(V2Cutscene s, Vector3 at)
        {
            var look = s.Look("base", at, new Vector3(0f, 180f, 0f));
            for (int i = 0; i < 4; i++) s.Look("lamp", at + new Vector3(i % 2 == 0 ? -6.1f : 6.1f, 0f, i < 2 ? -6.1f : 6.1f), default, 1.6f);
            return look;
        }

        static void StageIgnition(V2Cutscene s)
        {
            var ship = LaunchPad(s);
            var jet = new Spout();
            float nextWave = .3f, seconds = s.Seconds;
            s.Every(t =>
            {
                // The jet grows from a few cubes to a long dense stream toward the camera; the tank brightens; the ship trembles.
                float k = Smooth(t / seconds);
                ship.SetColour("core", Color.Lerp(Table("core"), FullTank, k));
                ship.transform.localPosition = Jitter(.015f + .04f * k, .015f + .03f * k, 0f);
                Vector3 nozzle = s.OnStage(ship.transform, new Vector3(0f, 1.5f, -7.6f)), back = s.OnStageDirection(ship.transform, Vector3.back);
                for (int n = jet.Take(10f + 150f * k, s.Step); n > 0; n--)
                    s.Emit(nozzle + Jitter(.4f, .4f, .1f), back * (J(7f, 12f) * (.5f + .5f * k)) + Jitter(.6f, .4f, .6f), UnityEngine.Random.value < .6f ? ExhaustCyan : Color.white,
                        J(.3f, .5f), J(.6f, 1.1f));
                if (t >= nextWave)
                {
                    nextWave = t + .6f;
                    SandWave(s, nozzle, 14 + (int)(20f * k), 4f + 4f * k);
                }

                s.Shake(.02f + .07f * k);
            });
            s.CameraMove(new Vector3(5.2f, 1.9f, -21.5f), new Vector3(3f, 2.4f, 0f), new Vector3(5f, 1.9f, -20.5f), new Vector3(3f, 2.4f, 0f), 40f);
            s.Subject(ship.transform, new Vector3(0f, 1.5f, -6.2f));
        }

        /// <summary>A wave of sand-brown cubes blown outward along the ground (at height ground) round a point.</summary>
        static void SandWave(V2Cutscene s, Vector3 around, int count, float speed, float ground = 0f)
        {
            var foot = new Vector3(around.x, ground + .2f, around.z);
            for (int i = 0; i < count; i++)
            {
                float a = J(0f, Mathf.PI * 2f);
                var direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                s.Emit(foot + direction * J(1.5f, 3f), direction * J(speed * .6f, speed) + Vector3.up * J(1f, 2.5f), Sand, J(.2f, .4f), J(.8f, 1.3f), 6f);
            }
        }

        static void StageSputter(V2Cutscene s)
        {
            var ship = LaunchPad(s);
            // The jet stutters: weak bursts, each shorter than the last, cyan turning dull orange; then only embers and smoke, the engine
            // dark for good. The blown sand settles meanwhile.
            (float start, float length, float power)[] bursts = { (.3f, .9f, 1f), (1.6f, .6f, .6f), (2.6f, .35f, .35f) };
            const float dead = 3.2f;
            var jet = new Spout();
            var embers = new Spout();
            var smoke = new Spout();
            for (int i = 0; i < 24; i++)
                s.Emit(new Vector3(J(-6f, 6f), J(.6f, 2.2f), J(-14f, -6f)), new Vector3(J(-.5f, .5f), J(-.5f, .3f), J(-.5f, .5f)), Sand, J(.2f, .35f), J(.6f, 1.4f), 8f);
            s.Every(t =>
            {
                float strength = 0f;
                foreach (var (start, length, power) in bursts)
                    if (t >= start && t < start + length) strength = power * (1f - .5f * (t - start) / length);
                bool firing = strength > 0f;
                ship.SetColour("control", firing && Mathf.Repeat(t * 9f, 1f) < .7f ? Table("control") : BrokenGrey);
                // The full tank flickers unevenly between bright and dull while the ignition fails, then stays dull.
                float flicker = Mathf.PerlinNoise(t * 5f, .3f);
                ship.SetColour("core", t < dead ? Color.Lerp(Color.Lerp(FullTank, EmptyTank, .6f), FullTank, flicker) : Color.Lerp(FullTank, EmptyTank, .45f));
                Vector3 nozzle = s.OnStage(ship.transform, new Vector3(0f, 1.5f, -7.6f)), back = s.OnStageDirection(ship.transform, Vector3.back);
                if (firing)
                {
                    for (int n = jet.Take(110f * strength, s.Step); n > 0; n--)
                        s.Emit(nozzle + Jitter(.4f, .4f, .1f), back * (J(4f, 9f) * strength) + Jitter(.8f, .6f, .8f), ExhaustCyan, J(.3f, .5f), J(.4f, .8f), 0f, DullOrange);
                    ship.transform.localPosition = Jitter(.04f * strength, .03f * strength, 0f);
                    s.Shake(.06f * strength);
                }
                else ship.transform.localPosition = Vector3.zero;

                if (t >= dead)
                    for (int n = embers.Take(3f, s.Step); n > 0; n--)
                        s.Emit(nozzle + Jitter(.3f, .3f, .1f), back * J(1f, 2.5f) + Vector3.up * J(.2f, .8f), Table("fire"), J(.2f, .35f), J(.6f, 1f), 1f, DullOrange);
                if (t >= 1f)
                    for (int n = smoke.Take(t >= dead ? 7f : 3f, s.Step); n > 0; n--)
                        s.Emit(nozzle + Jitter(.4f, .3f, .2f), Vector3.up * J(.6f, 1.3f) + back * J(.2f, .6f), Smoke, J(.4f, .8f), J(1.8f, 2.8f));
            });
            s.CameraMove(new Vector3(5f, 1.9f, -20.5f), new Vector3(3f, 2.4f, 0f), new Vector3(4.8f, 1.9f, -20f), new Vector3(3f, 2.4f, 0f), 40f);
            s.Subject(ship.transform, new Vector3(0f, 1.5f, -6.2f));
        }

        // ───── L3: in the cockpit, the engine wants an enormous energy it does not have ─────

        static void StageCockpit(V2Cutscene s)
        {
            s.Sky(DawnSky);
            s.Box(new Vector3(400f, 1f, 400f), new Vector3(0f, -.5f, 120f), Sand); // the desert beyond the window
            s.Box(new Vector3(10f, 4f, 10f), new Vector3(13f, 2f, 30f), Table("base")); // a corner of the base out there
            s.Box(new Vector3(10f, .2f, 9f), new Vector3(0f, .9f, 0f), SlateFloor);
            // The front wall at z = 4.4 with a wide window, x -3.9 to 3.9 and y 2.0 to 4.2.
            s.Box(new Vector3(10f, 1f, .3f), new Vector3(0f, 1.5f, 4.4f), Slate);
            s.Box(new Vector3(10f, .9f, .3f), new Vector3(0f, 4.65f, 4.4f), Slate);
            s.Box(new Vector3(1.1f, 2.2f, .3f), new Vector3(-4.45f, 3.1f, 4.4f), Slate);
            s.Box(new Vector3(1.1f, 2.2f, .3f), new Vector3(4.45f, 3.1f, 4.4f), Slate);
            s.Box(new Vector3(.3f, 4.2f, 9f), new Vector3(-5f, 3f, 0f), Slate);
            s.Box(new Vector3(.3f, 4.2f, 9f), new Vector3(5f, 3f, 0f), Slate);
            s.Box(new Vector3(10.3f, .3f, 9.3f), new Vector3(0f, 5.2f, 0f), SlateCeiling);
            s.Box(new Vector3(3.4f, 1.1f, 1f), new Vector3(0f, 1.55f, 3f), ConsoleGrey);
            // The diagram over the console, on a dark backing so its cyan reads against the bright window: the engine as a long
            // outline, inside it the empty ring where the energy belongs, rows of cubes running at the ring and stopping short of it,
            // and a gauge of ten boxes with only the bottom one lit. No writing: the shapes say it.
            const float front = 2.85f, across = .35f, rowStart = -1.3f + across, rowEnd = .15f + across;
            s.Box(new Vector3(3.5f, 1.35f, .04f), new Vector3(.15f + across, 2.75f, 2.92f), ScreenBack);
            Outline(s, new Vector3(-.2f + across, 2.75f, front), 2.6f, .9f, .06f, Table("door"));
            var ring = Outline(s, new Vector3(.55f + across, 2.75f, front), .5f, .5f, .07f, RingRed).GetComponentsInChildren<MeshRenderer>();
            var runners = new List<(Transform cube, float y, float phase)>();
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 5; k++)
                    runners.Add((s.Box(Vector3.one * .08f, new Vector3(rowStart, 2.55f + row * .2f, front), Table("door")).transform, 2.55f + row * .2f, k / 5f + row * .07f));
            MeshRenderer lastLit = null;
            for (int i = 0; i < 10; i++)
            {
                var box = s.Box(new Vector3(.16f, .1f, .06f), new Vector3(1.85f, 2.3f + i * .12f, front), i == 0 ? Table("door") : BrokenGrey);
                if (i == 0) lastLit = box;
            }

            // Felix from behind, in the left half, leaning slowly toward the diagram.
            var felix = s.Felix(new Vector3(-1.5f, 1f, 1f), 32f);
            float seconds = s.Seconds;
            s.Every(t =>
            {
                bool lit = Mathf.Repeat(t * 1.6f, 1f) < .55f;
                foreach (var bar in ring) Paint(bar, lit ? RingRed : RingDim);
                foreach (var (cube, y, phase) in runners)
                {
                    // Each runs at the ring, stops short of it for a moment, and starts again.
                    float p = Mathf.Repeat(t * .55f + phase, 1f);
                    cube.gameObject.SetActive(p < .92f);
                    cube.localPosition = new Vector3(Mathf.Lerp(rowStart, rowEnd, Mathf.Min(1f, p * 1.3f)), y, front);
                }

                Paint(lastLit, Color.Lerp(BrokenGrey, Table("door"), .55f + .45f * Mathf.PerlinNoise(t * 7f, .6f)));
                felix.transform.localRotation = Quaternion.Euler(5f * Smooth(t / seconds), 32f, 0f);
            });
            s.CameraMove(new Vector3(.7f, 2.7f, -3.8f), new Vector3(.25f, 2.35f, 3f), new Vector3(.5f, 2.62f, -3.05f), new Vector3(.2f, 2.4f, 3f), 46f);
            s.LightFrom(135f);
            s.Subject(ring[0].transform.parent, Vector3.zero);
        }

        /// <summary>A rectangle outline of four thin bars standing in the plane of the picture (x across, y up), facing -z.</summary>
        static Transform Outline(V2Cutscene s, Vector3 centre, float width, float height, float bar, Color colour)
        {
            var outline = s.Node("Outline", centre);
            s.Box(new Vector3(width + bar, bar, bar), new Vector3(0f, height * .5f, 0f), colour, default, outline);
            s.Box(new Vector3(width + bar, bar, bar), new Vector3(0f, -height * .5f, 0f), colour, default, outline);
            s.Box(new Vector3(bar, height + bar, bar), new Vector3(width * .5f, 0f, 0f), colour, default, outline);
            s.Box(new Vector3(bar, height + bar, bar), new Vector3(-width * .5f, 0f, 0f), colour, default, outline);
            return outline;
        }

        // ───── L4: the wave of dark matter spreads; the Tyrant senses it ─────

        static void StageSurge(V2Cutscene s)
        {
            s.Sky(DawnSky);
            s.Ground(Sand);
            // A ridge of big rocks across the foreground, the camera on it behind the Tyrant, looking back at the base far away.
            var random = new System.Random(14);
            var rocks = new List<Matrix4x4>();
            for (int i = 0; i < 14; i++)
            {
                float w = R(random, 5f, 9f), h = R(random, 2.5f, 4.5f);
                rocks.Add(Matrix4x4.TRS(new Vector3(-22f + i * 3.6f + R(random, -1f, 1f), h * .5f, R(random, -2f, 8f)), Quaternion.Euler(0f, R(random, -20f, 20f), 0f),
                    new Vector3(w, h, R(random, 5f, 9f))));
            }

            Vector3 tyrantAt = new(-3.2f, 3.6f, 9f);
            rocks.Add(Matrix4x4.TRS(new Vector3(tyrantAt.x - .3f, tyrantAt.y * .5f, tyrantAt.z + .5f), Quaternion.Euler(0f, 8f, 0f), new Vector3(8f, tyrantAt.y, 8f)));
            s.Batch(Prim.Box, rocks, Rock, "Ridge");
            Vector3 baseAt = new(75f, 0f, 140f), shipAt = baseAt + new Vector3(-14f, 0f, -6f);
            BaseWithLamps(s, baseAt);
            s.Look("ship", shipAt, new Vector3(0f, 90f, 0f)).SetColour("core", FullTank);
            var tyrant = Tyrant(s, tyrantAt, -95f);
            // The ring of dark-matter energy: one thin flat band of bright violet growing from the ship over the ground.
            const int segments = 96;
            var ring = new Transform[segments];
            for (int i = 0; i < segments; i++) ring[i] = s.Box(Vector3.one, shipAt, Violet).transform;
            Vector3 eye = new(3f, 12.5f, -14f), toEye = eye - shipAt;
            toEye.y = 0f;
            float distance = toEye.magnitude, facingEye = Mathf.Atan2(toEye.z, toEye.x), seconds = s.Seconds;
            float face = Mathf.Atan2(shipAt.x - tyrantAt.x, shipAt.z - tyrantAt.z) * Mathf.Rad2Deg;
            Vector3 stepTo = tyrantAt + Quaternion.Euler(0f, face, 0f) * Vector3.forward * 2f;
            var hops = new Spout();
            bool landed = false;
            s.Every(t =>
            {
                float radius = 4f + (distance + 30f) * t / seconds;
                float length = 2f * Mathf.PI * radius / segments * 1.02f;
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    ring[i].localPosition = shipAt + new Vector3(Mathf.Cos(a) * radius, .18f, Mathf.Sin(a) * radius);
                    ring[i].localRotation = Quaternion.Euler(0f, 90f - a * Mathf.Rad2Deg, 0f);
                    ring[i].localScale = new Vector3(length, .35f, 1.8f);
                }

                // Small violet cubes hop up along the part of the ring coming toward the camera and fall back.
                for (int n = hops.Take(60f, s.Step); n > 0; n--)
                {
                    float a = facingEye + J(-.6f, .6f);
                    Vector3 at = shipAt + new Vector3(Mathf.Cos(a) * radius, .3f, Mathf.Sin(a) * radius);
                    s.Emit(at, new Vector3(J(-.5f, .5f), J(3f, 5.5f), J(-.5f, .5f)), Violet, J(.4f, .8f), J(.9f, 1.2f), 9f);
                }

                // The Tyrant turns until its wedge points at the ring, its red flares a moment, and it slides one heavy step forward.
                float yaw = Mathf.LerpAngle(-95f, face, Smooth(t / 3f));
                float flare = Mathf.Sin(Mathf.PI * Ramp(t, 3f, .6f));
                float step = Smooth(Ramp(t, 3.6f, .8f));
                tyrant.transform.localPosition = Vector3.Lerp(tyrantAt, stepTo, step);
                tyrant.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                tyrant.SetColour("tyrant", Color.Lerp(Table("tyrant"), TyrantFlash, flare));
                if (!landed && step >= 1f)
                {
                    landed = true;
                    SandWave(s, stepTo, 26, 5f, stepTo.y);
                }

                if (step > 0f && step < 1f || landed && t < 4.9f) s.Shake(.05f);
            });
            s.CameraMove(eye, new Vector3(16f, 2f, 60f), new Vector3(3.4f, 12.8f, -15f), new Vector3(16f, 2f, 60f), 46f);
            s.LightFrom(120f);
            s.Subject(tyrant.transform, new Vector3(0f, 6f, 0f));
        }

        /// <summary>The Tyrant as the whitebox shows it, with a white wedge on the front of its top box for the way it faces.</summary>
        static WhiteboxLook Tyrant(V2Cutscene s, Vector3 at, float yaw, float scale = 1f)
        {
            var tyrant = s.Look("tyrant", at, new Vector3(0f, yaw, 0f), scale);
            s.Solid(Prim.Wedge, new Vector3(.9f, .6f, .7f), new Vector3(0f, 7.9f, 1.4f), Table("white"), default, tyrant.transform);
            return tyrant;
        }

        // ───── L5: the Tyrant rises outside the base ─────

        static void StageArrival(V2Cutscene s)
        {
            s.Sky(DawnSky);
            s.Ground(Sand);
            var random = new System.Random(15);
            var rocks = new List<Matrix4x4>();
            for (int i = 0; i < 5; i++)
            {
                float w = R(random, 4.5f, 6.5f), h = R(random, 2f, 3f);
                rocks.Add(Matrix4x4.TRS(new Vector3(-5f + i * 3.6f + R(random, -.8f, .8f), h * .5f, 8.5f + R(random, -1f, 1f)), Quaternion.Euler(0f, R(random, -15f, 15f), 0f),
                    new Vector3(w, h, R(random, 4f, 6f))));
            }

            s.Batch(Prim.Box, rocks, Rock, "Rocks");
            // The ship in the middle distance on the left, its engine dark.
            var ship = s.Look("ship", new Vector3(-19f, 0f, 25f), new Vector3(0f, 70f, 0f));
            ship.SetColour("control", BrokenGrey);
            ship.SetColour("core", FullTank);
            // The Tyrant rises from behind the rocks, one box after another, its wedge pointing down at Felix; its tail swings once.
            Vector3 tyrantAt = new(-3.2f, 0f, 13f);
            const float sunk = 14.5f, scale = 1.4f;
            var tyrant = Tyrant(s, tyrantAt + Vector3.down * sunk, 180f, scale);
            var hinge = s.Node("Tail hinge", new Vector3(0f, 1f, -2.2f), default, tyrant.transform);
            tyrant.PartRenderer(5).transform.SetParent(hinge, true); // the tail, the table's sixth part, swings about the back of the body
            // Felix in the right foreground from behind, looking up at it.
            Vector3 felixAt = new(2.6f, 0f, -1.4f);
            var felix = s.Felix(felixAt, -22f);
            var pour = new Spout();
            var hop = new Spout();
            s.Every(t =>
            {
                tyrant.transform.localPosition = tyrantAt + Vector3.down * (sunk * (1f - EaseOut(t / 3f)));
                tyrant.transform.localRotation = Quaternion.Euler(-6f, 180f, 0f);
                hinge.localRotation = Quaternion.Euler(0f, 28f * Mathf.Sin(Mathf.PI * Ramp(t, 3f, 1.1f)), 0f);
                felix.transform.localPosition = felixAt + Vector3.back * (.6f * Smooth(Ramp(t, 2.4f, .5f)));
                felix.transform.localRotation = Quaternion.Euler(-7f, -22f, 0f);
                if (t >= 3.4f) return;
                s.Shake(.1f * (1f - Ramp(t, 2.6f, .8f)));
                // Sand pours off the edges of its three boxes; the ground shakes small cubes toward the camera.
                for (int n = pour.Take(40f, s.Step); n > 0; n--)
                {
                    int level = UnityEngine.Random.Range(0, 3);
                    float half = level == 0 ? 2.2f : level == 1 ? 1.7f : 1.1f, top = level == 0 ? 3.6f : level == 1 ? 6.6f : 8.8f, u = J(-half, half);
                    float edge = UnityEngine.Random.value < .5f ? -half : half;
                    var local = UnityEngine.Random.value < .5f ? new Vector3(u, top, edge) : new Vector3(edge, top, u);
                    s.Emit(s.OnStage(tyrant.transform, local), Jitter(.6f, .2f, .6f), Sand, J(.25f, .45f), J(.9f, 1.4f), 9f);
                }

                for (int n = hop.Take(30f, s.Step); n > 0; n--)
                    s.Emit(new Vector3(J(-8f, 6f), .2f, J(5f, 7.5f)), new Vector3(J(-.5f, .5f), J(2f, 4f), -J(3f, 6f)), Sand, J(.2f, .35f), J(.7f, 1f), 9f);
            });
            // Low behind Felix, tilting slowly up the Tyrant.
            Vector3 eye = new(1.2f, 1.1f, -7.2f);
            s.CameraMove(eye, new Vector3(-2.6f, 4.2f, 13f), eye, new Vector3(-2.8f, 7.8f, 13f), 50f);
            s.LightFrom(130f);
            s.Subject(felix.transform, new Vector3(0f, 1f, 0f));
        }
    }
}
