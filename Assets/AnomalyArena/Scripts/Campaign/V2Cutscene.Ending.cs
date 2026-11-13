using UnityEngine;
using Prim = AnomalyArena.Whitebox.Prim;

namespace AnomalyArena
{
    /// <summary>
    /// The ending (V2 doc section 2), played once the dark matter core is taken: the core rises beside the fallen Tyrant, the ship lifts
    /// off over the base, and it flies into the jump gate home, the picture turning white. The base system's E01 and E02 and Felix's E03
    /// keep their voices; the run is won when the white is full.
    /// </summary>
    public sealed partial class V2Cutscene
    {
        /// <summary>The ending's three shots, E1 to E3.</summary>
        public static readonly CutsceneShot[] Ending =
        {
            new("E1", 5f, StageCore),
            new("E2", 6f, StageLiftOff, CutsceneBeat.Sound(CampaignSfx.CoreInsert, 0f), CutsceneBeat.Sound(CampaignSfx.IgnitionSuccess, .8f),
                CutsceneBeat.Say(V2Campaign.Line.E01, .5f)),
            new("E3", 5f, StageWarp, CutsceneBeat.Say(V2Campaign.Line.E02, .3f), CutsceneBeat.Say(V2Campaign.Line.E03, 3f), CutsceneBeat.Sound(CampaignSfx.WarpJump, 3.4f),
                CutsceneBeat.WhiteOut(1f, 4f)),
        };

        static readonly Color GateHot = new(.86f, .66f, 1f);

        // ───── E1: the core rises beside the fallen Tyrant ─────

        static void StageCore(V2Cutscene s)
        {
            s.Sky(DawnSky);
            s.Ground(Sand);
            // The Tyrant fallen behind Felix: its three boxes on their sides in a row, dull now, its horns and its tail on the sand.
            var fallen = new[]
            {
                s.Box(new Vector3(4.4f, 3.6f, 4.4f), new Vector3(-7.5f, 2.2f, 13f), TyrantDull, new Vector3(0f, 20f, 90f)).transform,
                s.Box(new Vector3(3.4f, 3f, 3.4f), new Vector3(-3.2f, 1.7f, 14.5f), TyrantDull, new Vector3(0f, 25f, 90f)).transform,
                s.Box(new Vector3(2.2f, 2.2f, 2.2f), new Vector3(0f, 1.1f, 15.5f), TyrantDull, new Vector3(0f, 30f, 90f)).transform
            };
            s.Solid(Prim.Pyramid, new Vector3(.8f, 1.4f, .8f), new Vector3(1.8f, .4f, 15f), Table("tyrantHorn"), new Vector3(0f, 0f, 80f));
            s.Solid(Prim.Pyramid, new Vector3(.8f, 1.4f, .8f), new Vector3(2.2f, .4f, 16.4f), Table("tyrantHorn"), new Vector3(0f, 40f, 95f));
            s.Box(new Vector3(1f, 1f, 3f), new Vector3(-10.5f, .5f, 11.5f), TyrantDull, new Vector3(0f, 60f, 0f));
            s.Look("base", new Vector3(22f, 0f, 60f), new Vector3(0f, 200f, 0f)); // far away on the right
            // Felix from the front, the core half sunk in the sand before him: it rises turning on its point to the top of his box.
            s.Felix(Vector3.zero, 195f);
            var core = s.Solid(Prim.Octa, new Vector3(.9f, 1.2f, .9f), new Vector3(.2f, 0f, -1.25f), Table("core")).transform;
            var motes = new Spout();
            var smoke = new Spout();
            s.Every(t =>
            {
                core.localPosition = new Vector3(.2f, Mathf.Lerp(0f, 2f, Smooth(t / 4.2f)), -1.25f);
                core.localRotation = Quaternion.Euler(0f, 70f * t, 0f);
                for (int n = motes.Take(12f, s.Step); n > 0; n--)
                    s.Emit(core.localPosition + Jitter(.35f, .4f, .35f), new Vector3(J(-.15f, .15f), J(.6f, 1.2f), J(-.15f, .15f)), Violet, J(.1f, .18f), J(1.2f, 1.8f));
                for (int n = smoke.Take(9f, s.Step); n > 0; n--)
                {
                    var box = fallen[UnityEngine.Random.Range(0, fallen.Length)];
                    s.Emit(box.localPosition + Vector3.up * J(1f, 2f) + Jitter(1f, 0f, 1f), new Vector3(J(-.2f, .2f), J(.8f, 1.4f), J(-.2f, .2f)), Smoke, J(.4f, .8f), J(2f, 3f));
                }
            });
            s.CameraMove(new Vector3(2.6f, 2.1f, -8f), new Vector3(.2f, 1.6f, 0f), new Vector3(1.7f, 2f, -5.6f), new Vector3(.2f, 1.75f, -1.25f), 38f);
            s.LightFrom(150f);
            s.Subject(core, Vector3.zero);
        }

        // ───── E2: the ship lifts off over the base ─────

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

        // ───── E3: into the jump gate, home ─────

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
                float stretch = 1f + 18f * Mathf.Pow(Ramp(t, 2.5f, 2.5f), 2f);
                foreach (var star in stars) star.localScale = new Vector3(.9f, .9f, .9f * stretch);
            });
            s.CameraPath(t => new CameraPose(eye, new Vector3(3f, 1.5f, 40f), 50f));
            s.LightFrom(140f);
            s.Subject(frames[0], Vector3.zero);
        }
    }
}
