using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// Draws every text again once a dynamic font's texture has been rebuilt, after the canvas update that rebuilt it is over.
    /// Unity redraws the other texts itself, but from inside the rebuild, and a text redrawn there can keep glyph positions of a
    /// texture that changes once more before the frame ends: it then shows pieces of other glyphs until its string changes. Seen
    /// 2026-11-10 on a fresh start in whitebox: the title's wordmark grew the texture four times in one frame (512 x 512 to
    /// 1024 x 2048) and the key hints, which nothing redraws afterwards, stayed broken; a call of Text.FontTextureChanged in a
    /// later frame put them right. The wordmark has had the built-in font since (V3TitleBackdrop), and this guard does that later
    /// call for every text and every such rebuild: after a rebuild each active text of that font forgets its cached mesh and the
    /// canvases update once more, until an update passes without a rebuild. A text made outside a rebuild has the right glyphs.
    /// </summary>
    public static class FontAtlasGuard
    {
        /// <summary>Off only in a test that shows what a rebuild leaves behind without the guard.</summary>
        public static bool Enabled = true;

        /// <summary>How many times the texts were drawn again since the play began.</summary>
        public static int Refreshes { get; private set; }

        // A redraw may itself rebuild the texture (a glyph that was dropped comes back): up to this many more updates in the same
        // frame, the rest in the next frame. A font rebuilt in this many frames in a row is left alone until a frame passes
        // without a rebuild, so a texture too small for what the screen shows cannot make every text redraw every frame for good.
        const int PassesPerFrame = 3, FramesInARow = 30;

        static readonly HashSet<Font> rebuilt = new();
        static readonly List<Font> fonts = new();
        static bool refreshing, warned;
        static int lastFrame = -2, framesInARow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            rebuilt.Clear();
            refreshing = warned = false;
            lastFrame = -2;
            framesInARow = 0;
            Refreshes = 0;
            Enabled = true;
            Font.textureRebuilt -= OnRebuilt;
            Font.textureRebuilt += OnRebuilt;
            Canvas.willRenderCanvases -= AfterCanvasUpdate;
        }

        // After the scene has loaded: Unity's own canvas update listens to the same event from the moment the first graphic
        // registers, so this one is called after it and sees the rebuilds of the frame that is about to be drawn. Called before it
        // instead, it would see them one frame later, which is still right, only one frame late.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Listen()
        {
            Canvas.willRenderCanvases -= AfterCanvasUpdate;
            Canvas.willRenderCanvases += AfterCanvasUpdate;
        }

        static void OnRebuilt(Font font)
        {
            if (font && Application.isPlaying) rebuilt.Add(font);
        }

        /// <summary>For tests and checks: how many of a text's quads are not on the glyph of any of its characters as the font's
        /// texture has them now, plus the characters whose glyph the texture has dropped; 0 for a text that is drawn right. Which
        /// character a quad belongs to is not asked: a space has no quad, an ideographic space has one. A text with rich text tags
        /// is not counted (a tag can change the style of a part of it).</summary>
        public static int StaleGlyphs(Text text)
        {
            if (!text || !text.font || string.IsNullOrEmpty(text.text)) return 0;
            if (text.supportRichText && text.text.IndexOf('<') >= 0) return 0;
            var generator = text.cachedTextGenerator;
            var verts = generator.verts;
            int size = generator.fontSizeUsedForBestFit, stale = 0;
            glyphs.Clear();
            seen.Clear();
            foreach (char c in text.text)
            {
                if (char.IsControl(c) || !seen.Add(c) || !text.font.HasCharacter(c)) continue;
                if (text.font.GetCharacterInfo(c, out var glyph, size, text.fontStyle)) glyphs.Add(glyph);
                else if (!char.IsWhiteSpace(c)) stale++; // a character the text shows, and the texture no longer holds its glyph
            }
            for (int quad = 0; quad * 4 + 3 < verts.Count; quad++)
            {
                // A quad without any area shows nothing, whatever it points at.
                if ((verts[quad * 4].position - verts[quad * 4 + 2].position).sqrMagnitude < 1e-6f) continue;
                bool onAGlyph = false;
                foreach (var glyph in glyphs)
                {
                    onAGlyph = true;
                    for (int corner = 0; corner < 4 && onAGlyph; corner++)
                    {
                        Vector2 uv = verts[quad * 4 + corner].uv0;
                        onAGlyph = Near(uv, glyph.uvTopLeft) || Near(uv, glyph.uvTopRight) || Near(uv, glyph.uvBottomRight) || Near(uv, glyph.uvBottomLeft);
                    }
                    if (onAGlyph) break;
                }
                if (!onAGlyph) stale++;
            }
            return stale;
        }

        static readonly List<CharacterInfo> glyphs = new();
        static readonly HashSet<char> seen = new();

        static bool Near(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) < .0005f && Mathf.Abs(a.y - b.y) < .0005f;

        /// <summary>For tests and checks: the active texts on screen that show stale glyphs, by name, with the count for each.</summary>
        public static List<string> StaleTexts()
        {
            var found = new List<string>();
            foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
            {
                if (!text.isActiveAndEnabled) continue;
                int stale = StaleGlyphs(text);
                if (stale > 0) found.Add($"{text.name} ({stale})");
            }

            return found;
        }

        static void AfterCanvasUpdate()
        {
            if (refreshing || rebuilt.Count == 0 || !Application.isPlaying) return;
            if (!Enabled)
            {
                rebuilt.Clear();
                return;
            }

            int frame = Time.frameCount;
            if (frame != lastFrame) framesInARow = frame == lastFrame + 1 ? framesInARow + 1 : 1;
            lastFrame = frame;
            if (framesInARow > FramesInARow)
            {
                if (!warned)
                    Debug.LogWarning(
                        $"[FontAtlasGuard] a font texture was rebuilt in {FramesInARow} frames in a row; texts are not drawn again until that stops");
                warned = true;
                rebuilt.Clear();
                return;
            }

            refreshing = true;
            try
            {
                for (int pass = 0; pass < PassesPerFrame && rebuilt.Count > 0; pass++)
                {
                    fonts.Clear();
                    fonts.AddRange(rebuilt);
                    rebuilt.Clear();
                    foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
                        if (text && text.font && fonts.Contains(text.font))
                            text.FontTextureChanged();
                    Refreshes++;
                    Canvas.ForceUpdateCanvases();
                }
            }
            finally
            {
                refreshing = false;
            }
        }
    }
}