using System;

namespace AnomalyArena
{
    /// <summary>
    /// One shot of a story cutscene, as data: its storyboard id (M1 to M9, L1 to L5, E1 to E3), how long it lasts, what is said and
    /// heard in it and when (its beats), and the method that stages it (V2Cutscene builds the set and moves it). V2Campaign plays a list
    /// of them (PlayCutscene); a shot can show a picture instead of its staging (CampaignArt.cutscenePictures) without any of the rest
    /// changing.
    /// </summary>
    public sealed class CutsceneShot
    {
        public readonly string id;
        public readonly float seconds;
        /// <summary>The shot's lines, sounds and white-outs, in the order of their times.</summary>
        public readonly CutsceneBeat[] beats;
        /// <summary>Builds the shot's set on the stage it is given and hands the stage its camera and its motions.</summary>
        public readonly Action<V2Cutscene> stage;

        public CutsceneShot(string id, float seconds, Action<V2Cutscene> stage, params CutsceneBeat[] beats)
        {
            this.id = id;
            this.seconds = seconds;
            this.stage = stage;
            this.beats = beats ?? Array.Empty<CutsceneBeat>();
            Array.Sort(this.beats, (a, b) => a.at.CompareTo(b.at));
        }
    }

    /// <summary>Something that happens at a moment of a shot (seconds from its start): a line said with its subtitle, a sound, or a
    /// white-out over the whole screen that rises for its seconds and falls for as long again (V2CampaignUi.WhiteOut).</summary>
    public readonly struct CutsceneBeat
    {
        public readonly float at;
        public readonly V2Campaign.Line? line;
        public readonly CampaignSfx? sound;
        public readonly float whiteOut;

        CutsceneBeat(float at, V2Campaign.Line? line, CampaignSfx? sound, float whiteOut)
        {
            this.at = at;
            this.line = line;
            this.sound = sound;
            this.whiteOut = whiteOut;
        }

        public static CutsceneBeat Say(V2Campaign.Line line, float at) => new(at, line, null, 0f);

        public static CutsceneBeat Sound(CampaignSfx sound, float at) => new(at, null, sound, 0f);

        public static CutsceneBeat WhiteOut(float seconds, float at) => new(at, null, null, seconds);
    }
}
