using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// One shot of a story cutscene, as data: its storyboard id (the memory M11 to M20, the failed ignition L11 to L15, the ending E11 to
    /// E14), how long it lasts, whether it shows the present or a memory, how it begins, which part of its picture is on screen from its
    /// first moment to its last, what is said and heard in it and when (its beats), and the method that stages it (V2Cutscene builds the
    /// set and moves it). V2Campaign plays a list of them (PlayCutscene); a shot can show a picture instead of its staging
    /// (CampaignArt.cutscenePictures, by the shot's id) without any of the rest changing.
    /// </summary>
    public sealed class CutsceneShot
    {
        public readonly string id;
        /// <summary>The shot's length as the table writes it. It plays longer only while its last line still needs the time
        /// (V2Campaign.Schedule).</summary>
        public readonly float seconds;
        /// <summary>The present, or a memory: a memory is graded warm and pale with deep dark edges, the present keeps its colours.</summary>
        public readonly CutsceneTime time;
        /// <summary>How the shot begins.</summary>
        public readonly CutsceneCut cut;
        /// <summary>The part of the shot's picture on screen over the shot; a shot staged in whitebox has its own camera instead.</summary>
        public readonly CutsceneFraming framing;
        /// <summary>The shot's lines, sounds, white-outs and cuts to black, in the order of their times.</summary>
        public readonly CutsceneBeat[] beats;
        /// <summary>Builds the shot's set on the stage it is given and hands the stage its camera and its motions.</summary>
        public readonly Action<V2Cutscene> stage;

        public CutsceneShot(string id, float seconds, CutsceneTime time, CutsceneCut cut, CutsceneFraming framing, Action<V2Cutscene> stage,
            params CutsceneBeat[] beats)
        {
            this.id = id;
            this.seconds = seconds;
            this.time = time;
            this.cut = cut;
            this.framing = framing;
            this.stage = stage;
            this.beats = beats ?? Array.Empty<CutsceneBeat>();
            Array.Sort(this.beats, (a, b) => a.at.CompareTo(b.at));
        }

        /// <summary>Where the shot's closing begins: a white-out that is full as the shot ends, or a cut to black (infinity: the shot has
        /// none). A shot that plays longer than its seconds keeps its closing at its end.</summary>
        public float ClosingAt
        {
            get
            {
                float at = float.PositiveInfinity;
                foreach (var beat in beats)
                    if (beat.black || EndsWithShot(beat)) at = Mathf.Min(at, beat.at);
                return at;
            }
        }

        /// <summary>The screen is white as the shot ends: nothing after it needs a fade to black.</summary>
        public bool EndsWhite
        {
            get
            {
                foreach (var beat in beats)
                    if (EndsWithShot(beat)) return true;
                return false;
            }
        }

        bool EndsWithShot(CutsceneBeat beat) => beat.whiteOut > 0f && beat.at + beat.whiteOut >= seconds - .01f;
    }

    /// <summary>When a shot happens: in the story's present, or in Felix's memory.</summary>
    public enum CutsceneTime { Present, Memory }

    /// <summary>How a shot begins: a hard cut; a half-second dissolve from the last picture of the shot before; or from black, the shot
    /// before fading out to black first.</summary>
    public enum CutsceneCut { Hard, Dissolve, Black }

    /// <summary>Something that happens at a moment of a shot (seconds from its start): a line said with its subtitle, a sound, a
    /// white-out over the whole screen that rises for its seconds and falls for as long again (V2CampaignUi.WhiteOut), or the picture
    /// cut to black for the rest of the shot.</summary>
    public readonly struct CutsceneBeat
    {
        public readonly float at;
        public readonly V2Campaign.Line? line;
        public readonly CampaignSfx? sound;
        public readonly float whiteOut;
        public readonly bool black;

        CutsceneBeat(float at, V2Campaign.Line? line, CampaignSfx? sound, float whiteOut, bool black)
        {
            this.at = at;
            this.line = line;
            this.sound = sound;
            this.whiteOut = whiteOut;
            this.black = black;
        }

        public static CutsceneBeat Say(V2Campaign.Line line, float at) => new(at, line, null, 0f, false);

        public static CutsceneBeat Sound(CampaignSfx sound, float at) => new(at, null, sound, 0f, false);

        public static CutsceneBeat WhiteOut(float seconds, float at) => new(at, null, null, seconds, false);

        public static CutsceneBeat CutToBlack(float at) => new(at, null, null, 0f, true);
    }

    /// <summary>The part of a picture on screen: the point of the picture at the screen's centre (x from its left edge, y from its top,
    /// 0 to 1) and how much of the picture's width shows (1: all of it). The height follows, as the screen's picture and the pictures
    /// are both 16:9.</summary>
    public readonly struct CutsceneFrame
    {
        public readonly float x, y, size;

        public CutsceneFrame(float x, float y, float size)
        {
            this.x = x;
            this.y = y;
            this.size = size;
        }

        /// <summary>The whole picture.</summary>
        public static CutsceneFrame Whole => new(.5f, .5f, 1f);

        /// <summary>The same part as a RawImage's uvRect (measured from the picture's lower left), moved back inside the picture where it
        /// would stick out.</summary>
        public Rect Uv
        {
            get
            {
                float s = Mathf.Clamp(size, .1f, 1f);
                return new Rect(Mathf.Clamp(x - s * .5f, 0f, 1f - s), Mathf.Clamp(1f - y - s * .5f, 0f, 1f - s), s, s);
            }
        }

        public static CutsceneFrame Lerp(CutsceneFrame a, CutsceneFrame b, float k) =>
            new(Mathf.Lerp(a.x, b.x, k), Mathf.Lerp(a.y, b.y, k), Mathf.Lerp(a.size, b.size, k));
    }

    /// <summary>How a shot's picture is framed over the shot: frames at moments of it (seconds from its start), eased from one to the
    /// next. Before the first and after the last the picture holds still, so a shot that plays longer than its seconds holds its last
    /// frame. With no frames the whole picture shows.</summary>
    public readonly struct CutsceneFraming
    {
        // A time below zero stands for the end the shot's table gives it.
        readonly float[] times;
        readonly CutsceneFrame[] frames;

        CutsceneFraming(float[] times, CutsceneFrame[] frames)
        {
            this.times = times;
            this.frames = frames;
        }

        /// <summary>From one frame at the shot's start to another at the end its table gives it.</summary>
        public static CutsceneFraming Move(CutsceneFrame from, CutsceneFrame to) => new(new[] { 0f, -1f }, new[] { from, to });

        /// <summary>Frames at the given moments of the shot (seconds from its start, in order).</summary>
        public static CutsceneFraming Keys(params (float at, CutsceneFrame frame)[] keys)
        {
            var times = new float[keys.Length];
            var frames = new CutsceneFrame[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                times[i] = keys[i].at;
                frames[i] = keys[i].frame;
            }

            return new CutsceneFraming(times, frames);
        }

        /// <summary>The frame at a moment of a shot whose table gives it seconds.</summary>
        public CutsceneFrame At(float t, float seconds)
        {
            if (frames == null || frames.Length == 0) return CutsceneFrame.Whole;
            if (t <= When(times[0], seconds)) return frames[0];
            for (int i = 1; i < frames.Length; i++)
            {
                float from = When(times[i - 1], seconds), to = When(times[i], seconds);
                if (t < to) return CutsceneFrame.Lerp(frames[i - 1], frames[i], V2Cutscene.Smooth((t - from) / Mathf.Max(1e-4f, to - from)));
            }

            return frames[frames.Length - 1];
        }

        static float When(float at, float seconds) => at < 0f ? seconds : at;
    }
}
