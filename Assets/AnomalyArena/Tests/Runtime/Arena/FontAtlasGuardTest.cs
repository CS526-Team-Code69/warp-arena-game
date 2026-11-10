using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The guard that draws texts again after a dynamic font's texture was rebuilt (FontAtlasGuard, 2026-11-10): a rebuild is
    /// followed by a redraw of that font's texts, whose glyphs then sit where the texture has them; with the guard off it draws
    /// nothing again; its count of stale glyphs finds a text that missed a rebuild; and the whitebox title, where the fault was
    /// seen, keeps its wordmark out of the game's font and shows no stale glyph. The fault itself (a text left with glyph
    /// positions of a texture that is gone, seen on the whitebox title after a fresh start) needs the game's title and a font texture
    /// that grows several times in one frame; the editor's interface tour checks that case after "Reset the font texture".</summary>
    [TestFixture]
    public class FontAtlasGuardTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const string Giant = "WARPEN";
        const int GiantSize = 300;
        Font font;

        [TearDown]
        public void TearDown()
        {
            FontAtlasGuard.Enabled = true;
            if (font) Object.Destroy(font);
        }

        [Test, CreateScene, Timeout(30000)]
        public async Task FontTextureRebuilt_ItsTextsAreDrawnAgain_AndTheirGlyphsMatchTheTexture()
        {
            var label = await LabelInAFreshFont();
            Assert.That(FontAtlasGuard.StaleGlyphs(label), Is.Zero, "the text is drawn right before the rebuild");
            int before = FontAtlasGuard.Refreshes;
            Assume.That(RebuildsWhile(() => font.RequestCharactersInTexture(Giant, GiantSize, FontStyle.Bold)), Is.GreaterThan(0), "letters this large rebuild a fresh font's texture");
            await Frames(3);
            Assert.That(FontAtlasGuard.Refreshes, Is.GreaterThan(before), "the texts were drawn again after the rebuild");
            Assert.That(FontAtlasGuard.StaleGlyphs(label), Is.Zero, "every glyph of the text is where the texture has it now");
            Assert.That(FontAtlasGuard.StaleTexts(), Is.Empty, "no text on screen shows stale glyphs");
        }

        [Test, CreateScene, Timeout(30000)]
        public async Task FontTextureRebuilt_GuardOff_DrawsNothingAgain()
        {
            await LabelInAFreshFont();
            FontAtlasGuard.Enabled = false;
            int before = FontAtlasGuard.Refreshes;
            Assume.That(RebuildsWhile(() => font.RequestCharactersInTexture(Giant, GiantSize, FontStyle.Bold)), Is.GreaterThan(0), "letters this large rebuild a fresh font's texture");
            await Frames(3);
            Assert.That(FontAtlasGuard.Refreshes, Is.EqualTo(before), "the guard is off");
        }

        [Test, CreateScene, Timeout(30000)]
        public async Task StaleGlyphs_TextThatMissedARebuild_IsCounted_AndIsRightAgainOnceDrawnAgain()
        {
            var label = await LabelInAFreshFont();
            // A disabled text is not told about a rebuild and keeps the mesh it made, which is what a text that missed one looks like.
            label.enabled = false;
            Assume.That(RebuildsWhile(() => font.RequestCharactersInTexture(Giant, GiantSize, FontStyle.Bold)), Is.GreaterThan(0), "letters this large rebuild a fresh font's texture");
            Assert.That(FontAtlasGuard.StaleGlyphs(label), Is.GreaterThan(0), "the mesh it kept points at glyph positions the texture no longer has");
            label.enabled = true;
            await Frames(3);
            Assert.That(FontAtlasGuard.StaleGlyphs(label), Is.Zero, "drawn again, its glyphs are where the texture has them");
        }

        [Test, LoadScene(Scene), Timeout(60000)]
        public async Task WhiteboxTitle_TheWordmarkHasAFontOfItsOwn_AndNoTextShowsStaleGlyphs()
        {
            bool was = Art.Enabled;
            try
            {
                await Awaitable.NextFrameAsync();
                float deadline = Time.realtimeSinceStartup + 12f;
                while (!(GameManager.Instance && ArenaText.Ready && WorldMap.Active))
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "scene, localization and map initialize");
                    await Awaitable.NextFrameAsync();
                }
                Art.Enabled = false;
                await Frames(6);
                Text wordmark = null;
                foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
                    if (text.name == "WhiteboxWordmark") wordmark = text;
                Assert.That(wordmark, Is.Not.Null, "the whitebox title shows the game's name as text");
                Assert.That(wordmark.font, Is.Not.Null.And.Not.SameAs(ArenaText.Font), "its large letters are not in the game's font, whose texture they would rebuild");
                Assert.That(FontAtlasGuard.StaleTexts(), Is.Empty, "every text of the whitebox title is drawn from where the fonts' textures have its glyphs");
            }
            finally { Art.Enabled = was; }
        }

        /// <summary>A text on a canvas of its own, in a font no other test has used: the font's texture starts at its smallest.</summary>
        async Task<Text> LabelInAFreshFont()
        {
            font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            Assume.That(font, Is.Not.Null, "the machine has the Arial font");
            var canvas = new GameObject("Canvas", typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(canvas.transform, false);
            label.rectTransform.sizeDelta = new Vector2(900f, 120f);
            label.font = font;
            label.fontSize = 40;
            label.supportRichText = false;
            label.text = "Key hints stay readable";
            await Frames(3);
            Assume.That(label.cachedTextGenerator.vertexCount, Is.GreaterThan(0), "the text made its mesh");
            return label;
        }

        int RebuildsWhile(Action act)
        {
            int rebuilds = 0;
            void Count(Font rebuiltFont) { if (rebuiltFont == font) rebuilds++; }
            Font.textureRebuilt += Count;
            try { act(); }
            finally { Font.textureRebuilt -= Count; }
            return rebuilds;
        }

        static async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await Awaitable.NextFrameAsync();
        }
    }
}
